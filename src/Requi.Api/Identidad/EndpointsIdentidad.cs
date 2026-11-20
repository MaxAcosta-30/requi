using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using Requi.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Requi.Api.Identidad;

public static class EndpointsIdentidad
{
    private static readonly string[] ScopesPermitidos =
        [Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess, SemillaIdentidad.ScopeApi];

    public static WebApplication MapIdentidad(this WebApplication app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], Autorizar)
            .DisableAntiforgery()
            .ExcludeFromDescription();
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], CerrarSesion)
            .DisableAntiforgery()
            .ExcludeFromDescription();

        app.MapGet("/cuenta/login", PaginaLogin).ExcludeFromDescription();
        app.MapPost("/cuenta/login", IniciarSesion).ExcludeFromDescription();

        app.MapGet("/api/sesion", Sesion)
            .RequireAuthorization(IdentidadServicios.PoliticaApi)
            .WithName("ObtenerSesion")
            .WithSummary("Usuario y roles del access token actual.");
        return app;
    }

    public record SesionDto(Guid Id, string Nombre, string Correo, string[] Roles);

    private static Ok<SesionDto> Sesion(ClaimsPrincipal usuario) => TypedResults.Ok(new SesionDto(
        Guid.Parse(usuario.FindFirstValue(Claims.Subject)!),
        usuario.FindFirstValue(Claims.Name) ?? "",
        usuario.FindFirstValue(Claims.Email) ?? "",
        [.. usuario.FindAll(Claims.Role).Select(c => c.Value).Order()]));

    private static async Task<IResult> Autorizar(
        HttpContext http, UserManager<Usuario> usuarios, SignInManager<Usuario> sesiones)
    {
        var solicitud = http.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("No es una solicitud OpenID Connect.");

        var cookie = await http.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (!cookie.Succeeded)
        {
            // Sin sesión: a la página de login, que regresa aquí con los mismos parámetros.
            var parametros = http.Request.HasFormContentType ? http.Request.Form.ToList() : http.Request.Query.ToList();
            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = http.Request.PathBase + http.Request.Path + QueryString.Create(parametros) },
                [IdentityConstants.ApplicationScheme]);
        }

        var usuario = await usuarios.GetUserAsync(cookie.Principal);
        if (usuario is null || !await sesiones.CanSignInAsync(usuario) || await usuarios.IsLockedOutAsync(usuario))
        {
            return Rechazar(Errors.LoginRequired, "La cuenta no puede iniciar sesión.");
        }

        var identidad = new ClaimsIdentity(
            TokenValidationParameters.DefaultAuthenticationType, Claims.Name, Claims.Role);
        identidad.SetClaim(Claims.Subject, usuario.Id.ToString())
            .SetClaim(Claims.Name, usuario.NombreCompleto.Length > 0 ? usuario.NombreCompleto : usuario.UserName)
            .SetClaim(Claims.Email, usuario.Email)
            .SetClaims(Claims.Role, [.. await usuarios.GetRolesAsync(usuario)]);

        // Sin SetScopes no se emite refresh token aunque se pida offline_access (error de FortressID).
        identidad.SetScopes(solicitud.GetScopes().Intersect(ScopesPermitidos));
        identidad.SetResources(SemillaIdentidad.ScopeApi);
        identidad.SetDestinations(Destinos);

        return Results.SignIn(new ClaimsPrincipal(identidad), properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static IEnumerable<string> Destinos(Claim claim) => claim.Type switch
    {
        Claims.Name or Claims.Email or Claims.Role when claim.Subject?.HasScope(Scopes.Profile) == true
            => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Subject => [Destinations.AccessToken, Destinations.IdentityToken],
        "AspNet.Identity.SecurityStamp" => [], // nunca sale en un token
        _ => [Destinations.AccessToken],
    };

    private static IResult Rechazar(string error, string descripcion) => Results.Forbid(
        new AuthenticationProperties(new Dictionary<string, string?>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = error,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = descripcion,
        }),
        [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);

    private static async Task<IResult> CerrarSesion(SignInManager<Usuario> sesiones)
    {
        await sesiones.SignOutAsync();
        // OpenIddict valida post_logout_redirect_uri contra las registradas del cliente.
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static IResult PaginaLogin(HttpContext http, IAntiforgery antiforgery, string? returnUrl, string? error)
        => Results.Content(HtmlLogin(antiforgery.GetAndStoreTokens(http), returnUrl, error), "text/html; charset=utf-8");

    private static async Task<IResult> IniciarSesion(
        HttpContext http,
        IAntiforgery antiforgery,
        SignInManager<Usuario> sesiones,
        UserManager<Usuario> usuarios,
        [FromForm] string correo,
        [FromForm] string clave,
        [FromForm] string? returnUrl)
    {
        var usuario = await usuarios.FindByEmailAsync(correo.Trim());
        var resultado = usuario is null
            ? Microsoft.AspNetCore.Identity.SignInResult.Failed
            : await sesiones.PasswordSignInAsync(usuario, clave, isPersistent: false, lockoutOnFailure: true);

        if (!resultado.Succeeded)
        {
            // Mismo mensaje exista o no la cuenta: no se revela quién está registrado.
            var mensaje = resultado.IsLockedOut
                ? "Demasiados intentos. Espera unos minutos."
                : "Correo o contraseña incorrectos.";
            return Results.Content(HtmlLogin(antiforgery.GetAndStoreTokens(http), returnUrl, mensaje),
                "text/html; charset=utf-8", statusCode: StatusCodes.Status401Unauthorized);
        }
        return Results.LocalRedirect(EsLocal(returnUrl) ? returnUrl! : "/");
    }

    // Evita redirecciones abiertas: solo rutas de esta misma API.
    public static bool EsLocal(string? url) =>
        !string.IsNullOrEmpty(url) && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\'));

    // Escapa < > & " pero deja el español legible (Default convierte "ñ" en &#xF1;).
    private static readonly HtmlEncoder Codificador = HtmlEncoder.Create(UnicodeRanges.All);

    private static string HtmlLogin(AntiforgeryTokenSet tokens, string? returnUrl, string? error)
    {
        var e = Codificador;
        var aviso = string.IsNullOrEmpty(error) ? "" : $"""<p role="alert" class="error">{e.Encode(error)}</p>""";
        return $$"""
            <!doctype html>
            <html lang="es">
            <head>
            <meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Iniciar sesión · Requi</title>
            <style>
              body { margin: 0; min-height: 100vh; display: grid; place-items: center; font: 16px/1.5 system-ui, sans-serif; background: #f6f5f2; color: #16150f; }
              form { width: min(360px, calc(100% - 32px)); display: grid; gap: 14px; }
              h1 { font-size: 1.5rem; margin: 0 0 4px; }
              label { display: grid; gap: 4px; font-weight: 600; }
              input { font: inherit; padding: 10px 12px; border: 1px solid #8a877d; border-radius: 6px; min-height: 44px; }
              button { font: inherit; font-weight: 600; min-height: 44px; border: 0; border-radius: 6px; background: #16150f; color: #fff; cursor: pointer; }
              :focus-visible { outline: 2px solid #2459c7; outline-offset: 2px; }
              .error { color: #a1251b; margin: 0; }
            </style>
            </head>
            <body>
            <form method="post" action="/cuenta/login">
              <h1>Requi</h1>
              {{aviso}}
              <input type="hidden" name="{{e.Encode(tokens.FormFieldName)}}" value="{{e.Encode(tokens.RequestToken ?? "")}}">
              <input type="hidden" name="returnUrl" value="{{e.Encode(returnUrl ?? "")}}">
              <label>Correo <input name="correo" type="email" autocomplete="username" required></label>
              <label>Contraseña <input name="clave" type="password" autocomplete="current-password" required></label>
              <button type="submit">Iniciar sesión</button>
            </form>
            </body>
            </html>
            """;
    }
}
