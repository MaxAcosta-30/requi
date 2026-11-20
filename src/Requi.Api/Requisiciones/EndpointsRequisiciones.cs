using System.Security.Claims;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Requi.Api.Comun;
using Requi.Api.Identidad;
using Requi.Domain.Entities;
using Requi.Domain.Reglas;
using Requi.Infrastructure.Identity;
using Requi.Infrastructure.Persistence;

namespace Requi.Api.Requisiciones;

/// <summary>Casos 3 y 4 (y el disparo del 5): requisiciones, envío, aprobación y rechazo.</summary>
public static class EndpointsRequisiciones
{
    public record RenglonEntrada(Guid ProductoId, int Cantidad);
    public record RequisicionEntrada(Guid ProveedorId, string? Notas, List<RenglonEntrada> Renglones, bool Enviar);
    public record RechazoEntrada(string Motivo);

    public record RenglonDto(Guid ProductoId, string Producto, int Cantidad, decimal PrecioUnitario, decimal Importe);
    public record FirmaDto(Guid AprobadorId, string Decision, string? Motivo, DateTime Fecha);
    public record RequisicionDto(
        Guid Id, long Folio, string Estado, Guid ProveedorId, string Proveedor, Guid SolicitanteId,
        decimal Total, string? Notas, string? MotivoRechazo, DateTime CreadaEn, DateTime? EnviadaEn,
        int FirmasNecesarias, List<FirmaDto> Firmas, List<RenglonDto> Renglones);

    public sealed class RequisicionValidador : AbstractValidator<RequisicionEntrada>
    {
        public RequisicionValidador()
        {
            RuleFor(x => x.ProveedorId).NotEmpty();
            RuleFor(x => x.Notas).MaximumLength(1000);
            RuleFor(x => x.Renglones).NotEmpty().WithMessage("Agrega al menos un renglón.")
                .Must(r => r.Count <= 50).WithMessage("Máximo 50 renglones.")
                .Must(r => r.Select(x => x.ProductoId).Distinct().Count() == r.Count)
                .WithMessage("Un producto no puede repetirse: ajusta la cantidad del renglón.");
            RuleForEach(x => x.Renglones).ChildRules(r =>
            {
                r.RuleFor(x => x.ProductoId).NotEmpty();
                r.RuleFor(x => x.Cantidad).InclusiveBetween(1, 100_000);
            });
        }
    }

    public sealed class RechazoValidador : AbstractValidator<RechazoEntrada>
    {
        public RechazoValidador() => RuleFor(x => x.Motivo).NotEmpty().MaximumLength(500);
    }

    public static WebApplication MapRequisiciones(this WebApplication app)
    {
        var g = app.MapGroup("/api/requisiciones").WithTags("Requisiciones").RequireAuthorization(IdentidadServicios.PoliticaApi);
        g.MapGet("/", Listar).WithName("ListarRequisiciones");
        g.MapGet("/{id:guid}", Obtener).WithName("ObtenerRequisicion");
        g.MapPost("/", Crear).WithName("CrearRequisicion").RequireAuthorization(Roles.Solicitante).Validar<RequisicionEntrada>();
        g.MapPost("/{id:guid}/enviar", Enviar).WithName("EnviarRequisicion").RequireAuthorization(Roles.Solicitante);
        g.MapPost("/{id:guid}/cancelar", Cancelar).WithName("CancelarRequisicion").RequireAuthorization(Roles.Solicitante);
        g.MapPost("/{id:guid}/aprobar", Aprobar).WithName("AprobarRequisicion").RequireAuthorization(Roles.Aprobador);
        g.MapPost("/{id:guid}/rechazar", Rechazar).WithName("RechazarRequisicion").RequireAuthorization(Roles.Aprobador).Validar<RechazoEntrada>();
        return app;
    }

    private static Guid Yo(ClaimsPrincipal u) => Guid.Parse(u.FindFirstValue(OpenIddictConstants.Claims.Subject)!);

    private static IQueryable<Requisition> ConTodo(RequiDbContext db) => db.Requisitions
        .Include(r => r.Supplier)
        .Include(r => r.Approvals)
        .Include(r => r.RequisitionLines);

