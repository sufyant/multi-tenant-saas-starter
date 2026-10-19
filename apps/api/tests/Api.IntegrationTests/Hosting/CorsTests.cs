using System.Net;

namespace Api.IntegrationTests.Hosting;

// Section 8 (API8): the API sends no CORS headers. No browser calls it: the browser calls only its app's BFF (F6). Without the
// headers, a browser keeps a script of another origin from reading an answer.
[RuntimeHandlerCode]
public sealed class CorsTests : IAsyncLifetime
{
    private const string Origin = "https://app.test";

    private PipelineHost _host = null!;

    public async ValueTask InitializeAsync() => _host = await PipelineHost.StartAsync();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Preflight_FromABrowserOrigin_GetsNoCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, "/v1/platform/tenants")
        {
            Headers =
            {
                { "Origin", Origin },
                { "Access-Control-Request-Method", "POST" },
                { "Access-Control-Request-Headers", "authorization,content-type,idempotency-key" },
            },
        };

        var response = await _host.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
        response.Headers.Contains("Access-Control-Allow-Methods").ShouldBeFalse();
    }

    [Fact]
    public async Task Request_FromABrowserOrigin_GetsNoCorsHeaders()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/v1/ping") { Headers = { { "Origin", Origin } } };

        var response = await _host.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }
}
