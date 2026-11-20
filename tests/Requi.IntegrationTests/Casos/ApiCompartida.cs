using System.Net.Http.Headers;
using System.Net.Http.Json;
using Requi.IntegrationTests.Identidad;

namespace Requi.IntegrationTests.Casos;

/// <summary>Una sola API con PostgreSQL para todas las clases de esta colección.</summary>
[CollectionDefinition(Nombre)]
public sealed class ApiCompartida : ICollectionFixture<ApiConBase>
{
    public const string Nombre = "API compartida";
}

/// <summary>Un HttpClient con el access token de un rol, obtenido con el flujo OIDC real.</summary>
public static class Sesiones
{
    public static async Task<HttpClient> ComoAsync(ApiConBase api, string rol)
    {
        var tokens = await new FlujoOidc(api).IniciarSesion(ApiConBase.CorreoDe(rol));
        var cliente = api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return cliente;
    }

    public static async Task<T> LeerAsync<T>(this HttpResponseMessage r)
    {
        if (!r.IsSuccessStatusCode) throw new Xunit.Sdk.XunitException($"{(int)r.StatusCode}: {await r.Content.ReadAsStringAsync()}");
        return (await r.Content.ReadFromJsonAsync<T>())!;
    }
}
