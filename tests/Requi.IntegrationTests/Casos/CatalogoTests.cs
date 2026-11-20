using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Requi.Infrastructure.Identity;
using Requi.IntegrationTests.Identidad;
using static Requi.Api.Catalogo.EndpointsCatalogo;

namespace Requi.IntegrationTests.Casos;

/// <summary>Caso 2: crear, editar, listar y desactivar; FluentValidation; solo Compras escribe.</summary>
[Collection(ApiCompartida.Nombre)]
public class CatalogoTests(ApiConBase api)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static string Unico(string prefijo) => $"{prefijo}-{Guid.NewGuid().ToString("N")[..8]}";

    private async Task<ProveedorDto> CrearProveedor(HttpClient compras, string? nombre = null) =>
        await (await compras.PostAsJsonAsync("/api/proveedores",
            new ProveedorEntrada(nombre ?? Unico("Proveedor"), "ventas@proveedor.test", "667 123 4567"), Ct)).LeerAsync<ProveedorDto>();

    [Fact]
    public async Task Compras_crea_edita_lista_y_desactiva()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var proveedor = await CrearProveedor(compras);
        var sku = Unico("GUA");

        var creado = await compras.PostAsJsonAsync("/api/productos",
            new ProductoEntrada(proveedor.Id, sku.ToLowerInvariant(), "Guantes de nitrilo", "caja", 185.00m), Ct);
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var producto = await creado.LeerAsync<ProductoDto>();
        Assert.Equal(sku.ToUpperInvariant(), producto.Sku); // SKU normalizado
        Assert.Equal(proveedor.Nombre, producto.Proveedor);

        var editado = await (await compras.PutAsJsonAsync($"/api/productos/{producto.Id}",
            new ProductoEntrada(proveedor.Id, sku, "Guantes de nitrilo talla M", "caja", 199.50m), Ct)).LeerAsync<ProductoDto>();
        Assert.Equal(199.50m, editado.PrecioUnitario);

        Assert.Equal(HttpStatusCode.NoContent, (await compras.PostAsync($"/api/productos/{producto.Id}/desactivar", null, Ct)).StatusCode);
        var activos = await (await compras.GetAsync($"/api/productos?activos=true&proveedorId={proveedor.Id}", Ct)).LeerAsync<List<ProductoDto>>();
        Assert.DoesNotContain(activos, p => p.Id == producto.Id);
        var todos = await (await compras.GetAsync($"/api/productos?proveedorId={proveedor.Id}", Ct)).LeerAsync<List<ProductoDto>>();
        Assert.Contains(todos, p => p.Id == producto.Id && !p.Activo);
    }

    [Theory]
    [InlineData("Solicitante")]
    [InlineData("Aprobador")]
    public async Task Otros_roles_leen_pero_no_escriben(string rol)
    {
        var cliente = await Sesiones.ComoAsync(api, rol);
        Assert.Equal(HttpStatusCode.OK, (await cliente.GetAsync("/api/productos", Ct)).StatusCode);

        var escribir = await cliente.PostAsJsonAsync("/api/proveedores", new ProveedorEntrada("No debería", "a@b.test", null), Ct);
        Assert.Equal(HttpStatusCode.Forbidden, escribir.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.PostAsync($"/api/productos/{Guid.NewGuid()}/desactivar", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Sin_sesion_nada()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.CreateClient().GetAsync("/api/proveedores", Ct)).StatusCode);
    }

    [Fact]
    public async Task La_validacion_responde_400_con_el_error_de_cada_campo()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var r = await compras.PostAsJsonAsync("/api/productos",
            new ProductoEntrada(Guid.Empty, "sku con espacios", "", "caja", -5m), Ct);

        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
        var errores = (await r.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("errors");
        foreach (var campo in new[] { "ProveedorId", "Sku", "Nombre", "PrecioUnitario" })
        {
            Assert.True(errores.TryGetProperty(campo, out _), $"falta el error de {campo}");
        }
    }

    [Fact]
    public async Task Un_precio_con_mas_de_dos_decimales_se_rechaza()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var proveedor = await CrearProveedor(compras);
        var r = await compras.PostAsJsonAsync("/api/productos", new ProductoEntrada(proveedor.Id, Unico("X"), "X", "pza", 10.001m), Ct);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact]
    public async Task Un_SKU_repetido_responde_409_sin_detalles_internos()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var proveedor = await CrearProveedor(compras);
        var sku = Unico("DUP");
        await (await compras.PostAsJsonAsync("/api/productos", new ProductoEntrada(proveedor.Id, sku, "Uno", "pza", 1m), Ct)).LeerAsync<ProductoDto>();

        var r = await compras.PostAsJsonAsync("/api/productos", new ProductoEntrada(proveedor.Id, sku, "Dos", "pza", 1m), Ct);

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        var cuerpo = await r.Content.ReadAsStringAsync(Ct);
        Assert.Contains("duplicado", cuerpo);
        Assert.DoesNotContain("uq_products_sku", cuerpo); // ni nombres de restricciones ni trazas
    }

    [Fact]
    public async Task No_se_da_de_alta_un_producto_de_un_proveedor_desactivado()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var proveedor = await CrearProveedor(compras);
        await compras.PostAsync($"/api/proveedores/{proveedor.Id}/desactivar", null, Ct);

        var r = await compras.PostAsJsonAsync("/api/productos", new ProductoEntrada(proveedor.Id, Unico("Z"), "Z", "pza", 1m), Ct);

        Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
        Assert.Contains("proveedor_invalido", await r.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task Editar_algo_que_no_existe_es_404()
    {
        var compras = await Sesiones.ComoAsync(api, Roles.Compras);
        var r = await compras.PutAsJsonAsync($"/api/proveedores/{Guid.NewGuid()}", new ProveedorEntrada("X", "x@y.test", null), Ct);
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
    }
}
