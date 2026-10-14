using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// A suspended tenant through the tenant door: its members are told it is suspended, a stranger still sees no tenant (T3), and
// reactivation lets the members in again.
public sealed class SuspendedTenantTests(Database database) : IAsyncLifetime
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
    public async Task EnterTenant_MemberOfASuspendedTenant_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await SuspendAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await CodeOfAsync(response)).ShouldBe("tenant.suspended");
    }

    [Fact]
    public async Task EnterTenant_StrangerToASuspendedTenant_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id);
        var stranger = await _catalog.AddUserAsync();
        await SuspendAsync(tenant.Id);

        var response = await _api.CreateClient(stranger).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain("tenant.suspended");
    }

    [Fact]
    public async Task EnterTenant_MemberAfterReactivation_GetsIn()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);
        await SuspendAsync(tenant.Id);
        (await _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true)
            .PostAsync($"/v1/admin/tenants/{tenant.Id}/reactivate", null, Cancellation)).EnsureSuccessStatusCode();

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SuspendAsync(Guid tenantId) =>
        (await _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true)
            .PostAsync($"/v1/admin/tenants/{tenantId}/suspend", null, Cancellation)).EnsureSuccessStatusCode();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
