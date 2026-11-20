using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Requi.Api.Requisiciones;
using Requi.Domain.Entities;
using Requi.Domain.Reglas;
using Requi.Infrastructure.Persistence;

namespace Requi.Api.Outbox;

/// <summary>Un manejador por tipo de mensaje. Debe ser idempotente: puede recibir el mismo mensaje dos veces.</summary>
public interface IManejadorOutbox
{
    string Tipo { get; }
    Task ManejarAsync(OutboxMessage mensaje, RequiDbContext db, DateTime ahora, CancellationToken ct);
}

/// <summary>
/// Casos 5 y 6. Procesa un mensaje por transacción, tomado con FOR UPDATE SKIP LOCKED para que
/// dos instancias nunca tomen el mismo. Si el manejador falla, todo lo que hizo se deshace y el
/// mensaje se reintenta con espera creciente (5, 10, 20, 40 s); al quinto fallo queda Failed.
/// Al arrancar procesa lo pendiente: nada se pierde aunque Render apague la API.
/// </summary>
public sealed class ProcesadorOutbox(
    IServiceScopeFactory scopes, TimeProvider reloj, IConfiguration config, ILogger<ProcesadorOutbox> log) : BackgroundService
{
    public const int MaxIntentos = 5;
    public static readonly TimeSpan Intervalo = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!config.GetValue("Outbox:Activo", true)) return; // las pruebas lo llaman a mano
        using var temporizador = new PeriodicTimer(Intervalo, reloj);
        do
        {
            try
            {
                await ProcesarPendientesAsync(ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                log.LogError(e, "El procesador del Outbox falló; se reintenta en {Segundos} s", Intervalo.TotalSeconds);
            }
        }
        while (await temporizador.WaitForNextTickAsync(ct));
    }

    /// <summary>Procesa hasta <paramref name="maximo"/> mensajes listos. Devuelve cuántos tomó.</summary>
    public async Task<int> ProcesarPendientesAsync(CancellationToken ct, int maximo = 20)
    {
        var tomados = 0;
        while (tomados < maximo && await ProcesarUnoAsync(ct)) tomados++;
        return tomados;
    }

    private async Task<bool> ProcesarUnoAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RequiDbContext>();
        var manejadores = scope.ServiceProvider.GetServices<IManejadorOutbox>().ToDictionary(m => m.Tipo);
        var ahora = reloj.GetUtcNow().UtcDateTime;

        // Con EnableRetryOnFailure, una transacción manual debe correr dentro de la estrategia de
        // ejecución: si la conexión se cae, se repite el bloque completo (la transacción se deshizo).
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        db.ChangeTracker.Clear();
        Guid id;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var mensaje = (await db.OutboxMessages
                .FromSql($"""
                    SELECT * FROM outbox_messages
                    WHERE status = 'Pending' AND next_attempt_at <= {ahora}
                    ORDER BY next_attempt_at
                    LIMIT 1
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct)).FirstOrDefault();
            if (mensaje is null) return false;
            id = mensaje.Id;

            try
            {
                if (!manejadores.TryGetValue(mensaje.Type, out var manejador))
                {
                    throw new InvalidOperationException($"No hay manejador para {mensaje.Type}");
                }
                await manejador.ManejarAsync(mensaje, db, ahora, ct);
                mensaje.Status = "Processed";
                mensaje.ProcessedAt = ahora;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return true;
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                await tx.RollbackAsync(ct); // se deshace TODO lo que hizo el manejador
                log.LogWarning("Mensaje {Id} ({Tipo}) falló: {Error}", mensaje.Id, mensaje.Type, e.GetType().Name);
                db.ChangeTracker.Clear();
                await RegistrarFalloAsync(db, id, e, ahora, ct);
                return true;
            }
        }
        });
    }

    private static async Task RegistrarFalloAsync(RequiDbContext db, Guid id, Exception error, DateTime ahora, CancellationToken ct)
    {
        var m = await db.OutboxMessages.FirstAsync(x => x.Id == id, ct);
        m.Attempts++;
        var texto = $"{error.GetType().Name}: {error.Message}";
        m.LastError = texto.Length > 500 ? texto[..500] : texto;
        if (m.Attempts >= MaxIntentos)
        {
            m.Status = "Failed";
        }
        else
        {
            m.NextAttemptAt = ahora + Espera(m.Attempts);
        }
        await db.SaveChangesAsync(ct);
    }

    public static TimeSpan Espera(int intentos) => TimeSpan.FromSeconds(5 * Math.Pow(2, intentos - 1));
}

/// <summary>Caso 5: crea la orden de compra de una requisición aprobada, una sola vez.</summary>
public sealed class CrearOrdenDeCompra : IManejadorOutbox
{
    public string Tipo => TiposOutbox.RequisicionAprobada;

    public async Task ManejarAsync(OutboxMessage mensaje, RequiDbContext db, DateTime ahora, CancellationToken ct)
    {
        var requisicionId = JsonDocument.Parse(mensaje.Payload).RootElement.GetProperty("requisicionId").GetGuid();
        // Idempotencia: si la orden ya existe (el mensaje se procesó antes y algo falló después), no se repite.
        // La restricción única uq_purchase_orders_requisition es la segunda red de seguridad.
        if (await db.PurchaseOrders.AnyAsync(o => o.RequisitionId == requisicionId, ct)) return;

        var r = await db.Requisitions.Include(x => x.RequisitionLines).Include(x => x.Approvals)
            .FirstAsync(x => x.Id == requisicionId, ct);
        var orden = new PurchaseOrder
        {
            Id = Guid.CreateVersion7(),
            RequisitionId = r.Id,
            SupplierId = r.SupplierId,
            Total = r.Total,
            CreatedAt = ahora,
        };
        foreach (var l in r.RequisitionLines)
        {
            orden.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = Guid.CreateVersion7(), ProductId = l.ProductId, Quantity = l.Quantity, UnitPrice = l.UnitPrice });
        }
        db.PurchaseOrders.Add(orden);
        r.MarcarOrdenGenerada();
        r.UpdatedAt = ahora;

        // El último aprobador es quien disparó la orden: queda como actor en el historial.
        var actor = r.Approvals.Where(a => a.Decision == DecisionAprobacion.Aprobada).MaxBy(a => a.DecidedAt)!.ApproverId;
        Historial.Registrar(db, actor, "orden_generada", r.Id, new { ordenId = orden.Id, total = orden.Total }, ahora);
        Requisiciones.Outbox.Agregar(db, TiposOutbox.OrdenGenerada, new { ordenId = orden.Id }, ahora);
    }
}

/// <summary>Caso 6: deja el aviso de la orden en la bandeja del proveedor, una sola vez.</summary>
public sealed class AvisarProveedor : IManejadorOutbox
{
    public string Tipo => TiposOutbox.OrdenGenerada;

    public async Task ManejarAsync(OutboxMessage mensaje, RequiDbContext db, DateTime ahora, CancellationToken ct)
    {
        var ordenId = JsonDocument.Parse(mensaje.Payload).RootElement.GetProperty("ordenId").GetGuid();
        if (await db.SupplierNotifications.AnyAsync(n => n.PurchaseOrderId == ordenId, ct)) return;

        var orden = await db.PurchaseOrders.Include(o => o.PurchaseOrderLines).FirstAsync(o => o.Id == ordenId, ct);
        var ids = orden.PurchaseOrderLines.Select(l => l.ProductId).ToList();
        var nombres = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name, ct);
        var renglones = string.Join("\n", orden.PurchaseOrderLines.Select(l => $"- {l.Quantity} × {nombres[l.ProductId]} a {l.UnitPrice:N2}"));

        db.SupplierNotifications.Add(new SupplierNotification
        {
            Id = Guid.CreateVersion7(),
            SupplierId = orden.SupplierId,
            PurchaseOrderId = orden.Id,
            Subject = $"Orden de compra OC-{orden.Number:D6}",
            Body = $"Le enviamos la orden OC-{orden.Number:D6} por un total de {orden.Total:N2} MXN.\n\n{renglones}",
            CreatedAt = ahora,
        });
    }
}
