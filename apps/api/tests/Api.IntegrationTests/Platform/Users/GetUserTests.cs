using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Platform.Users;

// GET /v1/platform/users/{userId}: one user for the system admin, with their memberships in every tenant. The memberships are
// under row level security, so the read declares the user (R11), as a user's deletion does, and reads them in one query. Whether the user
// is a tenant's last Owner comes from the tenant's summary (R12): the suspend dialog warns with it, and the delete refusal names those
// tenants.
public sealed class GetUserTests(Database database) : IAsyncLifetime
{
    private static readonly DateTimeOffset Created = new(2026, 2, 3, 4, 5, 6, TimeSpan.Zero);

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // A last Owner, an Owner with a co-Owner, an Admin of a suspended tenant and a Member of a closed one, by the tenant's name.
    [Fact]
    public async Task GetUser_AMemberOfSeveralTenants_ReturnsTheUserAndEachMembershipByTenantName()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await _testData.AddUserAsync(email: email, firstName: "Ada", lastName: "Lovelace", createdAt: Created);
        var alone = await _testData.AddTenantAsync(name: "Alpha Ltd");
        var shared = await _testData.AddTenantAsync(name: "Bravo Ltd");
        var suspended = await _testData.AddTenantAsync(name: "Charlie Ltd", status: "Suspended");
        var closed = await _testData.AddTenantAsync(name: "Delta Ltd", status: "Closed", closedAt: DateTimeOffset.UtcNow);
        var joined = new DateTimeOffset(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
        await _testData.AddMemberAsync(closed.Id, user, role: "Member", joinedAt: joined);
        await _testData.AddMemberAsync(suspended.Id, user, role: "Admin", joinedAt: joined);
        await _testData.AddMemberAsync(shared.Id, user, role: "Owner", joinedAt: joined);
        await _testData.AddMemberAsync(shared.Id, role: "Owner");
        await _testData.AddMemberAsync(alone.Id, user, role: "Owner", joinedAt: joined);
        await SummarizeAsync(alone.Id, shared.Id, suspended.Id, closed.Id);
        var id = await _testData.UserIdOfAsync(user);

        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/users/{id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        found.EnumerateObject().Select(field => field.Name).ShouldBe(
            ["id", "email", "firstName", "lastName", "status", "isSystemAdmin", "isRootSystemAdmin", "systemAdminGrantedAt", "membershipCount", "createdAt", "suspendedAt", "suspendedBy", "memberships"],
            ignoreOrder: true);
        (found.GetProperty("id").GetGuid(), found.GetProperty("email").GetString(), found.GetProperty("firstName").GetString(), found.GetProperty("lastName").GetString())
            .ShouldBe((id, email, "Ada", "Lovelace"));
        (found.GetProperty("status").GetString(), found.GetProperty("isSystemAdmin").GetBoolean(), found.GetProperty("membershipCount").GetInt32())
            .ShouldBe(("active", false, 4));
        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(Created);
        found.GetProperty("memberships")[0].EnumerateObject().Select(field => field.Name).ShouldBe(
            ["tenantId", "tenantName", "tenantSlug", "tenantStatus", "role", "joinedAt", "lastOwner"], ignoreOrder: true);
        Memberships(found).ShouldBe(
        [
            (alone.Id, "Alpha Ltd", alone.Slug, "active", RoleAnswers.BuiltIn("owner"), joined, true),
            (shared.Id, "Bravo Ltd", shared.Slug, "active", RoleAnswers.BuiltIn("owner"), joined, false),
            (suspended.Id, "Charlie Ltd", suspended.Slug, "suspended", RoleAnswers.BuiltIn("admin"), joined, false),
            (closed.Id, "Delta Ltd", closed.Slug, "closed", RoleAnswers.BuiltIn("member"), joined, false),
        ]);
    }

    // A membership of a tenant whose purge has started stays until the purge's last step, with the tenant's status (K4).
    [Fact]
    public async Task GetUser_AMemberOfAPurgingTenant_ShowsTheTenantPurging()
    {
        var user = await _testData.AddUserAsync();
        var purging = await _testData.AddTenantAsync(name: "Echo Ltd", status: "Purging", closedAt: DateTimeOffset.UtcNow);
        await _testData.AddMemberAsync(purging.Id, user);
        await SummarizeAsync(purging.Id);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{await _testData.UserIdOfAsync(user)}", Cancellation);

        found.GetProperty("memberships").EnumerateArray().Select(membership => membership.GetProperty("tenantStatus").GetString()).ShouldBe(["purging"]);
    }

    [Fact]
    public async Task GetUser_AUserWithNoMembership_HasAnEmptyListAndNoSuspension()
    {
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com"));

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("memberships").GetArrayLength().ShouldBe(0);
        found.GetProperty("membershipCount").GetInt32().ShouldBe(0);
        found.GetProperty("suspendedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("suspendedBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The system admin who suspended the user is named as they are named now.
    [Fact]
    public async Task GetUser_ASuspendedUser_SaysWhenAndNamesTheSystemAdminWhoSuspendedThem()
    {
        var admin = await _testData.AddUserAsync(firstName: "Sys", lastName: "Admin");
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{admin}'");
        var client = _api.CreateClient(admin, secondFactor: true);
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
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
        var client = await SystemAdminAsync();
        var id = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        (await client.PostAsync($"/v1/platform/users/{id}/suspend", null, Cancellation)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/v1/platform/users/{id}/reactivate", null, Cancellation)).EnsureSuccessStatusCode();

        var found = await client.GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("status").GetString().ShouldBe("active");
        found.GetProperty("suspendedAt").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("suspendedBy").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A8: the deletion took the user out of every tenant and erased the email address and the names.
    [Fact]
    public async Task GetUser_ADeletedUser_HasNoEmailNoNamesAndNoMemberships()
    {
        var tenant = await _testData.AddTenantAsync();
        var user = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@example.com", firstName: "Gone"));
        var id = await _testData.UserIdOfAsync(user);
        var client = await SystemAdminAsync();
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
        var admin = await _testData.AddSystemAdminAsync();
        var id = await _testData.UserIdOfAsync(admin);
        var granted = await database.ScalarAsync<DateTime>($"SELECT granted_at FROM control_plane.system_admins WHERE user_id = '{id}'");

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        found.GetProperty("isSystemAdmin").GetBoolean().ShouldBeTrue();
        found.GetProperty("systemAdminGrantedAt").GetDateTimeOffset().UtcDateTime.ShouldBe(granted);
    }

    [Fact]
    public async Task GetUser_AnIdNoUserHas_IsNotFound()
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/users/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("user.not_found");
    }

    // platform.users.read: the system door lets in system admins only; a tenant's Owner is refused.
    [Fact]
    public async Task GetUser_AsATenantOwner_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var response = await _api.CreateClient(owner, secondFactor: true).GetAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(owner)}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // The memberships are one query: a user in more tenants sends no more commands.
    [Fact]
    public async Task GetUser_AUserInMoreTenants_SendsNoMoreSqlCommands()
    {
        using var commands = new SqlCommands();
        var admin = await _testData.AddSystemAdminAsync();
        var one = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);
        var three = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, three);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, three);
        var oneId = await _testData.UserIdOfAsync(one);
        var threeId = await _testData.UserIdOfAsync(three);

        var forOne = await commands.CountAsync(() => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users/{oneId}", Cancellation));
        var forThree = await commands.CountAsync(() => _api.CreateClient(admin, secondFactor: true), client => client.GetAsync($"/v1/platform/users/{threeId}", Cancellation));

        forThree.ShouldBe(forOne);
        forOne.ShouldBeGreaterThan(0);
    }

    // Root is read from the list on each request: the same system admin is a root under one list and not under another, and nothing is
    // written in between.
    [Fact]
    public async Task GetUser_TheSameSystemAdminUnderTwoLists_IsARootOnlyWhileListed()
    {
        var root = RootEmail();
        var admin = await _testData.AddSystemAdminAsync(email: root.ToLowerInvariant());
        var id = await _testData.UserIdOfAsync(admin);
        var reader = await _testData.AddSystemAdminAsync();
        await using var listed = WithRoots(root);
        await using var unlisted = WithRoots(RootEmail());

        var whileListed = await listed.CreateClient(reader, secondFactor: true).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);
        var afterwards = await unlisted.CreateClient(reader, secondFactor: true).GetFromJsonAsync<JsonElement>($"/v1/platform/users/{id}", Cancellation);

        (whileListed.GetProperty("isSystemAdmin").GetBoolean(), whileListed.GetProperty("isRootSystemAdmin").GetBoolean()).ShouldBe((true, true));
        (afterwards.GetProperty("isSystemAdmin").GetBoolean(), afterwards.GetProperty("isRootSystemAdmin").GetBoolean()).ShouldBe((true, false));
    }

    private static List<(Guid TenantId, string? Name, string? Slug, string? Status, string? Role, DateTimeOffset JoinedAt, bool LastOwner)> Memberships(JsonElement user) =>
    [
        .. user.GetProperty("memberships").EnumerateArray().Select(membership => (
            membership.GetProperty("tenantId").GetGuid(),
            membership.GetProperty("tenantName").GetString(),
            membership.GetProperty("tenantSlug").GetString(),
            membership.GetProperty("tenantStatus").GetString(),
            RoleAnswers.Of(membership),
            membership.GetProperty("joinedAt").GetDateTimeOffset(),
            membership.GetProperty("lastOwner").GetBoolean())),
    ];

    // An address in mixed case, so the list and the stored address meet only in their one form.
    private static string RootEmail() => $"Root.{Guid.NewGuid():N}@Example.com";

    private ApiFactory WithRoots(params string[] roots) =>
        new(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: roots.Select((root, index) => KeyValuePair.Create($"ControlPlane:SystemAdmins:RootEmails:{index}", (string?)root)).ToDictionary());

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private async Task SummarizeAsync(params Guid[] tenantIds)
    {
        foreach (var tenantId in tenantIds)
        {
            await _testData.SummarizeAsync(tenantId);
        }
    }
}
