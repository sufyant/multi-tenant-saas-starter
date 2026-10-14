using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Authorization;

// A9: a suspended user is refused on every me and tenant endpoint, through the composed application. The user is suspended and
// reactivated through the system door.
public sealed class SuspendedUserTests(Database database) : IAsyncLifetime
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
    public async Task ListMyTenants_SuspendedUser_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync();
        var user = await _catalog.AddMemberAsync(tenant.Id);
        await SuspendAsync(user);

        var response = await _api.CreateClient(user).GetAsync("/v1/me/tenants", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await CodeOfAsync(response)).ShouldBe("user.suspended");
    }

    [Fact]
    public async Task AcceptInvitation_SuspendedUser_IsForbiddenAndJoinsNothing()
    {
        var user = await _catalog.AddUserAsync();
        await SuspendAsync(user);
        var email = $"{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount(user, email);
        var slug = $"tenant-{Guid.NewGuid():N}"[..20];
        await _api.WaitingForMessagesAsync(async () => await (await AdminAsync()).CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = email }));

        var response = await _api.CreateClient(user).PostAsJsonAsync("/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email) }, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(response)).ShouldBe("user.suspended");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT invitations.status FROM control_plane.invitations JOIN control_plane.tenants ON tenants.id = invitations.tenant_id WHERE tenants.slug = '{slug}'"))
            .ShouldBe("Pending");
    }

    [Fact]
    public async Task EnterTenant_SuspendedMember_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        await SuspendAsync(member);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(response)).ShouldBe("user.suspended");
    }

    // The user's own state comes first: it says nothing about the tenant.
    [Fact]
    public async Task EnterTenant_SuspendedMemberOfASuspendedTenant_IsToldTheUserIsSuspended()
    {
        var tenant = await _catalog.AddTenantAsync(status: "Suspended");
        var member = await _catalog.AddMemberAsync(tenant.Id);
        await SuspendAsync(member);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(response)).ShouldBe("user.suspended");
    }

    [Fact]
    public async Task ReactivateUser_TheUser_GetsInAgain()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);
        await SuspendAsync(member);
        (await (await AdminAsync()).PostAsync($"/v1/admin/users/{await _catalog.UserIdOfAsync(member)}/reactivate", null, Cancellation))
            .EnsureSuccessStatusCode();

        var tenants = await _api.CreateClient(member).GetAsync("/v1/me/tenants", Cancellation);
        var members = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        tenants.StatusCode.ShouldBe(HttpStatusCode.OK);
        members.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task SuspendAsync(string externalId) =>
        (await (await AdminAsync()).PostAsync($"/v1/admin/users/{await _catalog.UserIdOfAsync(externalId)}/suspend", null, Cancellation))
            .EnsureSuccessStatusCode();

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
