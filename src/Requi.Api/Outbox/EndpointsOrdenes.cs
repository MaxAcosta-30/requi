using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Requi.Api.Identidad;
using Requi.Infrastructure.Identity;
using Requi.Infrastructure.Persistence;

namespace Requi.Api.Outbox;

/// <summary>Casos 5, 6 y 7: órdenes, bandeja del proveedor, mensajes fallidos e historial.</summary>
public static class EndpointsOrdenes
{
    public record OrdenDto(Guid Id, long Folio, Guid RequisicionId, Guid ProveedorId, string Proveedor, decimal Total, DateTime CreadaEn);
    public record AvisoDto(Guid Id, Guid ProveedorId, string Proveedor, Guid OrdenId, string Asunto, string Cuerpo, DateTime CreadoEn);
    public record MensajeDto(Guid Id, string Tipo, string Estado, int Intentos, string? UltimoError, DateTime OcurrioEn, DateTime? ProcesadoEn);
    public record EventoDto(DateTime Fecha, Guid ActorId, string Accion, string? Datos);

    public static WebApplication MapOrdenes(this WebApplication app)
    {
        var compras = app.MapGroup("/api").WithTags("Compras").RequireAuthorization(Roles.Compras);
        compras.MapGet("/ordenes", Ordenes).WithName("ListarOrdenes");
        compras.MapGet("/avisos-proveedor", Avisos).WithName("ListarAvisosProveedor");
        compras.MapGet("/outbox", Mensajes).WithName("ListarMensajesOutbox");

        app.MapGet("/api/requisiciones/{id:guid}/historial", Historial)
            .WithTags("Requisiciones").WithName("HistorialRequisicion")
            .RequireAuthorization(IdentidadServicios.PoliticaApi);
        return app;
    }

    private static async Task<Ok<List<OrdenDto>>> Ordenes(RequiDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.PurchaseOrders.AsNoTracking().OrderByDescending(o => o.Number).Take(200)
            .Select(o => new OrdenDto(o.Id, o.Number, o.RequisitionId, o.SupplierId, o.Supplier.Name, o.Total, o.CreatedAt))
            .ToListAsync(ct));

    /// <summary>La bandeja del proveedor (no tiene cuenta): lo que se le habría enviado.</summary>
    private static async Task<Ok<List<AvisoDto>>> Avisos(RequiDbContext db, Guid? proveedorId, CancellationToken ct) =>
        TypedResults.Ok(await db.SupplierNotifications.AsNoTracking()
            .Where(n => proveedorId == null || n.SupplierId == proveedorId)
            .OrderByDescending(n => n.CreatedAt).Take(200)
            .Select(n => new AvisoDto(n.Id, n.SupplierId, n.Supplier.Name, n.PurchaseOrderId, n.Subject, n.Body, n.CreatedAt))
            .ToListAsync(ct));

    /// <summary>Caso 6: los mensajes que fallaron 5 veces quedan visibles aquí (?estado=Failed).</summary>
    private static async Task<Ok<List<MensajeDto>>> Mensajes(RequiDbContext db, string? estado, CancellationToken ct) =>
        TypedResults.Ok(await db.OutboxMessages.AsNoTracking()
            .Where(m => estado == null || m.Status == estado)
            .OrderByDescending(m => m.OccurredAt).Take(200)
            .Select(m => new MensajeDto(m.Id, m.Type, m.Status, m.Attempts, m.LastError, m.OccurredAt, m.ProcessedAt))
            .ToListAsync(ct));

    /// <summary>Caso 7: solo lectura. No hay endpoints para editar ni borrar, y la base lo impide con un trigger.</summary>
    private static async Task<Results<Ok<List<EventoDto>>, NotFound>> Historial(Guid id, ClaimsPrincipal u, RequiDbContext db, CancellationToken ct)
    {
        var yo = Guid.Parse(u.FindFirstValue(OpenIddictConstants.Claims.Subject)!);
        var r = await db.Requisitions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null || !(r.RequesterId == yo || u.IsInRole(Roles.Aprobador) || u.IsInRole(Roles.Compras)))
        {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(await db.AuditEvents.AsNoTracking()
            .Where(e => e.EntityType == "requisition" && e.EntityId == id)
            .OrderBy(e => e.Seq) // consecutivo de la base: orden real aunque compartan la hora
            .Select(e => new EventoDto(e.OccurredAt, e.ActorId, e.Action, e.Data))
            .ToListAsync(ct));
    }
}
