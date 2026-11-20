using Requi.Domain.Entities;
using Requi.Domain.Reglas;

namespace Requi.UnitTests;

/// <summary>
/// Caso 4 y reglas de estado. Escritas antes que el código (TDD), como pide CLAUDE.md:
/// Borrador → Enviada → Aprobada → OrdenGenerada; Enviada → Rechazada;
/// Borrador o Enviada → Cancelada. Hasta $20,000 basta 1 aprobación; arriba, 2 de
/// aprobadores distintos; nadie aprueba lo suyo; rechazar exige motivo.
/// </summary>
public class ReglasDeRequisicionTests
{
    private static readonly Guid Solicitante = Guid.NewGuid();
    private static readonly Guid AprobadorA = Guid.NewGuid();
    private static readonly Guid AprobadorB = Guid.NewGuid();
    private static readonly DateTime Ahora = new(2026, 11, 19, 18, 0, 0, DateTimeKind.Utc);

    private static Requisition Nueva(params (int Cantidad, decimal Precio)[] renglones)
    {
        var r = new Requisition { Id = Guid.NewGuid(), RequesterId = Solicitante, Status = EstadoRequisicion.Borrador };
        foreach (var (cantidad, precio) in renglones)
        {
            r.RequisitionLines.Add(new RequisitionLine { Id = Guid.NewGuid(), ProductId = Guid.NewGuid(), Quantity = cantidad, UnitPrice = precio });
        }
        return r;
    }

    private static Requisition Enviada(decimal total)
    {
        var r = Nueva((1, total));
        r.Enviar(Ahora);
        return r;
    }

    private static void FallaCon(string codigo, Action accion)
    {
        var e = Assert.Throws<ReglaDeNegocioException>(accion);
        Assert.Equal(codigo, e.Codigo);
    }

    // ----- Total y envío -----

    [Fact]
    public void El_total_se_calcula_en_el_servidor_desde_los_renglones()
    {
        var r = Nueva((3, 125.50m), (2, 1_000m));
        r.Enviar(Ahora);
        Assert.Equal(2_376.50m, r.Total);
    }

    [Fact]
    public void Enviar_pasa_de_borrador_a_enviada_y_guarda_la_fecha()
    {
        var r = Nueva((1, 100m));
        r.Enviar(Ahora);
        Assert.Equal(EstadoRequisicion.Enviada, r.Status);
        Assert.Equal(Ahora, r.SubmittedAt);
    }

    [Fact]
    public void No_se_envia_una_requisicion_sin_renglones()
        => FallaCon("sin_renglones", () => Nueva().Enviar(Ahora));

    [Fact]
    public void No_se_envia_dos_veces()
        => FallaCon("transicion_invalida", () => Enviada(100m).Enviar(Ahora));

    // ----- Aprobación -----

    [Theory]
    [InlineData(1)]
    [InlineData(20_000)]
    public void Hasta_20000_basta_una_aprobacion(decimal total)
    {
        var r = Enviada(total);
        var quedoAprobada = r.Aprobar(AprobadorA, Ahora);
        Assert.True(quedoAprobada);
        Assert.Equal(EstadoRequisicion.Aprobada, r.Status);
        Assert.Single(r.Approvals);
    }

    [Fact]
    public void Arriba_de_20000_se_necesitan_dos_aprobadores_distintos()
    {
        var r = Enviada(20_000.01m);

        Assert.False(r.Aprobar(AprobadorA, Ahora));
        Assert.Equal(EstadoRequisicion.Enviada, r.Status);
        Assert.Equal(1, r.AprobacionesFaltantes);

        Assert.True(r.Aprobar(AprobadorB, Ahora));
        Assert.Equal(EstadoRequisicion.Aprobada, r.Status);
        Assert.Equal(0, r.AprobacionesFaltantes);
    }

    [Fact]
    public void El_mismo_aprobador_no_cuenta_dos_veces()
    {
        var r = Enviada(50_000m);
        r.Aprobar(AprobadorA, Ahora);
        FallaCon("ya_decidio", () => r.Aprobar(AprobadorA, Ahora));
        Assert.Equal(EstadoRequisicion.Enviada, r.Status);
    }

