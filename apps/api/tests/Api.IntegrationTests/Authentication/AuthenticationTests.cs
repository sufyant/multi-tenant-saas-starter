using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Authentication;

// Clerk only authenticates: the host accepts its session tokens and nothing else.
[RuntimeHandlerCode]
public sealed class AuthenticationTests : IAsyncLifetime
{
    private PipelineHost _host = null!;

    public async ValueTask InitializeAsync() => _host = await PipelineHost.StartAsync();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task Authenticate_ValidSessionToken_IdentifiesTheUserBySubject()
    {
        var response = await _host.CreateClient("user_ada").GetAsync("/v1/whoami", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<string>(TestContext.Current.CancellationToken)).ShouldBe("user_ada");
    }

    // Mobile clients send no Origin, so Clerk leaves the authorized party out.
    [Fact]
    public async Task Authenticate_TokenWithoutAuthorizedParty_IsAccepted()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", authorizedParty: null));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Authenticate_WithoutAToken_IsUnauthorizedWithProblemDetails()
    {
        var response = await _host.CreateClient().GetAsync("/v1/whoami", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        response.Headers.WwwAuthenticate.ToString().ShouldBe("Bearer");
        (await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().ShouldBe("auth.unauthenticated");
    }

    // API8: a refused token gets the answer of no token at all, apart from the trace id, so the body does not say why either.
    [Fact]
    public async Task Authenticate_TokenSignedWithAnUntrustedKey_AnswersAsWithoutAToken()
    {
        var refused = await WhoAmIAsync(TestTokens.For("user_ada", trusted: false));
        var anonymous = await _host.CreateClient().GetAsync("/v1/whoami", TestContext.Current.CancellationToken);

        (await ProblemBodies.WithoutTraceIdAsync(refused)).ShouldBe(await ProblemBodies.WithoutTraceIdAsync(anonymous));
        refused.Headers.WwwAuthenticate.ToString().ShouldBe(anonymous.Headers.WwwAuthenticate.ToString());
    }

    [Fact]
    public async Task Authenticate_ExpiredToken_IsUnauthorized()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", expiresIn: TimeSpan.FromMinutes(-1)));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticate_TokenFromAnotherIssuer_IsUnauthorized()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", issuer: "https://clerk.example"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticate_TokenSignedWithAnUntrustedKey_IsUnauthorized()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", trusted: false));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // API8: a refused token is not told why, in any environment.
    [Fact]
    public async Task Authenticate_TokenSignedWithAnUntrustedKey_DoesNotSayWhy()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", trusted: false));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.WwwAuthenticate.ShouldNotBeEmpty();
        response.Headers.WwwAuthenticate.ToString().ShouldNotContain("error_description");
    }

    // API2: Clerk signs JWT template tokens with the same keys and issuer, but only a session token carries the session's id.
    [Fact]
    public async Task Authenticate_TokenWithoutASession_IsUnauthorized()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", sessionToken: false));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Authenticate_TokenIssuedToAnUnknownParty_IsUnauthorized()
    {
        var response = await WhoAmIAsync(TestTokens.For("user_ada", authorizedParty: "https://evil.example"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> WhoAmIAsync(string token) =>
        _host.CreateClientWith(token).GetAsync("/v1/whoami", TestContext.Current.CancellationToken);
}
