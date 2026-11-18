using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Requi.IntegrationTests;

public class HealthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Live_responde_200_sin_base_de_datos()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Database", "Host=no-existe"))
            .CreateClient();

        var response = await client.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Ready_responde_503_si_la_base_no_contesta()
    {
        var client = factory.WithWebHostBuilder(b => b.UseSetting("ConnectionStrings:Database", "Host=127.0.0.1;Port=1;Timeout=2"))
            .CreateClient();

        var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
