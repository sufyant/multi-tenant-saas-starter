using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.RateLimiting;

public sealed class RateLimitingTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);
    private PipelineHost _host = null!;

    public async ValueTask InitializeAsync() =>
        _host = await PipelineHost.StartAsync(new Dictionary<string, string?> { ["Host:RateLimiting:PermitLimit"] = "1" });

    public async ValueTask DisposeAsync() => await _host.DisposeAsync();

    // API4: the bucket is the signed-in user, never the tenant, so one member cannot use up the others' requests.
    [Fact]
    public async Task CallTheApi_AUserOverTheirLimit_IsRejectedWithProblemDetailsAndARetryHint()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await GetAsync($"/v1/tenants/{tenant.Id}/ping", member);

        var rejected = await GetAsync($"/v1/tenants/{tenant.Id}/ping", member);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        (await rejected.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("code").GetString().ShouldBe("rate_limit.exceeded");
    }

    [Fact]
    public async Task CallTheApi_TwoMembersOfOneTenant_HaveABucketEach()
    {
        var tenant = await _testData.AddTenantAsync();
        var first = await _testData.AddMemberAsync(tenant.Id);
        var second = await _testData.AddMemberAsync(tenant.Id);
        await GetAsync($"/v1/tenants/{tenant.Id}/ping", first);

        var limited = await GetAsync($"/v1/tenants/{tenant.Id}/ping", first);
        var other = await GetAsync($"/v1/tenants/{tenant.Id}/ping", second);

        limited.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter.ShouldNotBeNull();
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The user is limited wherever they call, in every tenant they belong to and outside any tenant.
    [Fact]
    public async Task CallTheApi_AUserOverTheirLimitInOneTenant_IsLimitedInTheirOtherTenant()
    {
        var tenant = await _testData.AddTenantAsync();
        var other = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(other.Id, member);
        await GetAsync($"/v1/tenants/{tenant.Id}/ping", member);

        var response = await GetAsync($"/v1/tenants/{other.Id}/ping", member);

        response.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LimitRequests_UserNotAMember_IsLimitedAsAUser()
    {
        var tenant = await _testData.AddTenantAsync();
        var outsider = await _testData.AddUserAsync();
        await GetAsync($"/v1/tenants/{tenant.Id}/ping", outsider);

        var rejected = await GetAsync("/v1/ping", outsider);

        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task LimitRequests_OutsideATenant_EachUserHasTheirOwnLimit()
    {
        await GetAsync("/v1/ping", "user_1");
        await GetAsync("/v1/ping", "user_1");

        var other = await GetAsync("/v1/ping", "user_2");

        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LimitRequests_WithoutAUser_EachAddressHasItsOwnLimit()
    {
        await SendFromAsync("203.0.113.1");
        var limited = await SendFromAsync("203.0.113.1");

        var other = await SendFromAsync("203.0.113.2");

        limited.Response.StatusCode.ShouldBe(429);
        other.Response.StatusCode.ShouldBe(200);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string userId) =>
        _host.CreateClient(userId).GetAsync(path, TestContext.Current.CancellationToken);

    private Task<Microsoft.AspNetCore.Http.HttpContext> SendFromAsync(string ipAddress) =>
        _host.Server.SendAsync(context =>
        {
            context.Request.Method = "GET";
            context.Request.Path = "/v1/ping";
            context.Connection.RemoteIpAddress = IPAddress.Parse(ipAddress);
        }, TestContext.Current.CancellationToken);
}