    private static async Task<RequisicionDto> ADto(RequiDbContext db, Requisition r, CancellationToken ct)
    {
        var ids = r.RequisitionLines.Select(l => l.ProductId).ToList();
        var nombres = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        return new RequisicionDto(
            r.Id, r.Number, r.Status, r.SupplierId, r.Supplier.Name, r.RequesterId, r.Total, r.Notes, r.RejectionReason,
            r.CreatedAt, r.SubmittedAt, r.AprobacionesNecesarias,
            [.. r.Approvals.OrderBy(a => a.DecidedAt).Select(a => new FirmaDto(a.ApproverId, a.Decision, a.Reason, a.DecidedAt))],
            [.. r.RequisitionLines.Select(l => new RenglonDto(l.ProductId, nombres.GetValueOrDefault(l.ProductId, ""), l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice))]);
    }

    /// <summary>vista=mias (por defecto) · por-aprobar (Aprobador) · todas (Aprobador o Compras).</summary>
    private static async Task<Results<Ok<List<RequisicionDto>>, ForbidHttpResult>> Listar(
        ClaimsPrincipal u, RequiDbContext db, string? vista, CancellationToken ct)
    {
        var yo = Yo(u);
        var q = ConTodo(db).AsNoTracking();
        switch (vista ?? "mias")
        {
            case "mias":
                q = q.Where(r => r.RequesterId == yo);
                break;
            case "por-aprobar" when u.IsInRole(Roles.Aprobador):
                q = q.Where(r => r.Status == EstadoRequisicion.Enviada && r.RequesterId != yo && r.Approvals.All(a => a.ApproverId != yo));
                break;
            case "todas" when u.IsInRole(Roles.Aprobador) || u.IsInRole(Roles.Compras):
                break;
            default:
                return TypedResults.Forbid();
        }
        var lista = await q.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync(ct);
        var dtos = new List<RequisicionDto>(lista.Count);
        foreach (var r in lista) dtos.Add(await ADto(db, r, ct));
        return TypedResults.Ok(dtos);
    }

