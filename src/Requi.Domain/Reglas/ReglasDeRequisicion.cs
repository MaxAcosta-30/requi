using Requi.Domain.Entities;

namespace Requi.Domain.Reglas;

/// <summary>Estados tal como los guarda la base (CHECK en 0001_inicial.sql).</summary>
public static class EstadoRequisicion
{
    public const string Borrador = "Draft";
    public const string Enviada = "Submitted";
    public const string Aprobada = "Approved";
    public const string OrdenGenerada = "OrderGenerated";
    public const string Rechazada = "Rejected";
    public const string Cancelada = "Cancelled";
}

public static class DecisionAprobacion
{
    public const string Aprobada = "Approved";
    public const string Rechazada = "Rejected";
}

/// <summary>Una regla del negocio impidió la acción. La API la responde como 409 con ProblemDetails.</summary>
public sealed class ReglaDeNegocioException(string codigo, string mensaje) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
}
