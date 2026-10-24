using System.Net;

namespace Api.IntegrationTests.Platform;

// The system door is a separate route group with its own authorization, and needs a second factor.
public sealed class PlatformRoutesTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private PipelineHost _host = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync() => _host = await PipelineHost.StartAsync();

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    [Fact]
    public async Task EnterSystemDoor_SystemAdminWithASecondFactor_IsLetIn()
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _host.CreateClient(admin, secondFactor: true).GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnterSystemDoor_SystemAdminWithoutASecondFactor_IsForbidden()
    {
        var admin = await _testData.AddSystemAdminAsync();

        var response = await _host.CreateClient(admin).GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    // Only the host's reading of fva may vouch for a second factor; a claim of that name inside the token proves nothing.
    [Fact]
    public async Task EnterSystemDoor_SecondFactorClaimedByTheTokenItself_IsForbidden()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var token = TestTokens.For(admin, secondFactor: false, extraClaims: new Dictionary<string, object> { ["second_factor_age"] = "0" });

        var response = await _host.CreateClientWith(token).GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The door asks for a second factor of any age. Only the grant and the revocation of a system admin ask for a fresh one (decision A
    // of 2026-10-23).
    [Fact]
    public async Task EnterSystemDoor_ASecondFactorOlderThanTheReverificationWindow_IsLetIn()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var token = TestTokens.WithSecondFactorAge(admin, 600);

        var response = await _host.CreateClientWith(token).GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EnterSystemDoor_UserNotASystemAdmin_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _host.CreateClient(owner, secondFactor: true).GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EnterSystemDoor_WithoutAToken_IsUnauthorized()
    {
        var response = await _host.CreateClient().GetAsync("/v1/platform/ping", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
