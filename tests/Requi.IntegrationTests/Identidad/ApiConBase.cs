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

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    /// <summary>Límite de intentos de login por minuto (las pruebas de límite usan uno bajo).</summary>
    public int LoginPorMinuto { get; init; } = 100;

    public static string CorreoDe(string rol) => $"{rol.ToLowerInvariant()}@requi.test";

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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", _postgres.GetConnectionString());
        builder.UseSetting("Limites:LoginPorMinuto", LoginPorMinuto.ToString());
        builder.UseSetting("Limites:TokensPorMinuto", "1000");
        // Endpoint solo de prueba para comprobar que un rol no entra a lo de otro (caso 1).
        builder.ConfigureServices(s => s.AddSingleton<IStartupFilter, EndpointsDePrueba>());
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
