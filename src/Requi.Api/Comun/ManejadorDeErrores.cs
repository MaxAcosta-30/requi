using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Requi.Domain.Reglas;

namespace Requi.Api.Comun;

/// <summary>
/// Convierte excepciones en ProblemDetails. Una regla de negocio rota o un duplicado son 409;
/// todo lo demás es 500 genérico: nunca se exponen trazas ni mensajes internos.
/// </summary>
public sealed class ManejadorDeErrores(IProblemDetailsService problemas, ILogger<ManejadorDeErrores> log) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception error, CancellationToken ct)
    {
        var (estado, titulo, detalle, codigo) = error switch
        {
            ReglaDeNegocioException r => (409, "Regla de negocio", r.Message, r.Codigo),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (409, "Ya existe", "Ya existe un registro con esos datos.", "duplicado"),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } =>
                (409, "Referencia inválida", "Uno de los registros relacionados no existe.", "referencia"),
            _ => (500, "Error interno", "Ocurrió un error inesperado.", "interno"),
        };
        if (estado == 500) log.LogError(error, "Error no controlado en {Ruta}", http.Request.Path);

        http.Response.StatusCode = estado;
        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = error,
            ProblemDetails = new ProblemDetails
            {
                Status = estado,
                Title = titulo,
                Detail = detalle,
                Extensions = { ["codigo"] = codigo },
            },
        });
    }
}
