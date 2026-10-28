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
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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
        var tenant = await _testData.AddTenantAsync(name: "Profile Ltd", createdAt: createdAt);
        var member = await _testData.AddMemberAsync(tenant.Id, role: role);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("id").GetGuid(), found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Profile Ltd", tenant.Slug, "active"));
        RoleAnswers.Of(found).ShouldBe(RoleAnswers.BuiltIn(name));
        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(createdAt);
        found.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "role", "permissions", "createdAt", "planName", "entitlements", "usage"], ignoreOrder: true);
    }

    // The tenant web reads the plan's name and the effective value of every key, by key, as it reads the permissions: a feature is true or
    // false, a limit a number, and null is unlimited. For each limit it reads the tenant's count, so it adds no list up (D1). A Pro tenant
    // with one custom role, two members and one pending invitation.
    [Fact]
    public async Task GetMyTenant_AProTenant_AnswersThePlanItsEntitlementsAndTheCounts()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        await _testData.AddInvitationAsync(tenant.Id);
        await _testData.SummarizeAsync(tenant.Id);

        var found = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("planName").GetString().ShouldBe("Pro");
        found.GetProperty("entitlements").GetRawText().ShouldBe("""{"members.max":null,"roles.custom":true,"roles.custom.max":20}""");
        found.GetProperty("usage").GetRawText().ShouldBe("""{"members.max":3,"roles.custom.max":1}""");
    }

    // An override over the plan's value is the effective value the tenant reads.
    [Fact]
    public async Task GetMyTenant_AFreeTenantWithAnOverride_AnswersTheEffectiveValues()
    {
        var tenant = await _testData.AddTenantAsync(planId: SeedPlans.Free);
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        (await _api.WaitingForMessagesAsync(() => admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Max(12)))).EnsureSuccessStatusCode();

        var found = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("planName").GetString().ShouldBe("Free");
        found.GetProperty("entitlements").GetRawText().ShouldBe("""{"members.max":12,"roles.custom":false,"roles.custom.max":0}""");
    }

    // The list is the role's permissions in code (BuiltInRoles), in ordinal order: an Owner holds the tenant pool, an Admin all of it but
    // the Owners' own permission (K1) and the management of roles, a Member reads the members and the tenant.
    [Theory]
    [InlineData("Owner", new[]
    {
        Permissions.TenantAuditRead, Permissions.TenantInvitationsCreate, Permissions.TenantInvitationsDelete, Permissions.TenantInvitationsRead,
        Permissions.TenantMembersDelete, Permissions.TenantMembersRead, Permissions.TenantMembersUpdate, Permissions.TenantOwnersManage,
        Permissions.TenantProfileRead, Permissions.TenantRolesCreate, Permissions.TenantRolesDelete, Permissions.TenantRolesRead,
        Permissions.TenantRolesUpdate,
    })]
    [InlineData("Admin", new[]
    {
        Permissions.TenantAuditRead, Permissions.TenantInvitationsCreate, Permissions.TenantInvitationsDelete, Permissions.TenantInvitationsRead,
        Permissions.TenantMembersDelete, Permissions.TenantMembersRead, Permissions.TenantMembersUpdate, Permissions.TenantProfileRead,
        Permissions.TenantRolesRead,
    })]
    [InlineData("Member", new[] { Permissions.TenantMembersRead, Permissions.TenantProfileRead })]
    public async Task GetMyTenant_AMemberOfEveryBuiltInRole_ReturnsTheRolesPermissionsSorted(string role, string[] permissions)
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: role);

        var found = await _api.CreateClient(member).GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe(permissions);
    }

    // A3: a custom role's member gets that role's permissions, from its rows, and the door lets them in and keeps them out by them.
    [Fact]
    public async Task GetMyTenant_AMemberOfACustomRole_ReturnsItsPermissionsAndTheDoorAllowsAndRefusesByThem()
    {
        var tenant = await _testData.AddTenantAsync();
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Auditor", Permissions.TenantProfileRead, Permissions.TenantAuditRead);
        var client = _api.CreateClient(await _testData.AddMemberWithRoleAsync(tenant.Id, roleId));

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/tenants/{tenant.Id}", Cancellation);
        var allowed = await client.GetAsync($"/v1/tenants/{tenant.Id}/audit-log", Cancellation);
        var refused = await client.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        found.GetProperty("permissions").EnumerateArray().Select(permission => permission.GetString()).ShouldBe([Permissions.TenantAuditRead, Permissions.TenantProfileRead]);
        RoleAnswers.Of(found).ShouldBe(RoleAnswers.Custom("Auditor"));
        found.GetProperty("role").GetProperty("id").GetGuid().ShouldBe(roleId);
        allowed.StatusCode.ShouldBe(HttpStatusCode.OK);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The list is for the screen only: a Member whose list lacks a permission is still refused by the endpoint that needs it.
    [Fact]
    public async Task GetMyTenant_AMemberWithoutAPermission_IsStillRefusedByTheEndpointThatNeedsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Member");
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
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var outsider = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, role: "Owner");

        var response = await _api.CreateClient(outsider).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_UnknownTenantId_IsNotFound()
    {
        var user = await _testData.AddUserAsync();

        var response = await _api.CreateClient(user).GetAsync($"/v1/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetMyTenant_ASuspendedTenant_IsForbiddenAndToldWhy()
    {
        var tenant = await _testData.AddTenantAsync(status: "Suspended");
        var member = await _testData.AddMemberAsync(tenant.Id);

        var response = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.suspended");
    }
}