    [Fact]
    public void Nadie_aprueba_su_propia_requisicion()
        => FallaCon("propia", () => Enviada(100m).Aprobar(Solicitante, Ahora));

    [Fact]
    public void Un_borrador_no_se_aprueba()
        => FallaCon("transicion_invalida", () => Nueva((1, 100m)).Aprobar(AprobadorA, Ahora));

    [Fact]
    public void Una_aprobada_no_se_vuelve_a_aprobar()
    {
        var r = Enviada(100m);
        r.Aprobar(AprobadorA, Ahora);
        FallaCon("transicion_invalida", () => r.Aprobar(AprobadorB, Ahora));
    }

    // ----- Rechazo -----

    [Fact]
    public void Rechazar_exige_motivo_y_lo_guarda()
    {
        var r = Enviada(100m);
        r.Rechazar(AprobadorA, "  Falta cotización  ", Ahora);

        Assert.Equal(EstadoRequisicion.Rechazada, r.Status);
        Assert.Equal("Falta cotización", r.RejectionReason);
        var decision = Assert.Single(r.Approvals);
        Assert.Equal(DecisionAprobacion.Rechazada, decision.Decision);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rechazar_sin_motivo_no_se_permite(string? motivo)
        => FallaCon("motivo_requerido", () => Enviada(100m).Rechazar(AprobadorA, motivo!, Ahora));

    [Fact]
    public void Un_rechazo_tras_la_primera_de_dos_aprobaciones_rechaza_la_requisicion()
    {
        var r = Enviada(30_000m);
        r.Aprobar(AprobadorA, Ahora);
        r.Rechazar(AprobadorB, "Presupuesto agotado", Ahora);
        Assert.Equal(EstadoRequisicion.Rechazada, r.Status);
    }

    [Fact]
    public void Nadie_rechaza_su_propia_requisicion()
        => FallaCon("propia", () => Enviada(100m).Rechazar(Solicitante, "No", Ahora));

    // ----- Cancelación y orden -----

    [Fact]
    public void Se_cancela_desde_borrador_o_enviada()
    {
        var borrador = Nueva((1, 1m));
        borrador.Cancelar(Solicitante);
        Assert.Equal(EstadoRequisicion.Cancelada, borrador.Status);

        var enviada = Enviada(1m);
        enviada.Cancelar(Solicitante);
        Assert.Equal(EstadoRequisicion.Cancelada, enviada.Status);
    }

    [Fact]
    public void Solo_quien_la_pidio_la_cancela()
        => FallaCon("no_es_tuya", () => Enviada(1m).Cancelar(AprobadorA));

    [Fact]
    public void Una_aprobada_ya_no_se_cancela()
    {
        var r = Enviada(1m);
        r.Aprobar(AprobadorA, Ahora);
        FallaCon("transicion_invalida", () => r.Cancelar(Solicitante));
    }

    [Fact]
    public void La_orden_solo_se_genera_desde_aprobada_y_una_sola_vez()
    {
        var r = Enviada(1m);
        FallaCon("transicion_invalida", r.MarcarOrdenGenerada);

        r.Aprobar(AprobadorA, Ahora);
        r.MarcarOrdenGenerada();
        Assert.Equal(EstadoRequisicion.OrdenGenerada, r.Status);
        FallaCon("transicion_invalida", r.MarcarOrdenGenerada);
    }

    [Fact]
    public void Los_estados_coinciden_con_los_permitidos_por_la_base()
    {
        // El CHECK de 0001_inicial.sql usa estos textos en inglés; la interfaz los traduce.
        Assert.Equal(
            ["Draft", "Submitted", "Approved", "OrderGenerated", "Rejected", "Cancelled"],
            [EstadoRequisicion.Borrador, EstadoRequisicion.Enviada, EstadoRequisicion.Aprobada,
             EstadoRequisicion.OrdenGenerada, EstadoRequisicion.Rechazada, EstadoRequisicion.Cancelada]);
    }
}
