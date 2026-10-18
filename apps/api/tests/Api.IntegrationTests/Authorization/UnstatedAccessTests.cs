using System.Net;

namespace Api.IntegrationTests.Authorization;

// A5: an endpoint that states no access is refused at run time, never opened.
[RuntimeHandlerCode]
public sealed class UnstatedAccessTests : IAsyncLifetime
{
    private PipelineHost _host = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await PipelineHost.StartAsync();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task CallEndpoint_WithoutAnAccessStateAsASignedInUser_IsForbidden()
    {
        var response = await _host.CreateClient("user_ada").GetAsync("/v1/unstated", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CallEndpoint_WithoutAnAccessStateAndWithoutAToken_IsUnauthorized()
    {
        var response = await _host.CreateClient().GetAsync("/v1/unstated", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
