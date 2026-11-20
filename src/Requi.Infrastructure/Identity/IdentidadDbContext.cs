using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Requi.Infrastructure.Identity;

/// <summary>
/// Tablas de Identity, OpenIddict y Data Protection. No se scaffoldean: usan las entidades
/// de sus librerías, mapeadas a snake_case. Su SQL vive en database/scripts/0002_identidad.sql
/// (generado una vez desde este modelo, ver ADR 0001).
/// </summary>
public class IdentidadDbContext(DbContextOptions<IdentidadDbContext> options)
    : IdentityDbContext<Usuario, IdentityRole<Guid>, Guid>(options), IDataProtectionKeyContext
{
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Usuario>().ToTable("users");
        builder.Entity<IdentityRole<Guid>>().ToTable("roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims");
        builder.Entity<DataProtectionKey>().ToTable("data_protection_keys");

        builder.UseOpenIddict<Guid>();
        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreApplication<Guid>>()
            .ToTable("oidc_applications");
        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization<Guid>>()
            .ToTable("oidc_authorizations");
        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreScope<Guid>>()
            .ToTable("oidc_scopes");
        builder.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken<Guid>>()
            .ToTable("oidc_tokens");
    }
}

public class Usuario : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = "";
}

public static class Roles
{
    public const string Solicitante = "Solicitante";
    public const string Aprobador = "Aprobador";
    public const string Compras = "Compras";
    public static readonly string[] Todos = [Solicitante, Aprobador, Compras];
}
