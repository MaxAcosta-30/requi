using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Requi.Infrastructure.Identity;

namespace Requi.IntegrationTests.Identidad;

public record Tokens(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("id_token")] string? IdToken,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);

/// <summary>Hace lo mismo que el navegador y Angular: authorize → login → code → token.</summary>
public sealed partial class FlujoOidc(WebApplicationFactory<Program> api)
{
    private readonly HttpClient _navegador = api.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        HandleCookies = true,
    });

    public HttpClient Navegador => _navegador;

    public static (string Verificador, string Reto) Pkce()
    {
        var verificador = Base64Url(RandomNumberGenerator.GetBytes(32));
        return (verificador, Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verificador))));
    }

    public static string UrlAutorizar(string? reto, string scope = "openid profile roles offline_access requi-api")
    {
        var q = new Dictionary<string, string?>
        {
            ["client_id"] = SemillaIdentidad.ClienteWeb,
            ["response_type"] = "code",
            ["redirect_uri"] = ApiConBase.Redireccion,
            ["scope"] = scope,
            ["state"] = "estado-123",
        };
        if (reto is not null)
        {
            q["code_challenge"] = reto;
            q["code_challenge_method"] = "S256";
        }
        return QueryHelpers.AddQueryString("/connect/authorize", q);
    }

    public async Task<HttpResponseMessage> Login(string correo, string clave, string? returnUrl)
    {
        var pagina = await _navegador.GetStringAsync($"/cuenta/login?returnUrl={Uri.EscapeDataString(returnUrl ?? "")}");
        var antiforgery = CampoAntiforgery().Match(pagina);
        var formulario = new Dictionary<string, string>
        {
            [antiforgery.Groups["nombre"].Value] = WebUtility.HtmlDecode(antiforgery.Groups["valor"].Value),
            ["correo"] = correo,
            ["clave"] = clave,
            ["returnUrl"] = returnUrl ?? "",
        };
        return await _navegador.PostAsync("/cuenta/login", new FormUrlEncodedContent(formulario));
    }

    /// <summary>Flujo completo para un usuario: devuelve los tokens.</summary>
    public async Task<Tokens> IniciarSesion(string correo, string clave = ApiConBase.Clave)
    {
        var (verificador, reto) = Pkce();
        var autorizar = UrlAutorizar(reto);

        var sinSesion = await _navegador.GetAsync(autorizar);
        Assert.Equal(HttpStatusCode.Redirect, sinSesion.StatusCode);
        var returnUrl = QueryHelpers.ParseQuery(sinSesion.Headers.Location!.Query)["ReturnUrl"].ToString();

        var login = await Login(correo, clave, returnUrl);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);

        var conSesion = await _navegador.GetAsync(login.Headers.Location);
        Assert.Equal(HttpStatusCode.Redirect, conSesion.StatusCode);
        var callback = conSesion.Headers.Location!;
        Assert.StartsWith(ApiConBase.Redireccion, callback.ToString());
        var query = QueryHelpers.ParseQuery(callback.Query);
        Assert.Equal("estado-123", query["state"].ToString());

        var respuesta = await CanjearCodigo(query["code"].ToString(), verificador);
        respuesta.EnsureSuccessStatusCode();
        return (await respuesta.Content.ReadFromJsonAsync<Tokens>())!;
    }

    public Task<HttpResponseMessage> CanjearCodigo(string codigo, string? verificador)
    {
        var campos = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = codigo,
            ["redirect_uri"] = ApiConBase.Redireccion,
            ["client_id"] = SemillaIdentidad.ClienteWeb,
        };
        if (verificador is not null) campos["code_verifier"] = verificador;
        return _navegador.PostAsync("/connect/token", new FormUrlEncodedContent(campos));
    }

    public Task<HttpResponseMessage> Refrescar(string refreshToken) =>
        _navegador.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = SemillaIdentidad.ClienteWeb,
        }));

    public Task<HttpResponseMessage> Get(string ruta, string accessToken)
    {
        var solicitud = new HttpRequestMessage(HttpMethod.Get, ruta);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return _navegador.SendAsync(solicitud);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [GeneratedRegex("""<input type="hidden" name="(?<nombre>[^"]+)" value="(?<valor>[^"]*)">""")]
    private static partial Regex CampoAntiforgery();
}
