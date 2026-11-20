using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Validation.AspNetCore;
using Requi.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Requi.Api.Identidad;

public static class IdentidadServicios
{
    public const string PoliticaApi = "api";
    public const string LimiteLogin = "login";

    /// <summary>Caso 1: Identity + OpenIddict (Authorization Code + PKCE, refresh token rotado).</summary>
    public static WebApplicationBuilder AddIdentidad(this WebApplicationBuilder builder)
    {
        var config = builder.Configuration;
        var services = builder.Services;

        services.AddDbContext<IdentidadDbContext>((sp, options) =>
        {
            options.UseNpgsql(
                    sp.GetRequiredService<IConfiguration>().GetConnectionString("Database")
                        ?? throw new InvalidOperationException("Falta ConnectionStrings:Database."),
                    npgsql => npgsql.EnableRetryOnFailure())
                .UseSnakeCaseNamingConvention();
            options.UseOpenIddict<Guid>();
        });

        services.AddIdentity<Usuario, IdentityRole<Guid>>(o =>
            {
                o.Password.RequiredLength = 10;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                o.User.RequireUniqueEmail = true;
                // OpenIddict usa sus propios nombres de claims (sub, name, role)
                o.ClaimsIdentity.UserIdClaimType = Claims.Subject;
                o.ClaimsIdentity.UserNameClaimType = Claims.Name;
                o.ClaimsIdentity.RoleClaimType = Claims.Role;
            })
            .AddEntityFrameworkStores<IdentidadDbContext>()
            .AddDefaultTokenProviders();

        // La cookie de sesión es del dominio de la API (Render); Angular solo redirige aquí.
        services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/cuenta/login";
            o.Cookie.Name = "requi.sesion";
            o.Cookie.HttpOnly = true;
            o.Cookie.SameSite = SameSiteMode.Lax; // viaja en la redirección de Angular (navegación GET)
            o.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            o.ExpireTimeSpan = TimeSpan.FromHours(8);
            o.SlidingExpiration = false;
        });

        // Render reinicia el contenedor: con llaves en memoria cada reinicio invalidaría las sesiones.
        services.AddDataProtection()
            .SetApplicationName("requi")
            .PersistKeysToDbContext<IdentidadDbContext>();

        services.AddAntiforgery(o => o.Cookie.Name = "requi.af");

        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore()
                .UseDbContext<IdentidadDbContext>()
                .ReplaceDefaultEntities<Guid>())
            .AddServer(o =>
            {
                o.SetAuthorizationEndpointUris("connect/authorize")
                    .SetTokenEndpointUris("connect/token")
                    .SetEndSessionEndpointUris("connect/logout");

                o.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
                // Solo S256: con "plain" el reto es el mismo verificador y PKCE no protege nada.
                o.Configure(opciones => opciones.CodeChallengeMethods.Remove(CodeChallengeMethods.Plain));
                o.AllowRefreshTokenFlow();

                o.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
                o.SetRefreshTokenLifetime(TimeSpan.FromHours(8));
                // Rotación: cada uso entrega un refresh token nuevo y "quema" el anterior. Sin
                // margen: si alguien presenta uno ya usado, OpenIddict revoca toda la sesión.
                o.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                o.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles,
                    Scopes.OfflineAccess, SemillaIdentidad.ScopeApi);

                if (config["Oidc:Issuer"] is { Length: > 0 } issuer)
                {
                    o.SetIssuer(new Uri(issuer));
                }

                ConfigurarLlaves(o, config, builder.Environment);

                var aspnet = o.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();
                if (!builder.Environment.IsProduction())
                {
                    aspnet.DisableTransportSecurityRequirement(); // local y pruebas usan http
                }
            })
            .AddValidation(o =>
            {
                o.UseLocalServer();
                o.UseAspNetCore();
                // Cada petición revisa en la base que el access token no esté revocado. Al reutilizar
                // un refresh token, OpenIddict revoca todos los tokens de la sesión (no el registro
                // de autorización), así que es esta validación la que corta el acceso de inmediato.
                o.EnableTokenEntryValidation();
            });

        services.AddAuthorizationBuilder()
            .AddPolicy(PoliticaApi, p => p
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());
        foreach (var rol in Roles.Todos)
        {
            services.AddAuthorizationBuilder().AddPolicy(rol, p => p
                .AddAuthenticationSchemes(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireRole(rol));
        }

        // Límites por IP en el login y en el endpoint de tokens (los maneja OpenIddict, no un
        // endpoint mapeado, por eso va en el limitador global filtrado por ruta).
        var porMinutoLogin = config.GetValue("Limites:LoginPorMinuto", 10);
        var porMinutoTokens = config.GetValue("Limites:TokensPorMinuto", 60);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            {
                var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "sin-ip";
                var ruta = ctx.Request.Path;
                if (HttpMethods.IsPost(ctx.Request.Method) && ruta.StartsWithSegments("/cuenta/login"))
                {
                    return RateLimitPartition.GetFixedWindowLimiter($"login:{ip}", _ => Ventana(porMinutoLogin));
                }
                if (ruta.StartsWithSegments("/connect/token"))
                {
                    return RateLimitPartition.GetFixedWindowLimiter($"token:{ip}", _ => Ventana(porMinutoTokens));
                }
                return RateLimitPartition.GetNoLimiter("libre");
            });
        });

        return builder;
    }

    private static FixedWindowRateLimiterOptions Ventana(int permitidos) => new()
    {
        PermitLimit = permitidos,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
    };

    /// <summary>
    /// En producción las llaves vienen de certificados en variables de entorno (base64 de un
    /// PFX), nunca del repositorio. En local y en pruebas se usan llaves efímeras en memoria.
    /// </summary>
    private static void ConfigurarLlaves(
        OpenIddictServerBuilder o, IConfiguration config, IHostEnvironment env)
    {
        var firma = config["Oidc:CertificadoFirma"];
        var cifrado = config["Oidc:CertificadoCifrado"];
        if (!string.IsNullOrEmpty(firma) && !string.IsNullOrEmpty(cifrado))
        {
            var clave = config["Oidc:ClaveCertificados"];
            o.AddSigningCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(firma), clave));
            o.AddEncryptionCertificate(X509CertificateLoader.LoadPkcs12(Convert.FromBase64String(cifrado), clave));
            return;
        }
        // El build arranca la app para escribir openapi/requi.json; ahí no se emite ningún token.
        var generandoOpenApi = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";
        if (env.IsProduction() && !generandoOpenApi)
        {
            throw new InvalidOperationException(
                "Faltan Oidc:CertificadoFirma y Oidc:CertificadoCifrado en producción.");
        }
        o.AddEphemeralSigningKey().AddEphemeralEncryptionKey();
    }
}
