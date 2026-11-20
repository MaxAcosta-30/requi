using Requi.Domain.Reglas;

namespace Requi.Domain.Entities;

// Reglas de la requisición en una clase partial: regenerar el scaffold no las borra.
public partial class Requisition
{
    /// <summary>Hasta este monto basta una aprobación; arriba se necesitan dos.</summary>
    public const decimal LimiteUnaAprobacion = 20_000m;

    public int AprobacionesNecesarias => Total <= LimiteUnaAprobacion ? 1 : 2;

    public int AprobacionesFaltantes =>
        Math.Max(0, AprobacionesNecesarias - Approvals.Count(a => a.Decision == DecisionAprobacion.Aprobada));

    /// <summary>El total siempre se calcula aquí, nunca se toma del cliente.</summary>
    public decimal CalcularTotal() => RequisitionLines.Sum(l => l.Quantity * l.UnitPrice);

    public void Enviar(DateTime ahora)
    {
        ExigirEstado(EstadoRequisicion.Borrador, "enviar");
        if (RequisitionLines.Count == 0)
        {
            throw new ReglaDeNegocioException("sin_renglones", "La requisición no tiene renglones.");
        }
        Total = CalcularTotal();
        Status = EstadoRequisicion.Enviada;
        SubmittedAt = ahora;
    }

    /// <summary>Registra la aprobación y devuelve true si con ella quedó aprobada.</summary>
    public bool Aprobar(Guid aprobador, DateTime ahora)
    {
        ValidarDecision(aprobador);
        Approvals.Add(new Approval
        {
            // Sin Id: lo genera la base. Con un Id puesto, EF lo trataría como fila existente.
            RequisitionId = Id,
            ApproverId = aprobador,
            Decision = DecisionAprobacion.Aprobada,
            DecidedAt = ahora,
        });
        if (AprobacionesFaltantes > 0) return false;
        Status = EstadoRequisicion.Aprobada;
        return true;
    }

    public void Rechazar(Guid aprobador, string motivo, DateTime ahora)
    {
        ValidarDecision(aprobador);
        var limpio = motivo?.Trim();
        if (string.IsNullOrEmpty(limpio))
        {
            throw new ReglaDeNegocioException("motivo_requerido", "Rechazar exige un motivo.");
        }
        Approvals.Add(new Approval
        {
            // Sin Id: lo genera la base. Con un Id puesto, EF lo trataría como fila existente.
            RequisitionId = Id,
            ApproverId = aprobador,
            Decision = DecisionAprobacion.Rechazada,
            Reason = limpio,
            DecidedAt = ahora,
        });
        Status = EstadoRequisicion.Rechazada;
        RejectionReason = limpio;
    }

    public void Cancelar(Guid usuario)
    {
        if (Status is not (EstadoRequisicion.Borrador or EstadoRequisicion.Enviada))
        {
            throw Transicion("cancelar");
        }
        if (usuario != RequesterId)
        {
            throw new ReglaDeNegocioException("no_es_tuya", "Solo quien pidió la requisición puede cancelarla.");
        }
        Status = EstadoRequisicion.Cancelada;
    }

    public void MarcarOrdenGenerada()
    {
        ExigirEstado(EstadoRequisicion.Aprobada, "generar la orden de");
        Status = EstadoRequisicion.OrdenGenerada;
    }

    private void ValidarDecision(Guid aprobador)
    {
        ExigirEstado(EstadoRequisicion.Enviada, "decidir");
        if (aprobador == RequesterId)
        {
            throw new ReglaDeNegocioException("propia", "Nadie aprueba ni rechaza su propia requisición.");
        }
        if (Approvals.Any(a => a.ApproverId == aprobador))
        {
            throw new ReglaDeNegocioException("ya_decidio", "Este aprobador ya decidió sobre la requisición.");
        }
    }

    private void ExigirEstado(string esperado, string accion)
    {
        if (Status != esperado) throw Transicion(accion);
    }

    private ReglaDeNegocioException Transicion(string accion) =>
        new("transicion_invalida", $"No se puede {accion} una requisición en estado {Status}.");
}
