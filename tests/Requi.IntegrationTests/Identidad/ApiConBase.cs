using DbUp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Requi.Infrastructure.Identity;
using Serilog.Core;
using Testcontainers.PostgreSql;

namespace Requi.IntegrationTests.Identidad;

/// <summary>
/// API real contra PostgreSQL real (Testcontainers), con los mismos scripts SQL que producción
/// aplicados por DbUp, la semilla de identidad y un usuario de prueba por rol.
/// </summary>
public sealed class ApiConBase : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Redireccion = "http://localhost:4200/callback";
    public const string Clave = "Clave-de-prueba-123";

    /// <summary>Todo lo que la API registra con Serilog, en JSON compacto, para revisar que no haya secretos.</summary>
    public LogsEnMemoria Logs { get; } = new();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Límite de intentos de login por minuto (las pruebas de límite usan uno bajo).</summary>
    public int LoginPorMinuto { get; init; } = 100;

    /// <summary>Si se fija, sube todos los niveles de Serilog (incluido OpenIddict) para probar que el código los contiene.</summary>
    public string? NivelDeLogs { get; init; }

    public static string CorreoDe(string rol) => $"{rol.ToLowerInvariant()}@requi.test";
    public const string Aprobador2 = "aprobador2@requi.test";
    public const string Solicitante2 = "solicitante2@requi.test";
    public const string Mixto = "mixto@requi.test";

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();
        var scripts = Path.Combine(RaizDelRepo(), "database", "scripts");
        var resultado = DeployChanges.To.PostgresqlDatabase(_postgres.GetConnectionString())
            .WithScriptsFromFileSystem(scripts)
            .WithTransactionPerScript()
            .LogToNowhere()
            .Build()
            .PerformUpgrade();
        if (!resultado.Successful) throw resultado.Error;

        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;
        await SemillaIdentidad.EjecutarAsync(
            sp.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            sp.GetRequiredService<IOpenIddictApplicationManager>(),
            [new Uri(Redireccion)],
            [new Uri("http://localhost:4200/")]);

        var usuarios = sp.GetRequiredService<UserManager<Usuario>>();
        foreach (var rol in Roles.Todos)
        {
            var usuario = new Usuario { UserName = CorreoDe(rol), Email = CorreoDe(rol), NombreCompleto = $"Prueba {rol}" };
            var creado = await usuarios.CreateAsync(usuario, Clave);
            if (!creado.Succeeded) throw new InvalidOperationException(string.Join(", ", creado.Errors.Select(e => e.Description)));
            await usuarios.AddToRoleAsync(usuario, rol);
        }

        // Usuarios extra para las reglas: 2 aprobadores distintos, otro solicitante y alguien con dos roles.
        foreach (var (correo, roles) in new[]
        {
            (Aprobador2, new[] { Roles.Aprobador }),
            (Solicitante2, new[] { Roles.Solicitante }),
            (Mixto, new[] { Roles.Solicitante, Roles.Aprobador }),
        })
        {
            var usuario = new Usuario { UserName = correo, Email = correo, NombreCompleto = correo };
            var creado = await usuarios.CreateAsync(usuario, Clave);
            if (!creado.Succeeded) throw new InvalidOperationException(string.Join(", ", creado.Errors.Select(e => e.Description)));
            await usuarios.AddToRolesAsync(usuario, roles);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Limites:LoginPorMinuto", LoginPorMinuto.ToString());
        builder.UseSetting("Limites:TokensPorMinuto", "1000");
        // El Outbox se procesa a mano en las pruebas, para controlar el momento.
        builder.UseSetting("Outbox:Activo", "false");
        if (NivelDeLogs is not null)
        {
            builder.UseSetting("Serilog:MinimumLevel:Default", NivelDeLogs);
            builder.UseSetting("Serilog:MinimumLevel:Override:OpenIddict", NivelDeLogs);
        }
        // Endpoint solo de prueba para comprobar que un rol no entra a lo de otro (caso 1).
        builder.ConfigureServices(s =>
        {
            s.AddSingleton<IStartupFilter, EndpointsDePrueba>();
            s.AddSingleton<ILogEventSink>(Logs);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    private static string RaizDelRepo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Requi.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No encontré la raíz del repositorio.");
    }

    private sealed class EndpointsDePrueba : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> siguiente) => app =>
        {
            // Un tramo propio al inicio: atiende /prueba/* y deja pasar todo lo demás a la app.
            app.UseWhen(c => c.Request.Path.StartsWithSegments("/prueba"), rama =>
            {
                rama.UseRouting();
                rama.UseAuthentication();
                rama.UseAuthorization();
                rama.UseEndpoints(e =>
                {
                    foreach (var rol in Roles.Todos)
                    {
                        e.MapGet($"/prueba/solo-{rol.ToLowerInvariant()}", () => "ok").RequireAuthorization(rol);
                    }
                });
            });
            siguiente(app);
        };
    }
}
