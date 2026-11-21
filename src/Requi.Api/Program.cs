using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using FluentValidation;
using Requi.Api.Catalogo;
using Requi.Api.Comun;
using Requi.Api.Identidad;
using Requi.Api.Outbox;
using Requi.Api.Requisiciones;
using Requi.Infrastructure.Persistence;
using Serilog;
using Serilog.Context;
using Serilog.Events;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// Logs en JSON compacto a la consola (Render los recoge de ahí). Los niveles salen de Serilog:MinimumLevel.
// ReadFrom.Services deja que las pruebas registren su propio sink en el contenedor.
builder.Host.UseSerilog((contexto, servicios, log) => log
    .ReadFrom.Configuration(contexto.Configuration)
    // Fijo en código y después de la configuración (gana el último): en Debug/Verbose,
    // OpenIddict escribe un fragmento del refresh token. Ninguna variable puede bajarlo.
    .MinimumLevel.Override("OpenIddict", Serilog.Events.LogEventLevel.Warning)
    .ReadFrom.Services(servicios)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

// La cadena se lee al crear el contexto, no al arrancar: así el documento OpenAPI
// se genera en el build sin base de datos, y /health/ready reporta si falta.
builder.Services.AddDbContext<RequiDbContext>((sp, options) =>
    options.UseNpgsql(
        sp.GetRequiredService<IConfiguration>().GetConnectionString("Database")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:Database."),
        npgsql => npgsql.EnableRetryOnFailure()));

// Un solo intento, sin la estrategia de reintentos: un health check debe contestar rápido.
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RequiDbContext>(tags: ["ready"], customTestQuery: async (db, ct) =>
    {
        await using var connection = new NpgsqlConnection(db.Database.GetConnectionString());
        await connection.OpenAsync(ct);
        return true;
    });

builder.AddIdentidad();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IManejadorOutbox, CrearOrdenDeCompra>();
builder.Services.AddScoped<IManejadorOutbox, AvisarProveedor>();
builder.Services.AddSingleton<ProcesadorOutbox>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ProcesadorOutbox>());
builder.Services.AddExceptionHandler<ManejadorDeErrores>();

builder.Services.AddOpenApi();

// Solo el dominio de Vercel y localhost. Se configuran en Cors:Origins (separados por coma).
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

// TraceId en todo evento del request, incluido el error que registra el manejador de excepciones.
app.Use(async (http, siguiente) =>
{
    using (LogContext.PushProperty("TraceId", http.TraceIdentifier))
        await siguiente(http);
});

// Una línea por request: método, ruta (Request.Path, SIN querystring), estado y duración.
// No registra encabezados, cookies ni cuerpos. Los health checks bajan a Debug para no llenar el log.
// Logger del contenedor y no el estático: así cada host (y cada host de pruebas) usa el suyo.
app.UseSerilogRequestLogging(o =>
{
    o.Logger = app.Services.GetRequiredService<Serilog.ILogger>();
    o.GetLevel = (http, _, error) =>
        error is not null || http.Response.StatusCode >= 500 ? LogEventLevel.Error
        : http.Request.Path.StartsWithSegments("/health") ? LogEventLevel.Debug
        : LogEventLevel.Information;
});

app.UseExceptionHandler();
app.UseStatusCodePages();

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenApi();

// live: el proceso responde (no toca la base, para no despertar Neon).
// ready: además la base contesta.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.MapIdentidad();
app.MapCatalogo();
app.MapRequisiciones();
app.MapOrdenes();

app.Run();

public partial class Program;