    private static async Task<Results<Ok<RequisicionDto>, NotFound>> Obtener(Guid id, ClaimsPrincipal u, RequiDbContext db, CancellationToken ct)
    {
        var r = await ConTodo(db).AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        // Sin permiso se responde 404, no 403: no se revela que la requisición existe.
        if (r is null || !(r.RequesterId == Yo(u) || u.IsInRole(Roles.Aprobador) || u.IsInRole(Roles.Compras)))
        {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(await ADto(db, r, ct));
    }

    private static async Task<Created<RequisicionDto>> Crear(RequisicionEntrada e, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct)
    {
        var proveedor = await db.Suppliers.FirstOrDefaultAsync(s => s.Id == e.ProveedorId && s.IsActive, ct)
            ?? throw new ReglaDeNegocioException("proveedor_invalido", "El proveedor no existe o está desactivado.");

        // El precio sale del catálogo, nunca del cliente.
        var ids = e.Renglones.Select(x => x.ProductoId).ToList();
        var productos = await db.Products.Where(p => ids.Contains(p.Id) && p.IsActive && p.SupplierId == proveedor.Id)
            .ToDictionaryAsync(p => p.Id, ct);
        if (productos.Count != ids.Count)
        {
            throw new ReglaDeNegocioException("producto_invalido", "Algún producto no existe, está desactivado o es de otro proveedor.");
        }

        var ahora = reloj.GetUtcNow().UtcDateTime;
        var r = new Requisition
        {
            Id = Guid.CreateVersion7(),
            RequesterId = Yo(u),
            SupplierId = proveedor.Id,
            Supplier = proveedor,
            Status = EstadoRequisicion.Borrador,
            Notes = string.IsNullOrWhiteSpace(e.Notas) ? null : e.Notas.Trim(),
            UpdatedAt = ahora,
        };
        foreach (var renglon in e.Renglones)
        {
            r.RequisitionLines.Add(new RequisitionLine
            {
                Id = Guid.CreateVersion7(), ProductId = renglon.ProductoId, Quantity = renglon.Cantidad, UnitPrice = productos[renglon.ProductoId].UnitPrice,
            });
        }
        r.Total = r.CalcularTotal();
        db.Requisitions.Add(r);
        Historial.Registrar(db, r.RequesterId, "creada", r.Id, new { total = r.Total, renglones = r.RequisitionLines.Count }, ahora);
        if (e.Enviar)
        {
            r.Enviar(ahora);
            Historial.Registrar(db, r.RequesterId, "enviada", r.Id, new { total = r.Total, firmas = r.AprobacionesNecesarias }, ahora);
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/requisiciones/{r.Id}", await ADto(db, r, ct));
    }

    private static Task<Results<Ok<RequisicionDto>, NotFound>> Enviar(Guid id, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct) =>
        Accion(id, u, db, reloj, ct, (r, yo, ahora) =>
        {
            if (r.RequesterId != yo) throw new ReglaDeNegocioException("no_es_tuya", "Solo quien pidió la requisición puede enviarla.");
            r.Enviar(ahora);
            Historial.Registrar(db, yo, "enviada", r.Id, new { total = r.Total, firmas = r.AprobacionesNecesarias }, ahora);
        });

    private static Task<Results<Ok<RequisicionDto>, NotFound>> Cancelar(Guid id, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct) =>
        Accion(id, u, db, reloj, ct, (r, yo, ahora) =>
        {
            r.Cancelar(yo);
            Historial.Registrar(db, yo, "cancelada", r.Id, null, ahora);
        });

    private static Task<Results<Ok<RequisicionDto>, NotFound>> Aprobar(Guid id, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct) =>
        Accion(id, u, db, reloj, ct, (r, yo, ahora) =>
        {
            var quedoAprobada = r.Aprobar(yo, ahora);
            Historial.Registrar(db, yo, "aprobada_por", r.Id, new { faltan = r.AprobacionesFaltantes }, ahora);
            if (quedoAprobada)
            {
                // Caso 5: el evento entra al Outbox en la MISMA transacción que la aprobación.
                Outbox.Agregar(db, TiposOutbox.RequisicionAprobada, new { requisicionId = r.Id }, ahora);
            }
        });

    private static Task<Results<Ok<RequisicionDto>, NotFound>> Rechazar(Guid id, RechazoEntrada e, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct) =>
        Accion(id, u, db, reloj, ct, (r, yo, ahora) =>
        {
            r.Rechazar(yo, e.Motivo, ahora);
            Historial.Registrar(db, yo, "rechazada", r.Id, new { motivo = r.RejectionReason }, ahora);
        });

    private static async Task<Results<Ok<RequisicionDto>, NotFound>> Accion(
        Guid id, ClaimsPrincipal u, RequiDbContext db, TimeProvider reloj, CancellationToken ct, Action<Requisition, Guid, DateTime> hacer)
    {
        var r = await ConTodo(db).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (r is null) return TypedResults.NotFound();
        var ahora = reloj.GetUtcNow().UtcDateTime;
        hacer(r, Yo(u), ahora); // las reglas del dominio lanzan ReglaDeNegocioException → 409
        r.UpdatedAt = ahora;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(await ADto(db, r, ct));
    }
}

/// <summary>Caso 7: cada acción guarda quién, qué y cuándo. La base impide editar o borrar (trigger).</summary>
public static class Historial
{
    public static void Registrar(RequiDbContext db, Guid actor, string accion, Guid entidad, object? datos, DateTime ahora) =>
        db.AuditEvents.Add(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = ahora,
            ActorId = actor,
            Action = accion,
            EntityType = "requisition",
            EntityId = entidad,
            Data = datos is null ? null : JsonSerializer.Serialize(datos),
        });
}

public static class TiposOutbox
{
    public const string RequisicionAprobada = "RequisicionAprobada";
    public const string OrdenGenerada = "OrdenGenerada";
}

public static class Outbox
{
    public static void Agregar(RequiDbContext db, string tipo, object payload, DateTime ahora) =>
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = Guid.CreateVersion7(),
            Type = tipo,
            Payload = JsonSerializer.Serialize(payload),
            OccurredAt = ahora,
            Status = "Pending",
            NextAttemptAt = ahora,
        });
}
