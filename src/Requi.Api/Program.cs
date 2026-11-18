using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Npgsql;
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

builder.Services.AddOpenApi();

var app = builder.Build();

app.MapOpenApi();

// live: el proceso responde (no toca la base, para no despertar Neon).
// ready: además la base contesta.
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });

app.Run();

public partial class Program;
