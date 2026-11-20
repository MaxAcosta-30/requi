using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Requi.Api.Comun;
using Requi.Api.Identidad;
using Requi.Domain.Entities;
using Requi.Domain.Reglas;
using Requi.Infrastructure.Identity;
using Requi.Infrastructure.Persistence;

namespace Requi.Api.Catalogo;

/// <summary>Caso 2: productos y proveedores. Todos leen; solo Compras escribe.</summary>
public static class EndpointsCatalogo
{
    public record ProveedorEntrada(string Nombre, string Correo, string? Telefono);
    public record ProveedorDto(Guid Id, string Nombre, string Correo, string? Telefono, bool Activo);
    public record ProductoEntrada(Guid ProveedorId, string Sku, string Nombre, string Unidad, decimal PrecioUnitario);
    public record ProductoDto(Guid Id, Guid ProveedorId, string Proveedor, string Sku, string Nombre, string Unidad, decimal PrecioUnitario, bool Activo);

    public sealed class ProveedorValidador : AbstractValidator<ProveedorEntrada>
    {
        public ProveedorValidador()
        {
            RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Correo).NotEmpty().EmailAddress().MaximumLength(254);
            RuleFor(x => x.Telefono).MaximumLength(30).Matches(@"^[0-9 +()-]*$").When(x => x.Telefono is not null);
        }
    }

    public sealed class ProductoValidador : AbstractValidator<ProductoEntrada>
    {
        public ProductoValidador()
        {
            RuleFor(x => x.ProveedorId).NotEmpty();
            RuleFor(x => x.Sku).NotEmpty().MaximumLength(40).Matches("^[A-Za-z0-9._-]+$")
                .WithMessage("El SKU solo admite letras, números, punto, guion y guion bajo.");
            RuleFor(x => x.Nombre).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Unidad).NotEmpty().MaximumLength(20);
            RuleFor(x => x.PrecioUnitario).GreaterThan(0).PrecisionScale(18, 2, ignoreTrailingZeros: true);
        }
    }

    public static WebApplication MapCatalogo(this WebApplication app)
    {
        var proveedores = app.MapGroup("/api/proveedores").WithTags("Proveedores").RequireAuthorization(IdentidadServicios.PoliticaApi);
        proveedores.MapGet("/", ListarProveedores).WithName("ListarProveedores");
        proveedores.MapPost("/", CrearProveedor).WithName("CrearProveedor").RequireAuthorization(Roles.Compras).Validar<ProveedorEntrada>();
        proveedores.MapPut("/{id:guid}", EditarProveedor).WithName("EditarProveedor").RequireAuthorization(Roles.Compras).Validar<ProveedorEntrada>();
        proveedores.MapPost("/{id:guid}/desactivar", DesactivarProveedor).WithName("DesactivarProveedor").RequireAuthorization(Roles.Compras);

        var productos = app.MapGroup("/api/productos").WithTags("Productos").RequireAuthorization(IdentidadServicios.PoliticaApi);
        productos.MapGet("/", ListarProductos).WithName("ListarProductos");
        productos.MapPost("/", CrearProducto).WithName("CrearProducto").RequireAuthorization(Roles.Compras).Validar<ProductoEntrada>();
        productos.MapPut("/{id:guid}", EditarProducto).WithName("EditarProducto").RequireAuthorization(Roles.Compras).Validar<ProductoEntrada>();
        productos.MapPost("/{id:guid}/desactivar", DesactivarProducto).WithName("DesactivarProducto").RequireAuthorization(Roles.Compras);
        return app;
    }

    private static ProveedorDto ADto(Supplier s) => new(s.Id, s.Name, s.Email, s.Phone, s.IsActive);

    private static async Task<Ok<List<ProveedorDto>>> ListarProveedores(RequiDbContext db, bool? activos, CancellationToken ct) =>
        TypedResults.Ok(await db.Suppliers.AsNoTracking()
            .Where(s => activos == null || s.IsActive == activos)
            .OrderBy(s => s.Name)
            .Select(s => new ProveedorDto(s.Id, s.Name, s.Email, s.Phone, s.IsActive))
            .ToListAsync(ct));

    private static async Task<Created<ProveedorDto>> CrearProveedor(ProveedorEntrada e, RequiDbContext db, CancellationToken ct)
    {
        var s = new Supplier { Id = Guid.CreateVersion7(), Name = e.Nombre.Trim(), Email = e.Correo.Trim(), Phone = e.Telefono?.Trim(), IsActive = true, UpdatedAt = DateTime.UtcNow };
        db.Suppliers.Add(s);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/proveedores/{s.Id}", ADto(s));
    }

    private static async Task<Results<Ok<ProveedorDto>, NotFound>> EditarProveedor(Guid id, ProveedorEntrada e, RequiDbContext db, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return TypedResults.NotFound();
        (s.Name, s.Email, s.Phone, s.UpdatedAt) = (e.Nombre.Trim(), e.Correo.Trim(), e.Telefono?.Trim(), DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ADto(s));
    }

    private static async Task<Results<NoContent, NotFound>> DesactivarProveedor(Guid id, RequiDbContext db, CancellationToken ct)
    {
        var s = await db.Suppliers.FindAsync([id], ct);
        if (s is null) return TypedResults.NotFound();
        (s.IsActive, s.UpdatedAt) = (false, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Ok<List<ProductoDto>>> ListarProductos(RequiDbContext db, bool? activos, Guid? proveedorId, CancellationToken ct) =>
        TypedResults.Ok(await db.Products.AsNoTracking()
            .Where(p => activos == null || p.IsActive == activos)
            .Where(p => proveedorId == null || p.SupplierId == proveedorId)
            .OrderBy(p => p.Name)
            .Select(p => new ProductoDto(p.Id, p.SupplierId, p.Supplier.Name, p.Sku, p.Name, p.Unit, p.UnitPrice, p.IsActive))
            .ToListAsync(ct));

    private static async Task<Created<ProductoDto>> CrearProducto(ProductoEntrada e, RequiDbContext db, CancellationToken ct)
    {
        var proveedor = await ProveedorActivo(db, e.ProveedorId, ct);
        var p = new Product
        {
            Id = Guid.CreateVersion7(),
            SupplierId = proveedor.Id,
            Sku = e.Sku.Trim().ToUpperInvariant(),
            Name = e.Nombre.Trim(),
            Unit = e.Unidad.Trim(),
            UnitPrice = e.PrecioUnitario,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Products.Add(p);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/api/productos/{p.Id}", new ProductoDto(p.Id, p.SupplierId, proveedor.Name, p.Sku, p.Name, p.Unit, p.UnitPrice, p.IsActive));
    }

    private static async Task<Results<Ok<ProductoDto>, NotFound>> EditarProducto(Guid id, ProductoEntrada e, RequiDbContext db, CancellationToken ct)
    {
        var p = await db.Products.FindAsync([id], ct);
        if (p is null) return TypedResults.NotFound();
        var proveedor = await ProveedorActivo(db, e.ProveedorId, ct);
        (p.SupplierId, p.Sku, p.Name, p.Unit, p.UnitPrice, p.UpdatedAt) =
            (proveedor.Id, e.Sku.Trim().ToUpperInvariant(), e.Nombre.Trim(), e.Unidad.Trim(), e.PrecioUnitario, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(new ProductoDto(p.Id, p.SupplierId, proveedor.Name, p.Sku, p.Name, p.Unit, p.UnitPrice, p.IsActive));
    }

    private static async Task<Results<NoContent, NotFound>> DesactivarProducto(Guid id, RequiDbContext db, CancellationToken ct)
    {
        var p = await db.Products.FindAsync([id], ct);
        if (p is null) return TypedResults.NotFound();
        (p.IsActive, p.UpdatedAt) = (false, DateTime.UtcNow);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    private static async Task<Supplier> ProveedorActivo(RequiDbContext db, Guid id, CancellationToken ct) =>
        await db.Suppliers.FirstOrDefaultAsync(s => s.Id == id && s.IsActive, ct)
            ?? throw new ReglaDeNegocioException("proveedor_invalido", "El proveedor no existe o está desactivado.");
}
