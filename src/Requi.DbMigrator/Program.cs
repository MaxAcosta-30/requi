using DbUp;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using Requi.Infrastructure.Identity;

// Aplica en orden los scripts de database/scripts que aún no corrieron.
// DbUp registra los aplicados en la tabla schemaversions.
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Migrations");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("Falta la variable ConnectionStrings__Migrations.");
    return 1;
}

var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(Program).Assembly)
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
if (!result.Successful)
{
    Console.Error.WriteLine(result.Error);
    return 1;
}

Console.WriteLine("Base de datos al día.");

// Datos de arranque de la identidad (roles y cliente de Angular). Solo donde hay URLs de
// redirección configuradas: el paso de scaffold del CI no las necesita.
var redirecciones = Uris("Oidc__Redirecciones");
if (redirecciones.Count == 0)
{
    Console.WriteLine("Semilla de identidad omitida: falta Oidc__Redirecciones.");
    return 0;
}

var servicios = new ServiceCollection();
servicios.AddLogging();
servicios.AddDbContext<IdentidadDbContext>(o =>
{
    o.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
    o.UseOpenIddict<Guid>();
});
servicios.AddIdentityCore<Usuario>()
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<IdentidadDbContext>();
servicios.AddOpenIddict().AddCore(o => o.UseEntityFrameworkCore()
    .UseDbContext<IdentidadDbContext>()
    .ReplaceDefaultEntities<Guid>());

await using var proveedor = servicios.BuildServiceProvider();
await using var scope = proveedor.CreateAsyncScope();
await SemillaIdentidad.EjecutarAsync(
    scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
    scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>(),
    redirecciones,
    Uris("Oidc__RedireccionesSalida"));
Console.WriteLine($"Semilla de identidad aplicada ({redirecciones.Count} URL de redirección).");
return 0;

static List<Uri> Uris(string variable) =>
    [.. (Environment.GetEnvironmentVariable(variable) ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(u => new Uri(u, UriKind.Absolute))];
