using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Requi.Infrastructure.Identity;

/// <summary>
/// Datos de arranque de la identidad: los 3 roles y el cliente público de Angular.
/// La corre Requi.DbMigrator después de los scripts (la API nunca modifica la base al
/// arrancar) y las pruebas de integración. Es idempotente: correrla dos veces no duplica.
/// </summary>
public static class SemillaIdentidad
{
    public const string ClienteWeb = "requi-web";
    public const string ScopeApi = "requi-api";

    public static async Task EjecutarAsync(
        RoleManager<IdentityRole<Guid>> roles,
        IOpenIddictApplicationManager aplicaciones,
        IReadOnlyCollection<Uri> redirecciones,
        IReadOnlyCollection<Uri> redireccionesSalida,
        CancellationToken ct = default)
    {
        foreach (var rol in Roles.Todos)
        {
            if (!await roles.RoleExistsAsync(rol))
            {
                var r = await roles.CreateAsync(new IdentityRole<Guid>(rol));
                if (!r.Succeeded) throw new InvalidOperationException($"No se creó el rol {rol}.");
            }
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClienteWeb,
            ClientType = ClientTypes.Public, // Angular no puede guardar un secreto: usa PKCE
            ApplicationType = ApplicationTypes.Web,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "Requi (Angular)",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.EndSession,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Email,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + ScopeApi,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };
        foreach (var uri in redirecciones) descriptor.RedirectUris.Add(uri);
        foreach (var uri in redireccionesSalida) descriptor.PostLogoutRedirectUris.Add(uri);

        // Crear o actualizar: así cambiar la URL de Vercel solo requiere volver a correr la semilla.
        var existente = await aplicaciones.FindByClientIdAsync(ClienteWeb, ct);
        if (existente is null) await aplicaciones.CreateAsync(descriptor, ct);
        else await aplicaciones.UpdateAsync(existente, descriptor, ct);
    }
}
