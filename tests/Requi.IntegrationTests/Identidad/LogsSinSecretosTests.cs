using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Requi.Infrastructure.Identity;

namespace Requi.IntegrationTests.Identidad;

/// <summary>Los logs no deben guardar códigos, verificadores PKCE, tokens, claves ni querystrings.</summary>
public class LogsSinSecretosTests(ApiConBase api) : IClassFixture<ApiConBase>
{
    [Fact]
    public Task Tras_login_y_canje_de_token_ningun_evento_contiene_secretos() => RevisarAsync(api);

    /// <summary>
    /// En Debug/Verbose, OpenIddict escribe un fragmento del refresh token. Program.cs fija OpenIddict
    /// en Warning después de leer la configuración: aunque alguien suba los niveles, no debe filtrarse.
    /// </summary>
    [Fact]
    public async Task Con_todos_los_niveles_en_Verbose_tampoco_se_filtran_secretos()
    {
        await using var verbose = new ApiConBase { NivelDeLogs = "Verbose" };
        await verbose.InitializeAsync();
        await RevisarAsync(verbose);
    }

    private static async Task RevisarAsync(ApiConBase api)
    {
        var ct = TestContext.Current.CancellationToken;
        var flujo = new FlujoOidc(api);

        // Flujo completo, pero con los pasos a la vista para tener el code y el verificador.
        var (verificador, reto) = FlujoOidc.Pkce();
        var sinSesion = await flujo.Navegador.GetAsync(FlujoOidc.UrlAutorizar(reto), ct);
        var returnUrl = QueryHelpers.ParseQuery(sinSesion.Headers.Location!.Query)["ReturnUrl"].ToString();
        var login = await flujo.Login(ApiConBase.CorreoDe(Roles.Solicitante), ApiConBase.Clave, returnUrl);
        var conSesion = await flujo.Navegador.GetAsync(login.Headers.Location, ct);
        var codigo = QueryHelpers.ParseQuery(conSesion.Headers.Location!.Query)["code"].ToString();
        var tokens = await (await flujo.CanjearCodigo(codigo, verificador)).Content.ReadFromJsonAsync<Tokens>(ct);
        await flujo.Refrescar(tokens!.RefreshToken!);

        // Un error de login y rutas con secretos inventados en la querystring.
        await flujo.Login(ApiConBase.CorreoDe(Roles.Solicitante), "clave-incorrecta-SECRETA", returnUrl);
        var ruta = await flujo.Navegador.GetAsync("/ruta-que-no-existe?code=XYZ-codigo&refresh_token=XYZ-refresh", ct);
        Assert.Equal(HttpStatusCode.NotFound, ruta.StatusCode);

        var eventos = api.Logs.Todos();
        var texto = string.Join('\n', eventos);

        // Hay logs de requests (la prueba no pasa por estar vacía) y llevan TraceId.
        Assert.Contains(eventos, e => e.Contains("/ruta-que-no-existe"));
        Assert.Contains(eventos, e => e.Contains("TraceId"));

        string[] secretos =
        [
            codigo, verificador, tokens.AccessToken, tokens.RefreshToken!, tokens.IdToken!,
            "XYZ-codigo", "XYZ-refresh", "clave-incorrecta-SECRETA", ApiConBase.Clave, "client_secret",
        ];
        foreach (var secreto in secretos) Assert.DoesNotContain(secreto, texto);
    }
}
