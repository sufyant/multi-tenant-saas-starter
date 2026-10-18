using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// A closed tenant through the tenant door: its members are told it is closed, a stranger still sees no tenant (T3), the members still
// find it in their list of tenants, and a reopening lets them in again.
public sealed class ClosedTenantTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task EnterTenant_MemberOfAClosedTenant_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await (await SystemAdminAsync()).CloseTenantAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await CodeOfAsync(response)).ShouldBe("tenant.closed");
    }

    // T3: to anyone else, a closed tenant answers with the handlers' own tenant.not_found, the same body apart from the trace id.
    [Fact]
    public async Task EnterTenant_StrangerToAClosedTenant_AnswersTheHandlersTenantNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id);
        var stranger = _api.CreateClient(await _catalog.AddUserAsync());
        var admin = await SystemAdminAsync();
        await admin.CloseTenantAsync(tenant.Id);

        var response = await stranger.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);
        var fromTheHandler = await admin.GetAsync($"/v1/platform/tenants/{Guid.NewGuid()}", Cancellation);

        (response.StatusCode, fromTheHandler.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        var handlers = await ProblemBodies.WithoutTraceIdAsync(fromTheHandler);
        handlers.ShouldContain("\"code\":\"tenant.not_found\"");
        (await ProblemBodies.WithoutTraceIdAsync(response)).ShouldBe(handlers);
    }

    [Fact]
    public async Task EnterTenant_MemberAfterReopening_GetsIn()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);
        var admin = await SystemAdminAsync();
        await admin.CloseTenantAsync(tenant.Id);
        (await admin.ReopenTenantAsync(tenant.Id)).EnsureSuccessStatusCode();

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
