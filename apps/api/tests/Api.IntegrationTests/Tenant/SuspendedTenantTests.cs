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

    private const string Reason = "Suspected fraud";

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
            .PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation)).EnsureSuccessStatusCode();

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A member never sees the system admin's reason: not at the door, not in the tenant, not in its list of tenants, and not in the
    // tenant's audit log once the tenant is reactivated (K3).
    [Fact]
    public async Task ReadTenant_AsAMemberAfterASuspensionWithAReason_NeverShowsTheReason()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id, Reason));
        var atTheDoor = await owner.GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);
        var inTheList = await owner.GetAsync("/v1/me/tenants", Cancellation);
        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var theTenant = await owner.GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);
        var activity = await owner.GetAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);

        atTheDoor.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (theTenant.StatusCode, activity.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK));
        (await activity.Content.ReadAsStringAsync(Cancellation)).ShouldContain("tenant.suspended");
        (await atTheDoor.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain(Reason);
        (await inTheList.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain(Reason);
        (await theTenant.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain(Reason);
        (await activity.Content.ReadAsStringAsync(Cancellation)).ShouldNotContain(Reason);
    }

    private async Task SuspendAsync(Guid tenantId) =>
        (await _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true).SuspendTenantAsync(tenantId)).EnsureSuccessStatusCode();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
