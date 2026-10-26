using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Caching;

// Section 3, "Cache": the access check of every tenant and platform request reads from Valkey, and the database only on a miss. Every
// write that changes a cached answer removes it after its commit, so the next request sees the new answer at once, with no wait for the
// expiry. Each test reads first, so the old answer is in the cache, then writes, then reads again.
public sealed class AccessCheckCacheTests(Valkey valkey, Database database) : IClassFixture<Valkey>, IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = Host();
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The second request of a member finds the user's state and the membership in the cache: its access check reads no database.
    [Fact]
    public async Task CheckAccess_TheSameMemberTwice_ReadsTheDatabaseOnlyTheFirstTime()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await using var cached = await PipelineHost.StartAsync(new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = valkey.ConnectionString });
        await using var uncached = await PipelineHost.StartAsync();
        using var commands = new SqlCommands();

        var withCache = await commands.CountAsync(() => cached.CreateClient(member), client => client.GetAsync($"/v1/tenants/{tenant.Id}/ping", Cancellation));
        var withoutCache = await commands.CountAsync(() => uncached.CreateClient(member), client => client.GetAsync($"/v1/tenants/{tenant.Id}/ping", Cancellation));

        withCache.ShouldBe(0);
        withoutCache.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task CheckAccess_AfterTheMembersRoleChanges_GrantsTheNewRolesPermissionsAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await InvitationsAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await owner.ChangeMemberRoleAsync(tenant.Id, await _testData.UserIdOfAsync(member), "admin"))).EnsureSuccessStatusCode();
        var after = await InvitationsAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheRolesPermissionsChange_GrantsTheNewPermissionsAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read");
        var member = await _testData.AddMemberWithRoleAsync(tenant.Id, role);
        var before = await InvitationsAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(() => owner.UpdateRoleAsync(tenant.Id, role, "Billing", ["tenant.profile.read", "tenant.invitations.read"]))).EnsureSuccessStatusCode();
        var after = await InvitationsAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheMemberIsRemoved_FindsNoTenantAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await owner.RemoveMemberAsync(tenant.Id, await _testData.UserIdOfAsync(member)))).EnsureSuccessStatusCode();
        var after = await MembersAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.OK, HttpStatusCode.NotFound));
    }

    [Fact]
    public async Task CheckAccess_AfterTheUserIsSuspended_RefusesTheUserAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).PostAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(member)}/suspend", null, Cancellation))).EnsureSuccessStatusCode();
        var after = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        before.ShouldBe(HttpStatusCode.OK);
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.Forbidden, "user.suspended"));
    }

    [Fact]
    public async Task CheckAccess_AfterTheUserIsDeleted_RefusesTheUserAtOnce()
    {
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id);
        var before = await _api.CreateClient(member).GetAsync("/v1/me", Cancellation);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(member)}", Cancellation))).EnsureSuccessStatusCode();
        var after = await _api.CreateClient(member).GetAsync("/v1/me", Cancellation);

        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.Forbidden, "user.deleted"));
    }

    [Fact]
    public async Task CheckAccess_AfterTheTenantIsSuspended_RefusesTheMemberAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).SuspendTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var after = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        before.ShouldBe(HttpStatusCode.OK);
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.Forbidden, "tenant.suspended"));
    }

    [Fact]
    public async Task CheckAccess_AfterTheTenantIsClosed_RefusesTheMemberAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).CloseTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var after = await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation);

        before.ShouldBe(HttpStatusCode.OK);
        (after.StatusCode, await CodeOfAsync(after)).ShouldBe((HttpStatusCode.Forbidden, "tenant.closed"));
    }

    [Fact]
    public async Task CheckAccess_AfterTheTenantIsReopened_LetsTheMemberInAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = await SystemAdminAsync();
        (await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var after = await MembersAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheUserIsMadeASystemAdmin_OpensTheSystemDoorAtOnce()
    {
        var user = await _testData.AddUserAsync();
        var before = await UsersAsync(user);

        using var grant = new HttpRequestMessage(HttpMethod.Post, $"/v1/platform/users/{await _testData.UserIdOfAsync(user)}/system-admin");
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).SendAsync(grant, Cancellation))).EnsureSuccessStatusCode();
        var after = await UsersAsync(user);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheSystemAdminIsRevoked_ClosesTheSystemDoorAtOnce()
    {
        var systemAdmin = await _testData.AddSystemAdminAsync();
        var before = await UsersAsync(systemAdmin);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).DeleteAsync($"/v1/platform/users/{await _testData.UserIdOfAsync(systemAdmin)}/system-admin", Cancellation))).EnsureSuccessStatusCode();
        var after = await UsersAsync(systemAdmin);

        (before, after).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Forbidden));
    }

    [Fact]
    public async Task CheckAccess_AfterTheUserAcceptsAnInvitation_LetsTheNewMemberInAtOnce()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var secret = Guid.NewGuid().ToString("N");
        var user = await _testData.AddUserAsync(email: email);
        _api.Identity.AddAccount(user, email);
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddInvitationAsync(tenant.Id, email, secret: secret);
        var before = await MembersAsync(user, tenant.Id);

        (await _api.WaitingForMessagesAsync(() => _api.CreateClient(user).PostAsJsonAsync("/v1/me/invitations/accept", new { code = $"{tenant.Id}.{secret}" }, Cancellation))).EnsureSuccessStatusCode();
        var after = await MembersAsync(user, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheMemberIsMadeAnOwner_GrantsTheOwnersPermissionsAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var before = await InvitationsAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).MakeOwnerAsync(tenant.Id, await _testData.UserIdOfAsync(member)))).EnsureSuccessStatusCode();
        var after = await InvitationsAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheUserIsReactivated_LetsTheUserInAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = await SystemAdminAsync();
        var userId = await _testData.UserIdOfAsync(member);
        (await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/users/{userId}/suspend", null, Cancellation))).EnsureSuccessStatusCode();
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/users/{userId}/reactivate", null, Cancellation))).EnsureSuccessStatusCode();
        var after = await MembersAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    [Fact]
    public async Task CheckAccess_AfterTheTenantIsReactivated_LetsTheMemberInAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = await SystemAdminAsync();
        (await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var before = await MembersAsync(member, tenant.Id);

        (await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation))).EnsureSuccessStatusCode();
        var after = await MembersAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.Forbidden, HttpStatusCode.OK));
    }

    // Two processes share Valkey and nothing else. A suspension through one is seen at once by the other.
    [Fact]
    public async Task CheckAccess_AWriteThroughAnotherProcess_IsSeenAtOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        await using var other = Host();
        var before = await MembersAsync(member, tenant.Id);

        (await other.WaitingForMessagesAsync(async () => await other.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true).SuspendTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var after = await MembersAsync(member, tenant.Id);

        (before, after).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Forbidden));
    }

    // Row level security does not protect the cache, so its key holds the tenant: a member of two tenants has two entries, and a change in
    // one tenant leaves the other's entry as it was. The reader is a host of its own on the same Valkey; its access check of the other
    // tenant reads no database.
    [Fact]
    public async Task CheckAccess_OneMemberOfTwoTenantsAfterOneIsSuspended_KeepsTheOtherEntry()
    {
        var suspended = await _testData.AddTenantAsync();
        var kept = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(suspended.Id);
        await _testData.AddMemberAsync(kept.Id, member);
        await using var reader = await PipelineHost.StartAsync(new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = valkey.ConnectionString });
        await reader.CreateClient(member).GetAsync($"/v1/tenants/{suspended.Id}/ping", Cancellation);
        await reader.CreateClient(member).GetAsync($"/v1/tenants/{kept.Id}/ping", Cancellation);
        using var commands = new SqlCommands();

        (await _api.WaitingForMessagesAsync(async () => await (await SystemAdminAsync()).SuspendTenantAsync(suspended.Id))).EnsureSuccessStatusCode();
        var inTheSuspended = (await reader.CreateClient(member).GetAsync($"/v1/tenants/{suspended.Id}/ping", Cancellation)).StatusCode;
        var keptReads = await commands.CountAsync(() => reader.CreateClient(member), client => client.GetAsync($"/v1/tenants/{kept.Id}/ping", Cancellation));

        inTheSuspended.ShouldBe(HttpStatusCode.Forbidden);
        keptReads.ShouldBe(0);
    }

    // A cache that stops never fails a request: the access check reads the database, and answers what the rows say.
    [Fact]
    public async Task CheckAccess_ValkeyStopped_AnswersFromTheDatabase()
    {
        await using var stopping = new Valkey();
        await stopping.InitializeAsync();
        await using var api = Host(stopping.ConnectionString);
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var admin = api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var before = (await api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode;

        await stopping.StopAsync();
        var whileStopped = (await api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode;
        (await api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant.Id))).EnsureSuccessStatusCode();
        var afterTheSuspension = (await api.CreateClient(member).GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode;

        (before, whileStopped, afterTheSuspension).ShouldBe((HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.Forbidden));
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private ApiFactory Host(string? cache = null) =>
        new(database.ConnectionStringFor(DatabaseRoles.Application), settings: new Dictionary<string, string?> { ["ConnectionStrings:Cache"] = cache ?? valkey.ConnectionString });

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private async Task<HttpStatusCode> MembersAsync(string member, Guid tenantId) =>
        (await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenantId}/members", Cancellation)).StatusCode;

    private async Task<HttpStatusCode> InvitationsAsync(string member, Guid tenantId) =>
        (await _api.CreateClient(member).GetAsync($"/v1/tenants/{tenantId}/invitations", Cancellation)).StatusCode;

    private async Task<HttpStatusCode> UsersAsync(string user) =>
        (await _api.CreateClient(user, secondFactor: true).GetAsync("/v1/platform/users", Cancellation)).StatusCode;
}
