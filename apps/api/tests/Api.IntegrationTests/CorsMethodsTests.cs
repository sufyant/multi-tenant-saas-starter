using System.Net;
using Tenancy;

namespace Api.IntegrationTests;

// Section 8 (API8), on the host with the modules' endpoints: a browser's preflight is allowed every method a mapped endpoint uses, and
// no other. The allowed methods come from the mapped endpoints, so the test compares with them and not with a list of its own.
public sealed class CorsMethodsTests(Database database) : IAsyncLifetime
{
    private const string AllowedOrigin = "https://app.test";

    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["Host:Cors:AllowedOrigins:0"] = AllowedOrigin });
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // One endpoint for each method the API maps: reading the caller, accepting an invitation, changing preferences and deleting a user.
    [Theory]
    [InlineData("GET", "/v1/me")]
    [InlineData("POST", "/v1/me/invitations/accept")]
    [InlineData("PATCH", "/v1/me/preferences")]
    [InlineData("DELETE", "/v1/platform/users/0b8f2a4e-6c1d-4f3a-9e5b-7d2c8a1f4e60")]
    public async Task Preflight_ForAMethodAnEndpointUses_IsAllowedTheMethod(string method, string path)
    {
        var response = await PreflightAsync(method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([AllowedOrigin]);
        AllowedMethodsOf(response).ShouldContain(method);
    }

    [Fact]
    public async Task Preflight_FromAnAllowedOrigin_IsAllowedTheMethodsOfTheMappedEndpoints()
    {
        var response = await PreflightAsync("GET", "/v1/me");

        AllowedMethodsOf(response).ShouldBe(MappedMethods.Of(_api.Services), ignoreOrder: true);
    }

    // No endpoint of the API uses PUT, so a browser does not send it.
    [Fact]
    public async Task Preflight_ForAMethodNoEndpointUses_IsNotAllowedTheMethod()
    {
        var response = await PreflightAsync("PUT", "/v1/me/preferences");

        MappedMethods.Of(_api.Services).ShouldNotContain("PUT");
        AllowedMethodsOf(response).ShouldNotContain("PUT");
    }

    private static string[] AllowedMethodsOf(HttpResponseMessage response) =>
        [.. response.Headers.GetValues("Access-Control-Allow-Methods").Single().Split(',').Select(method => method.Trim())];

    private async Task<HttpResponseMessage> PreflightAsync(string method, string path)
    {
        using var request = new HttpRequestMessage(HttpMethod.Options, path)
        {
            Headers =
            {
                { "Origin", AllowedOrigin },
                { "Access-Control-Request-Method", method },
                { "Access-Control-Request-Headers", "authorization,content-type" },
            },
        };

        return await _api.CreateClient().SendAsync(request, TestContext.Current.CancellationToken);
    }
}
