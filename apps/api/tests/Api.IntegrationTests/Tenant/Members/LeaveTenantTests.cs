using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant.Members;

// DELETE /v1/tenants/{tenantId}/membership: a member leaves the active tenant by themselves. The last Owner stays (K1). The member loses
// access at once (T7), and nobody is told (the maintainer's decisions of 2026-11-07).
public sealed class LeaveTenantTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly NotificationRows _notifications = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    // The tenant keeps an Owner in every case. The member acted and is the target (K3); the details hold the role they had.
    [Theory]
    [InlineData("Member", "member")]
    [InlineData("Admin", "admin")]
    [InlineData("Owner", "owner")]
    public async Task LeaveTenant_AMemberWhoIsNotTheLastOwner_LeavesAndRecordsTheRoleTheyHad(string role, string recordedRole)
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id, role: role);
        var memberId = await _testData.UserIdOfAsync(member);

        var left = await _api.WaitingForMessagesAsync(() => _api.CreateClient(member).LeaveTenantAsync(tenant.Id));

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsMemberAsync(tenant.Id, memberId)).ShouldBe(0);
        (await _audit.TenantLogAsync(memberId)).ShouldBe($"{tenant.Id}:member.left|User|{memberId}|User|{memberId}|-");
        (await _audit.TenantDetailsAsync(memberId))
            .ShouldBe($$"""{"role": "{{recordedRole}}", "roleId": "{{await _testData.RoleIdOfAsync(tenant.Id, role)}}", "roleBuiltIn": true}""");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // Every role reads the tenant's profile (role.profile_read_required), so every member may leave, a custom role's too.
    [Fact]
    public async Task LeaveTenant_ACustomRoleWithOnlyTheProfileRead_Leaves()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberWithRoleAsync(tenant.Id, await _testData.AddCustomRoleAsync(tenant.Id, "Viewer", "tenant.profile.read"));

        var left = await _api.CreateClient(member).LeaveTenantAsync(tenant.Id);

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsMemberAsync(tenant.Id, await _testData.UserIdOfAsync(member))).ShouldBe(0);
    }

    // K11: the leave counts the membership down on the user, in its own transaction.
    [Fact]
    public async Task LeaveTenant_AMemberOfTwoTenants_CountsOneMembershipDown()
    {
        var tenant = await _testData.AddTenantAsync();
        var other = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync(other.Id, member);

        var left = await _api.CreateClient(member).LeaveTenantAsync(tenant.Id);

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.StoredMembershipCountOfAsync(member), await _testData.CountedMembershipsOfAsync(member)).ShouldBe((1L, 1L));
    }

    // K1: the last Owner cannot leave while another member stays.
    [Fact]
    public async Task LeaveTenant_TheLastOwnerWithOtherMembers_IsConflictAndStays()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, role: "Admin");

        var left = await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).LeaveTenantAsync(tenant.Id));

        left.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(left)).ShouldBe("member.last_owner");
        (await IsMemberAsync(tenant.Id, await _testData.UserIdOfAsync(owner))).ShouldBe(1);
        (await _audit.TenantLogAsync(await _testData.UserIdOfAsync(owner))).ShouldBeNull();
    }

    // A tenant's only member is its last Owner, so they cannot leave either (the maintainer's decision of 2026-11-07).
    [Fact]
    public async Task LeaveTenant_TheOnlyMember_IsConflictAndStays()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var left = await _api.CreateClient(owner).LeaveTenantAsync(tenant.Id);

        left.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(left)).ShouldBe("member.last_owner");
        (await IsMemberAsync(tenant.Id, await _testData.UserIdOfAsync(owner))).ShouldBe(1);
    }

    // T3, T7: the tenant door reads the membership on every request, so the member's next request finds no tenant, and their tenant list
    // no longer holds it. A second leave is such a request.
    [Fact]
    public async Task LeaveTenant_TheMembersNextRequest_IsNotFoundAndTheTenantLeavesTheirList()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var client = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id));

        var left = await client.LeaveTenantAsync(tenant.Id);

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await client.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.LeaveTenantAsync(tenant.Id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var tenants = await client.GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);
        tenants.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldNotContain(tenant.Id);
    }

    // Only an active tenant: a member of a suspended or a closed one stays (the maintainer's decision of 2026-11-07).
    [Theory]
    [InlineData("Suspended", "tenant.suspended")]
    [InlineData("Closed", "tenant.closed")]
    public async Task LeaveTenant_ATenantThatIsNotActive_IsForbiddenAndStays(string status, string code)
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        await _testData.SetTenantStatusAsync(tenant.Id, status);

        var left = await _api.CreateClient(member).LeaveTenantAsync(tenant.Id);

        left.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(left)).ShouldBe(code);
        (await IsMemberAsync(tenant.Id, await _testData.UserIdOfAsync(member))).ShouldBe(1);
    }

    // Nobody is told: not the member, who acted (U3), and not the Owners or the Admins (the maintainer's decision of 2026-11-07).
    [Fact]
    public async Task LeaveTenant_AMemberLeaves_NotifiesNobody()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id, role: "Admin"));
        var member = await _testData.AddMemberAsync(tenant.Id);

        (await _api.WaitingForMessagesAsync(() => _api.CreateClient(member).LeaveTenantAsync(tenant.Id))).EnsureSuccessStatusCode();

        (await _notifications.OfAsync(owner, admin, await _testData.UserIdOfAsync(member))).ShouldBeEmpty();
    }

    // K6: the only two Owners leave at the same moment. The tenant row lock runs the two leaves one after the other, and the second one
    // counts one Owner, so one Owner remains.
    [Fact]
    public async Task LeaveTenant_TheOnlyTwoOwnersLeaveAtOnce_LeaveOneOwner()
    {
        var tenant = await _testData.AddTenantAsync();
        var one = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var responses = await database.AtOnceAsync(
            tenant.Id,
            () => _api.CreateClient(one).LeaveTenantAsync(tenant.Id),
            () => _api.CreateClient(other).LeaveTenantAsync(tenant.Id));

        responses.Select(response => response.StatusCode).Order().ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.Conflict]);
        (await CodeOfAsync(responses.Single(response => response.StatusCode == HttpStatusCode.Conflict))).ShouldBe("member.last_owner");
        (await OwnersOfAsync(tenant.Id)).ShouldBe(1);
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // K3: an entry holds ids and the role, never the member's name or email address.
    [Fact]
    public async Task LeaveTenant_AMemberWithAProfile_LeavesTheNameAndTheEmailOutOfTheAuditLog()
    {
        var tenant = await _testData.AddTenantAsync();
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: TestEmails.Of("grace@example.com"), name: "Grace Hopper"));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(member).LeaveTenantAsync(tenant.Id));

        (await _audit.TenantLogAsync(await _testData.UserIdOfAsync(member))).ShouldNotBeNull();
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "@")).ShouldBe(0);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "Grace")).ShouldBe(0);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "Hopper")).ShouldBe(0);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<long> IsMemberAsync(Guid tenantId, Guid userId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}' AND user_id = '{userId}'");

    private Task<long> OwnersOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.memberships JOIN control_plane.roles ON roles.id = memberships.role_id
            WHERE memberships.tenant_id = '{tenantId}' AND roles.built_in = 'Owner'
            """);
}
