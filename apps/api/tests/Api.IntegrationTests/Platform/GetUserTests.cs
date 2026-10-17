using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// GET /v1/platform/users/{userId}: one catalog user for the system admin, with their memberships in every tenant. The memberships are
// under row level security, so the read declares the user (R11), as a user's deletion does, and reads them in one query. Whether the user
// is a tenant's only Owner comes from the tenant's summary (R12): the suspend dialog warns with it, and the delete refusal names those
// tenants.
public sealed class GetUserTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Created = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A sole Owner, an Owner with a co-Owner, an Admin of a suspended tenant and a Member of a closed one, by the tenant's name.
    [Fact]
    public async Task GetUser_AMemberOfSeveralTenants_ReturnsTheUserAndEachMembershipByTenantName()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _catalog.AddUserAsync(email: email, firstName: "Ada", lastName: "Lovelace", createdAt: Created);
        var alone = await _catalog.AddTenantAsync(name: "Alpha Ltd");
        var shared = await _catalog.AddTenantAsync(name: "Bravo Ltd");
        var suspended = await _catalog.AddTenantAsync(name: "Charlie Ltd", status: "Suspended");
        var closed = await _catalog.AddTenantAsync(name: "Delta Ltd", status: "Closed", closedAt: DateTimeOffset.UtcNow);
        var joined = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        await _catalog.AddMemberAsync(closed.Id, user, role: "Member", joinedAt: joined);
        await _catalog.AddMemberAsync(suspended.Id, user, role: "Admin", joinedAt: joined);
        await _catalog.AddMemberAsync(shared.Id, user, role: "Owner", joinedAt: joined);
        await _catalog.AddMemberAsync(shared.Id, role: "Owner");
        await _catalog.AddMemberAsync(alone.Id, user, role: "Owner", joinedAt: joined);
        await SummarizeAsync(alone.Id, shared.Id, suspended.Id, closed.Id);
        var id = await _catalog.UserIdOfAsync(user);

        var response = await (await AdminAsync()).GetAsync($"/v1/platform/users/{id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        found.EnumerateObject().Select(field => field.Name).ShouldBe(
            ["id", "email", "firstName", "lastName", "status", "isSystemAdmin", "membershipCount", "createdAt", "suspendedAt", "suspendedBy", "memberships"],
            ignoreOrder: true);
        (found.GetProperty("id").GetGuid(), found.GetProperty("email").GetString(), found.GetProperty("firstName").GetString(), found.GetProperty("lastName").GetString())
            .ShouldBe((id, email, "Ada", "Lovelace"));
        (found.GetProperty("status").GetString(), found.GetProperty("isSystemAdmin").GetBoolean(), found.GetProperty("membershipCount").GetInt32())
            .ShouldBe(("active", false, 4));
        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Created);
        found.GetProperty("memberships")[0].EnumerateObject().Select(field => field.Name).ShouldBe(
            ["tenantId", "tenantName", "tenantSlug", "tenantStatus", "role", "joinedAt", "soleOwner"], ignoreOrder: true);
        Memberships(found).ShouldBe(
        [
            (alone.Id, "Alpha Ltd", alone.Slug, "active", "owner", joined, true),
            (shared.Id, "Bravo Ltd", shared.Slug, "active", "owner", joined, false),
            (suspended.Id, "Charlie Ltd", suspended.Slug, "suspended", "admin", joined, false),
            (closed.Id, "Delta Ltd", closed.Slug, "closed", "member", joined, false),
        ]);
    }

    [Fact]
    public async Task GetUser_AUserWithNoMembership_HasAnEmptyListAndNoSuspension()
    {
        var id = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com"));

        var found = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("memberships").GetArrayLength().ShouldBe(0);
        found.GetProperty("membershipCount").GetInt32().ShouldBe(0);
        found.GetProperty("suspendedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("suspendedBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The system admin who suspended the user is named as the catalog names them now.
    [Fact]
    public async Task GetUser_ASuspendedUser_SaysWhenAndNamesTheSystemAdminWhoSuspendedThem()
    {
        var admin = await _catalog.AddUserAsync(firstName: "Sys", lastName: "Admin");
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{admin}'");
        var client = _api.CreateClient(admin, secondFactor: true);
        var id = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync());
        (await client.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation)).EnsureSuccessStatusCode();

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("status").GetString().ShouldBe("suspended");
        found.GetProperty("suspendedAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
        found.GetProperty("suspendedBy").ToString().ShouldBe("""{"firstName":"Sys","lastName":"Admin","deleted":false}""");
    }

    // A reactivation forgets who suspended the user.
    [Fact]
    public async Task GetUser_AUserSuspendedAndReactivated_HasNoSuspension()
    {
        var client = await AdminAsync();
        var id = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync());
        (await client.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/v1/platform/users/{id}/reactivate", null, Cancellation)).EnsureSuccessStatusCode();

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("status").GetString().ShouldBe("active");
        found.GetProperty("suspendedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("suspendedBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A version before step 20l reactivates a user by clearing suspended_at only, during a deploy: who suspended them is no longer true.
    [Fact]
    public async Task GetUser_ReactivatedByAVersionThatKeptTheSuspendingAdmin_HasNoSuspension()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var user = await _catalog.AddUserAsync();
        await database.ScalarAsync<object>(
            $"UPDATE control_plane.users SET suspended_by = (SELECT id FROM control_plane.users WHERE external_id = '{admin}') WHERE external_id = '{user}'");

        var found = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{await _catalog.UserIdOfAsync(user)}", Cancellation);

        found.GetProperty("suspendedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("suspendedBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A8: the deletion took the user out of every tenant and erased the email address and the names.
    [Fact]
    public async Task GetUser_ADeletedUser_HasNoEmailNoNamesAndNoMemberships()
    {
        var tenant = await _catalog.AddTenantAsync();
        var user = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Gone"));
        var id = await _catalog.UserIdOfAsync(user);
        var client = await AdminAsync();
        (await _api.WaitingForMessagesAsync(() => client.DeleteAsync($"/v1/platform/users/{id}", Cancellation))).EnsureSuccessStatusCode();

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        (found.GetProperty("status").GetString(), found.GetProperty("email").GetString(), found.GetProperty("firstName").GetString())
            .ShouldBe(("deleted", null, null));
        found.GetProperty("memberships").GetArrayLength().ShouldBe(0);
        found.GetProperty("membershipCount").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task GetUser_ASystemAdmin_SaysSo()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var id = await _catalog.UserIdOfAsync(admin);

        var found = await (await AdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("isSystemAdmin").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public async Task GetUser_AnIdNoUserHas_IsNotFound()
    {
        var response = await (await AdminAsync()).GetAsync($"/v1/platform/users/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("user.not_found");
    }

    // platform.users.read: the system door lets in staff only; a tenant's Owner is refused.
    [Fact]
    public async Task GetUser_AsATenantOwner_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner, secondFactor: true).GetAsync($"/v1/platform/users/{await _catalog.UserIdOfAsync(owner)}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The memberships are one query: a user in more tenants sends no more commands.
    [Fact]
    public async Task GetUser_AUserInMoreTenants_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var admin = await _catalog.AddSystemAdminAsync();
        var one = await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id);
        var three = await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id);
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, three);
        await _catalog.AddMemberAsync((await _catalog.AddTenantAsync()).Id, three);
        var oneId = await _catalog.UserIdOfAsync(one);
        var threeId = await _catalog.UserIdOfAsync(three);

        var forOne = await commands.CountAsync(() => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users/{oneId}", Cancellation));
        var forThree = await commands.CountAsync(() => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users/{threeId}", Cancellation));

        forThree.ShouldBe(forOne);
        forOne.ShouldBeGreaterThan(0);
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private async Task SummarizeAsync(params Guid[] tenantIds)
    {
        foreach (var tenantId in tenantIds)
        {
            await _catalog.SummarizeAsync(tenantId);
        }
    }

    private static List<(Guid TenantId, string? Name, string? Slug, string? Status, string? Role, DateTimeOffset JoinedAt, bool SoleOwner)> Memberships(JsonElement user) =>
    [
        .. user.GetProperty("memberships").EnumerateArray().Select(membership => (
            membership.GetProperty("tenantId").GetGuid(),
            membership.GetProperty("tenantName").GetString(),
            membership.GetProperty("tenantSlug").GetString(),
            membership.GetProperty("tenantStatus").GetString(),
            membership.GetProperty("role").GetString(),
            membership.GetProperty("joinedAt").GetDateTimeOffset(),
            membership.GetProperty("soleOwner").GetBoolean())),
    ];
}
