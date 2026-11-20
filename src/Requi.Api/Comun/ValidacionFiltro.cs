using FluentValidation;

namespace Requi.Api.Comun;

/// <summary>Valida con FluentValidation el argumento del endpoint de tipo T; si falla, 400 con los errores por campo.</summary>
public sealed class ValidacionFiltro<T>(IValidator<T> validador) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext contexto, EndpointFilterDelegate siguiente)
    {
        var dto = contexto.Arguments.OfType<T>().FirstOrDefault();
        if (dto is null) return TypedResults.Problem("Falta el cuerpo de la petición.", statusCode: StatusCodes.Status400BadRequest);

        var resultado = await validador.ValidateAsync(dto, contexto.HttpContext.RequestAborted);
        if (!resultado.IsValid) return TypedResults.ValidationProblem(resultado.ToDictionary());
        return await siguiente(contexto);
    }
}

public static class ValidacionExtensiones
{
    public static RouteHandlerBuilder Validar<T>(this RouteHandlerBuilder ruta) =>
        ruta.AddEndpointFilter<ValidacionFiltro<T>>().ProducesValidationProblem();
}
