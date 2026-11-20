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

var builder = WebApplication.CreateBuilder(args);

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
