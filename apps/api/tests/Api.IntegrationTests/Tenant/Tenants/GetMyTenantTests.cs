using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Tenants;

// GET /v1/tenants/{tenantId}: the tenant in the route, for its members, with their role in it and its permissions. It passes the tenant
// door as every tenant route does.
public sealed class GetMyTenantTests(Database database) : IAsyncLifetime
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

    // The app shell and Settings › General: the tenant, when it was created, and the caller's role and its permissions, sorted. The web
    // shows an action only when the list holds its permission; every endpoint still checks the permission itself (A1).
    [Theory]
    [InlineData("Owner", "owner")]
    [InlineData("Admin", "admin")]
    [InlineData("Member", "member")]
    public async Task GetMyTenant_AMemberOfEveryBuiltInRole_ReturnsTheTenantTheirRoleAndWhenItWasCreated(string role, string name)
    {
        var createdAt = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var tenant = await _catalog.AddTenantAsync(name: "Profile Ltd", createdAt: createdAt);
        var member = await _catalog.AddMemberAsync(tenant.Id, role: role);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("id").GetGuid(), found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Profile Ltd", tenant.Slug, "active"));
        Roles.Of(found).ShouldBe(Roles.BuiltIn(name));
        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(createdAt);
        found.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "role", "permissions", "createdAt"], ignoreOrder: true);
    }

    // The list is the role's permissions in code (BuiltInRoles), in ordinal order: an Owner and an Admin hold the tenant pool, a Member
    // reads the members and the tenant.
    [Theory]
    [InlineData("Owner", new[]
    {
        Permissions.TenantAuditRead, Permissions.TenantInvitationsCreate, Permissions.TenantInvitationsDelete, Permissions.TenantInvitationsRead,
        Permissions.TenantMembersDelete, Permissions.TenantMembersRead, Permissions.TenantMembersUpdate, Permissions.TenantProfileRead,
    })]
    [InlineData("Admin", new[]
    {
        Permissions.TenantAuditRead, Permissions.TenantInvitationsCreate, Permissions.TenantInvitationsDelete, Permissions.TenantInvitationsRead,
        Permissions.TenantMembersDelete, Permissions.TenantMembersRead, Permissions.TenantMembersUpdate, Permissions.TenantProfileRead,
    })]
    [InlineData("Member", new[] { Permissions.TenantMembersRead, Permissions.TenantProfileRead })]
    public async Task GetMyTenant_AMemberOfEveryBuiltInRole_ReturnsTheRolesPermissionsSorted(string role, string[] permissions)
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: role);

        var found = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(permissions);
    }

    // The list is for the screen only: a Member whose list lacks a permission is still refused by the endpoint that needs it.
    [Fact]
    public async Task GetMyTenant_AMemberWithoutAPermission_IsStillRefusedByTheEndpointThatNeedsIt()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");
        var client = _api.CreateClient(member);

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);
        var invitations = await client.GetAsync($"/v1/tenants/{tenant.Id}/invitations", Cancellation);

        found.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldNotContain(Permissions.TenantInvitationsRead);
        invitations.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // T3: a tenant the caller cannot enter looks the same as one that does not exist.
    [Fact]
    public async Task GetMyTenant_AsAUserWhoIsNotAMember_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var outsider = await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, role: "Owner");

        var response = await _api.CreateClient(outsider).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_UnknownTenantId_IsNotFound()
    {
        var user = await _catalog.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_ASuspendedTenant_IsForbiddenAndToldWhy()
    {
        var tenant = await _catalog.AddTenantAsync(status: "Suspended");
        var member = await _catalog.AddMemberAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.suspended");
    }
}
