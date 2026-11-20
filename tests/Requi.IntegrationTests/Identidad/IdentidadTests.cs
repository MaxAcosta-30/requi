using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Requi.Infrastructure.Identity;

namespace Requi.IntegrationTests.Identidad;

/// <summary>Caso 1: Authorization Code + PKCE, access token de 10 min y refresh token rotado.</summary>
public class IdentidadTests(ApiConBase api) : IClassFixture<ApiConBase>
{
    private static readonly string Solicitante = ApiConBase.CorreoDe(Roles.Solicitante);

    [Fact]
    public async Task Flujo_completo_entrega_tokens_y_la_api_reconoce_al_usuario()
    {
        var flujo = new FlujoOidc(api);
        var tokens = await flujo.IniciarSesion(Solicitante);

        Assert.InRange(tokens.ExpiresIn, 590, 600); // 10 minutos (se calcula al emitirlo: puede dar 599)
        Assert.NotNull(tokens.RefreshToken);
        Assert.NotNull(tokens.IdToken);

        var sesion = await flujo.Get("/api/sesion", tokens.AccessToken);
        sesion.EnsureSuccessStatusCode();
        var cuerpo = await sesion.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        Assert.Equal(Solicitante, cuerpo.GetProperty("correo").GetString());
        Assert.Equal([Roles.Solicitante], cuerpo.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
    }

    [Fact]
    public async Task Sin_token_la_api_responde_401()
    {
        var respuesta = await api.CreateClient().GetAsync("/api/sesion", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Sin_PKCE_el_servidor_rechaza_la_autorizacion()
    {
        var flujo = new FlujoOidc(api);
        var respuesta = await flujo.Navegador.GetAsync(FlujoOidc.UrlAutorizar(reto: null), TestContext.Current.CancellationToken);

        // El error vuelve al cliente (o se muestra) en lugar de pedir login
        var destino = respuesta.Headers.Location?.ToString() ?? await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("/cuenta/login", destino);
        Assert.Contains("code_challenge", destino);
    }

    [Fact]
    public async Task PKCE_plain_no_se_acepta_solo_S256()
    {
        var flujo = new FlujoOidc(api);
        var (verificador, _) = FlujoOidc.Pkce();
        var url = FlujoOidc.UrlAutorizar(verificador).Replace("code_challenge_method=S256", "code_challenge_method=plain");

        var respuesta = await flujo.Navegador.GetAsync(url, TestContext.Current.CancellationToken);

        var destino = respuesta.Headers.Location?.ToString() ?? await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("/cuenta/login", destino);
        Assert.Contains("code_challenge_method", destino);
    }

    [Fact]
    public async Task El_codigo_no_se_canjea_con_un_verificador_distinto()
    {
        var flujo = new FlujoOidc(api);
        var (_, reto) = FlujoOidc.Pkce();
        var inicio = await flujo.Navegador.GetAsync(FlujoOidc.UrlAutorizar(reto), TestContext.Current.CancellationToken);
        var returnUrl = QueryHelpers.ParseQuery(inicio.Headers.Location!.Query)["ReturnUrl"].ToString();
        var login = await flujo.Login(Solicitante, ApiConBase.Clave, returnUrl);
        var callback = await flujo.Navegador.GetAsync(login.Headers.Location, TestContext.Current.CancellationToken);
        var codigo = QueryHelpers.ParseQuery(callback.Headers.Location!.Query)["code"].ToString();

        var (otroVerificador, _) = FlujoOidc.Pkce();
        var respuesta = await flujo.CanjearCodigo(codigo, otroVerificador);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("invalid_grant", await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task El_refresh_token_rota_en_cada_uso()
    {
        var flujo = new FlujoOidc(api);
        var tokens = await flujo.IniciarSesion(Solicitante);

        var respuesta = await flujo.Refrescar(tokens.RefreshToken!);
        respuesta.EnsureSuccessStatusCode();
        var nuevos = (await respuesta.Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken))!;

        Assert.NotNull(nuevos.RefreshToken);
        Assert.NotEqual(tokens.RefreshToken, nuevos.RefreshToken);
        (await flujo.Get("/api/sesion", nuevos.AccessToken)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Reutilizar_un_refresh_token_viejo_revoca_toda_la_sesion()
    {
        var flujo = new FlujoOidc(api);
        var tokens = await flujo.IniciarSesion(Solicitante);
        var rotado = (await (await flujo.Refrescar(tokens.RefreshToken!))
            .Content.ReadFromJsonAsync<Tokens>(TestContext.Current.CancellationToken))!;

        // Alguien presenta el refresh token viejo (por ejemplo, uno robado)
        var reuso = await flujo.Refrescar(tokens.RefreshToken!);
        Assert.Equal(HttpStatusCode.BadRequest, reuso.StatusCode);

        // Toda la sesión cae: ni el refresh token nuevo ni su access token sirven ya
        var conElNuevo = await flujo.Refrescar(rotado.RefreshToken!);
        Assert.Equal(HttpStatusCode.BadRequest, conElNuevo.StatusCode);
        var api401 = await flujo.Get("/api/sesion", rotado.AccessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, api401.StatusCode);
    }

    [Theory]
    [InlineData("Solicitante", "/prueba/solo-compras")]
    [InlineData("Solicitante", "/prueba/solo-aprobador")]
    [InlineData("Compras", "/prueba/solo-solicitante")]
    [InlineData("Aprobador", "/prueba/solo-compras")]
    public async Task Un_rol_no_puede_usar_los_endpoints_de_otro(string rol, string ruta)
    {
        var flujo = new FlujoOidc(api);
        var tokens = await flujo.IniciarSesion(ApiConBase.CorreoDe(rol));

        var ajeno = await flujo.Get(ruta, tokens.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, ajeno.StatusCode);

        var propio = await flujo.Get($"/prueba/solo-{rol.ToLowerInvariant()}", tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, propio.StatusCode);
    }

    [Fact]
    public async Task El_token_lleva_rol_y_sujeto_pero_nunca_el_sello_de_seguridad()
    {
        var flujo = new FlujoOidc(api);
        var tokens = await flujo.IniciarSesion(ApiConBase.CorreoDe(Roles.Compras));

        // El id_token es un JWT firmado: su segunda parte es JSON en base64url
        var carga = tokens.IdToken!.Split('.')[1].Replace('-', '+').Replace('_', '/');
        var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(carga.PadRight(carga.Length + (4 - carga.Length % 4) % 4, '=')));
        var claims = JsonDocument.Parse(json).RootElement;
        Assert.Equal(Roles.Compras, claims.GetProperty("role").GetString());
        Assert.DoesNotContain("SecurityStamp", json);
    }

    [Theory]
    [InlineData("noexiste@requi.test", ApiConBase.Clave)]
    [InlineData("aprobador@requi.test", "clave-incorrecta")]
    public async Task Login_fallido_no_revela_si_la_cuenta_existe(string correo, string clave)
    {
        var flujo = new FlujoOidc(api);
        var respuesta = await flujo.Login(correo, clave, "/");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos.", await respuesta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("//sitio-malo.example/robar")]
    [InlineData("https://sitio-malo.example/")]
    [InlineData("/\\sitio-malo.example")]
    public async Task El_login_nunca_redirige_fuera_de_la_api(string returnUrl)
    {
        var flujo = new FlujoOidc(api);
        var respuesta = await flujo.Login(ApiConBase.CorreoDe(Roles.Aprobador), ApiConBase.Clave, returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
        Assert.Equal("/", respuesta.Headers.Location?.ToString());
    }

    [Fact]
    public async Task El_login_sin_token_antiforgery_se_rechaza()
    {
        var respuesta = await api.CreateClient().PostAsync("/cuenta/login", new FormUrlEncodedContent(
            new Dictionary<string, string> { ["correo"] = Solicitante, ["clave"] = ApiConBase.Clave }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}

/// <summary>Límite de intentos en el login, con su propia API para no afectar a las demás pruebas.</summary>
public class LimiteDeLoginTests(LimiteDeLoginTests.ApiConLimiteBajo fixture)
    : IClassFixture<LimiteDeLoginTests.ApiConLimiteBajo>
{
    public sealed class ApiConLimiteBajo : IAsyncLifetime
    {
        public ApiConBase Api { get; } = new() { LoginPorMinuto = 3 };
        public ValueTask InitializeAsync() => Api.InitializeAsync();
        public ValueTask DisposeAsync() => Api.DisposeAsync();
    }

    [Fact]
    public async Task Tras_el_limite_por_minuto_responde_429()
    {
        var flujo = new FlujoOidc(fixture.Api);
        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await flujo.Login("x@requi.test", "x", "/")).StatusCode);
        }
        Assert.Equal(HttpStatusCode.TooManyRequests, (await flujo.Login("x@requi.test", "x", "/")).StatusCode);
    }
}
