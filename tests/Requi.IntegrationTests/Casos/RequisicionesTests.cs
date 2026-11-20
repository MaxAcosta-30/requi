using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Requi.Api.Outbox;
using Requi.Infrastructure.Identity;
using Requi.Infrastructure.Persistence;
using Requi.IntegrationTests.Identidad;
using static Requi.Api.Catalogo.EndpointsCatalogo;
using static Requi.Api.Outbox.EndpointsOrdenes;
using static Requi.Api.Requisiciones.EndpointsRequisiciones;

namespace Requi.IntegrationTests.Casos;

/// <summary>Casos 3 a 7 de punta a punta, con PostgreSQL real y el Outbox procesado a mano.</summary>
[Collection(ApiCompartida.Nombre)]
public class RequisicionesTests(ApiConBase api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private ProcesadorOutbox Procesador => api.Services.GetRequiredService<ProcesadorOutbox>();

    private async Task<T> ConDb<T>(Func<RequiDbContext, Task<T>> f)
    {
        await using var scope = api.Services.CreateAsyncScope();
        return await f(scope.ServiceProvider.GetRequiredService<RequiDbContext>());
    }

    private Task<int> MensajesDe(Guid requisicion, string? estado = null) => ConDb(db => db.Database
        .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM outbox_messages WHERE payload->>'requisicionId' = {requisicion.ToString()} AND ({estado}::text IS NULL OR status = {estado}::text)")
        .SingleAsync(Ct));

    /// <summary>Un proveedor con dos productos: $100.00 y $15,000.00.</summary>
    private async Task<(Guid Proveedor, Guid Barato, Guid Caro)> Catalogo()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var proveedor = await (await compras.PostAsJsonAsync("/api/proveedores",
            new ProveedorEntrada($"Prov-{Guid.NewGuid():N}"[..20], "p@prov.test", null), Ct)).LeerAsync<ProveedorDto>();
        async Task<Guid> Producto(decimal precio) => (await (await compras.PostAsJsonAsync("/api/productos",
            new ProductoEntrada(proveedor.Id, $"P{Guid.NewGuid():N}"[..12], "Producto", "pza", precio), Ct)).LeerAsync<ProductoDto>()).Id;
        return (proveedor.Id, await Producto(100m), await Producto(15_000m));
    }

    private static async Task<RequisicionDto> Crear(HttpClient solicitante, Guid proveedor, params (Guid Producto, int Cantidad)[] renglones) =>
        await (await solicitante.PostAsJsonAsync("/api/requisiciones",
            new RequisicionEntrada(proveedor, "Para la línea 3", [.. renglones.Select(r => new RenglonEntrada(r.Producto, r.Cantidad))], Enviar: true), Ct))
            .LeerAsync<RequisicionDto>();

    private static Task<HttpResponseMessage> Aprobar(HttpClient aprobador, Guid id) =>
        aprobador.PostAsync($"/api/requisiciones/{id}/aprobar", null, Ct);

    [Fact]
    public async Task Flujo_completo_hasta_el_aviso_al_proveedor()
    {
        var (proveedor, barato, _) = await Catalogo();
        var solicitante = await Sesiones.ComoAsync(api, Roles.Solicitante);
        var aprobador = await Sesiones.ComoAsync(api, Roles.Aprobador);
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);

        var r = await Crear(solicitante, proveedor, (barato, 3));
        Assert.Equal(("Submitted", 300.00m, 1), (r.Estado, r.Total, r.FirmasNecesarias)); // el total lo calcula el servidor

        var aprobada = await (await Aprobar(aprobador, r.Id)).LeerAsync<RequisicionDto>();
        Assert.Equal("Approved", aprobada.Estado);
        // El evento quedó en el Outbox en la misma transacción
        Assert.Equal(1, await MensajesDe(r.Id, "Pending"));

        await Procesador.ProcesarPendientesAsync(Ct);

        var final = await (await solicitante.GetAsync($"/api/requisiciones/{r.Id}", Ct)).LeerAsync<RequisicionDto>();
        Assert.Equal("OrderGenerated", final.Estado);
        var orden = Assert.Single(await (await compras.GetAsync("/api/ordenes", Ct)).LeerAsync<List<OrdenDto>>(), o => o.RequisicionId == r.Id);
        Assert.Equal(300.00m, orden.Total);
        var aviso = Assert.Single(await (await compras.GetAsync($"/api/avisos-proveedor?proveedorId={proveedor}", Ct)).LeerAsync<List<AvisoDto>>());
        Assert.Equal(orden.Id, aviso.OrdenId);
        Assert.Contains($"OC-{orden.Folio:D6}", aviso.Asunto);

        var historial = await (await solicitante.GetAsync($"/api/requisiciones/{r.Id}/historial", Ct)).LeerAsync<List<EventoDto>>();
        Assert.Equal(["creada", "enviada", "aprobada_por", "orden_generada"], historial.Select(e => e.Accion));
    }

    [Fact]
    public async Task Arriba_de_20000_se_necesitan_dos_aprobadores_distintos()
    {
        var (proveedor, _, caro) = await Catalogo();
        var r = await Crear(await Sesiones.ComoAsync(api, Roles.Solicitante), proveedor, (caro, 2)); // $30,000
        Assert.Equal(2, r.FirmasNecesarias);

        var primera = await (await Aprobar(await Sesiones.ComoAsync(api, Roles.Aprobador), r.Id)).LeerAsync<RequisicionDto>();
        Assert.Equal("Submitted", primera.Estado);
        Assert.Equal(0, await MensajesDe(r.Id));

        var segunda = await (await Aprobar(await Sesiones.ComoCorreoAsync(api, ApiConBase.Aprobador2), r.Id)).LeerAsync<RequisicionDto>();
        Assert.Equal("Approved", segunda.Estado);
        Assert.Equal(2, segunda.Firmas.Select(f => f.AprobadorId).Distinct().Count());
    }

    [Fact]
    public async Task Las_reglas_rotas_responden_409_con_su_codigo()
    {
        var (proveedor, barato, _) = await Catalogo();
        var mixto = await Sesiones.ComoCorreoAsync(api, ApiConBase.Mixto); // Solicitante y Aprobador a la vez
        var propia = await Crear(mixto, proveedor, (barato, 1));

        var r1 = await Aprobar(mixto, propia.Id);
        Assert.Equal(HttpStatusCode.Conflict, r1.StatusCode);
        Assert.Contains("\"codigo\":\"propia\"", await r1.Content.ReadAsStringAsync(Ct));

        var aprobador = await Sesiones.ComoAsync(api, Roles.Aprobador);
        await aprobador.PostAsJsonAsync($"/api/requisiciones/{propia.Id}/rechazar", new RechazoEntrada("Sin presupuesto"), Ct);
        var r2 = await Aprobar(await Sesiones.ComoCorreoAsync(api, ApiConBase.Aprobador2), propia.Id);
        Assert.Equal(HttpStatusCode.Conflict, r2.StatusCode);
        Assert.Contains("transicion_invalida", await r2.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Rechazar_sin_motivo_es_400_y_con_motivo_queda_registrado()
    {
        var (proveedor, barato, _) = await Catalogo();
        var r = await Crear(await Sesiones.ComoAsync(api, Roles.Solicitante), proveedor, (barato, 1));
        var aprobador = await Sesiones.ComoAsync(api, Roles.Aprobador);

        var sinMotivo = await aprobador.PostAsJsonAsync($"/api/requisiciones/{r.Id}/rechazar", new RechazoEntrada("  "), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, sinMotivo.StatusCode);

        var rechazada = await (await aprobador.PostAsJsonAsync($"/api/requisiciones/{r.Id}/rechazar",
            new RechazoEntrada("Falta la cotización"), Ct)).LeerAsync<RequisicionDto>();
        Assert.Equal(("Rejected", "Falta la cotización"), (rechazada.Estado, rechazada.MotivoRechazo));
    }

    [Fact]
    public async Task Un_producto_de_otro_proveedor_no_entra_y_el_precio_no_lo_pone_el_cliente()
    {
        var (proveedor, _, _) = await Catalogo();
        var (_, ajeno, _) = await Catalogo();
        var solicitante = await Sesiones.ComoAsync(api, Roles.Solicitante);

        var r = await solicitante.PostAsJsonAsync("/api/requisiciones",
            new RequisicionEntrada(proveedor, null, [new RenglonEntrada(ajeno, 1)], true), Ct);

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("producto_invalido", await r.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Otro_solicitante_no_ve_ni_cancela_lo_ajeno()
    {
        var (proveedor, barato, _) = await Catalogo();
        var r = await Crear(await Sesiones.ComoAsync(api, Roles.Solicitante), proveedor, (barato, 1));
        var otro = await Sesiones.ComoCorreoAsync(api, ApiConBase.Solicitante2);

        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/requisiciones/{r.Id}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otro.GetAsync($"/api/requisiciones/{r.Id}/historial", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await otro.PostAsync($"/api/requisiciones/{r.Id}/cancelar", null, Ct)).StatusCode);
        Assert.DoesNotContain(await (await otro.GetAsync("/api/requisiciones", Ct)).LeerAsync<List<RequisicionDto>>(), x => x.Id == r.Id);
        Assert.Equal(HttpStatusCode.Forbidden, (await otro.GetAsync("/api/requisiciones?vista=todas", Ct)).StatusCode);
    }

    [Fact]
    public async Task Procesar_el_mismo_mensaje_dos_veces_no_duplica_la_orden_ni_el_aviso()
    {
        var (proveedor, barato, _) = await Catalogo();
        var r = await Crear(await Sesiones.ComoAsync(api, Roles.Solicitante), proveedor, (barato, 2));
        await Aprobar(await Sesiones.ComoAsync(api, Roles.Aprobador), r.Id);
        await Procesador.ProcesarPendientesAsync(Ct);

        // Simula que la API se cayó antes de marcar los mensajes como procesados: vuelven a Pending
        await ConDb(db => db.Database.ExecuteSqlAsync(
            $"UPDATE outbox_messages SET status = 'Pending', processed_at = NULL WHERE payload::text LIKE {"%" + r.Id + "%"} OR type = 'OrdenGenerada'", Ct));
        await Procesador.ProcesarPendientesAsync(Ct);

        Assert.Equal(1, await ConDb(db => db.PurchaseOrders.CountAsync(o => o.RequisitionId == r.Id, Ct)));
        Assert.Equal(1, await ConDb(db => db.SupplierNotifications.CountAsync(n => n.SupplierId == proveedor, Ct)));
    }

    [Fact]
    public async Task Un_mensaje_que_falla_se_reintenta_y_al_quinto_queda_fallido_y_visible()
    {
        var id = Guid.CreateVersion7();
        await ConDb(db => db.Database.ExecuteSqlAsync(
            $"INSERT INTO outbox_messages (id, type, payload) VALUES ({id}, 'RequisicionAprobada', {"{\"requisicionId\":\"" + Guid.NewGuid() + "\"}"}::jsonb)", Ct));

        for (var intento = 1; intento <= ProcesadorOutbox.MaxIntentos; intento++)
        {
            await Procesador.ProcesarPendientesAsync(Ct);
            var m = await ConDb(db => db.OutboxMessages.AsNoTracking().FirstAsync(x => x.Id == id, Ct));
            Assert.Equal(intento, m.Attempts);
            Assert.Equal(intento < ProcesadorOutbox.MaxIntentos ? "Pending" : "Failed", m.Status);
            // Adelanta el reloj del mensaje en lugar de esperar la espera creciente
            await ConDb(db => db.Database.ExecuteSqlAsync($"UPDATE outbox_messages SET next_attempt_at = now() - interval '1 second' WHERE id = {id}", Ct));
        }

        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var fallidos = await (await compras.GetAsync("/api/outbox?estado=Failed", Ct)).LeerAsync<List<MensajeDto>>();
        Assert.Contains(fallidos, f => f.Id == id && f.UltimoError != null);
    }

    [Fact]
    public async Task Dos_procesadores_a_la_vez_no_toman_el_mismo_mensaje()
    {
        var (proveedor, barato, _) = await Catalogo();
        var solicitante = await Sesiones.ComoAsync(api, Roles.Solicitante);
        var aprobador = await Sesiones.ComoAsync(api, Roles.Aprobador);
        var ids = new List<Guid>();
        for (var i = 0; i < 4; i++)
        {
            var r = await Crear(solicitante, proveedor, (barato, i + 1));
            await Aprobar(aprobador, r.Id);
            ids.Add(r.Id);
        }

        await Task.WhenAll(Procesador.ProcesarPendientesAsync(Ct), Procesador.ProcesarPendientesAsync(Ct), Procesador.ProcesarPendientesAsync(Ct));
        await Procesador.ProcesarPendientesAsync(Ct);

        Assert.Equal(4, await ConDb(db => db.PurchaseOrders.CountAsync(o => ids.Contains(o.RequisitionId), Ct)));
        Assert.Equal(4, await ConDb(db => db.SupplierNotifications.CountAsync(n => n.SupplierId == proveedor, Ct)));
    }

    [Fact]
    public async Task El_historial_no_se_puede_editar_ni_borrar_ni_desde_la_base()
    {
        var (proveedor, barato, _) = await Catalogo();
        var r = await Crear(await Sesiones.ComoAsync(api, Roles.Solicitante), proveedor, (barato, 1));

        var editar = await Assert.ThrowsAnyAsync<Exception>(() => ConDb(db => db.Database.ExecuteSqlAsync(
            $"UPDATE audit_events SET action = 'otra' WHERE entity_id = {r.Id}", Ct)));
        Assert.Contains("audit_events no admite UPDATE", editar.Message);
        await Assert.ThrowsAnyAsync<Exception>(() => ConDb(db => db.Database.ExecuteSqlAsync(
            $"DELETE FROM audit_events WHERE entity_id = {r.Id}", Ct)));
    }
}
