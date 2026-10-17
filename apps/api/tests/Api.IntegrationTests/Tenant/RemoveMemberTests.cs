using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Tenant;

// DELETE /v1/tenants/{tenantId}/members/{userId}: an Owner or an Admin removes a member. Only an Owner removes an Owner, nobody removes
// themselves, and the last Owner stays (K1). The removed member loses access at once (T7).
public sealed class RemoveMemberTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Owner", "Member", "member")]
    [InlineData("Owner", "Admin", "admin")]
    [InlineData("Admin", "Member", "member")]
    [InlineData("Admin", "Admin", "admin")]
    public async Task RemoveMember_AMemberTheActorMayRemove_RemovesItAndRecordsTheRoleItHad(string actorRole, string memberRole, string recordedRole)
    {
        var tenant = await _catalog.AddTenantAsync();
        var actor = await _catalog.AddMemberAsync(tenant.Id, role: actorRole);
        var member = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id, role: memberRole));

        var removed = await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor).RemoveMemberAsync(tenant.Id, member));

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsMemberAsync(tenant.Id, member)).ShouldBe(0);
        (await _audit.TenantLogAsync(member))
            .ShouldBe($"{tenant.Id}:member.removed|User|{await _catalog.UserIdOfAsync(actor)}|User|{member}|-");
        (await _audit.TenantDetailsAsync(member)).ShouldBe($$"""{"role": "{{recordedRole}}"}""");
    }

    // An Owner removes another Owner when the tenant keeps one.
    [Fact]
    public async Task RemoveMember_AnOwnerRemovesAnotherOwner_RemovesIt()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));

        var removed = await _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, other);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsMemberAsync(tenant.Id, other)).ShouldBe(0);
    }

    // section 6, "Member management", check 3.
    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public async Task RemoveMember_Themselves_IsForbiddenAndStays(string role)
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var actor = await _catalog.AddMemberAsync(tenant.Id, role: role);
        var actorId = await _catalog.UserIdOfAsync(actor);

        var removed = await _api.CreateClient(actor).RemoveMemberAsync(tenant.Id, actorId);

        removed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(removed)).ShouldBe("membership.cannot_remove_self");
        (await IsMemberAsync(tenant.Id, actorId)).ShouldBe(1);
    }

    // section 6, "Member management", check 4.
    [Fact]
    public async Task RemoveMember_AnAdminRemovesAnOwner_IsForbiddenAndTheOwnerStays()
    {
        var tenant = await _catalog.AddTenantAsync();
        await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var admin = await _catalog.AddMemberAsync(tenant.Id, role: "Admin");
        var owner = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));

        var removed = await _api.CreateClient(admin).RemoveMemberAsync(tenant.Id, owner);

        removed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(removed)).ShouldBe("membership.owner_requires_owner");
        (await IsMemberAsync(tenant.Id, owner)).ShouldBe(1);
    }

    // T3, T7: the tenant door reads the membership on every request, so the removed member's next request finds no tenant, and their
    // tenant list no longer holds it.
    [Fact]
    public async Task RemoveMember_TheRemovedMembersNextRequest_IsNotFoundAndTheTenantLeavesTheirList()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _catalog.AddMemberAsync(tenant.Id);
        var client = _api.CreateClient(member);
        (await client.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, await _catalog.UserIdOfAsync(member))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await client.GetAsync($"/v1/tenants/{tenant.Id}/members", Cancellation)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var tenants = await client.GetFromJsonAsync<JsonElement>("/v1/me/tenants", Cancellation);
        tenants.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ShouldNotContain(tenant.Id);
    }

    [Fact]
    public async Task RemoveMember_UnknownUser_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var removed = await _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, Guid.NewGuid());

        removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(removed)).ShouldBe("membership.not_found");
    }

    // Row level security hides another tenant's member, so their id answers as an unknown one.
    [Fact]
    public async Task RemoveMember_AnotherTenantsMember_IsNotFoundAndStays()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var stranger = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(other.Id));

        var removed = await _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, stranger);

        removed.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(removed)).ShouldBe("membership.not_found");
        (await IsMemberAsync(other.Id, stranger)).ShouldBe(1);
    }

    [Fact]
    public async Task RemoveMember_AsAMember_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id);
        var other = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id));

        var removed = await _api.CreateClient(member).RemoveMemberAsync(tenant.Id, other);

        removed.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsMemberAsync(tenant.Id, other)).ShouldBe(1);
    }

    // K6: the only two Owners remove each other at the same moment. The tenant row lock runs the two removals one after the other. The
    // second runs for a user who is no longer a member, to whom the tenant does not exist (T3), so one Owner remains.
    [Fact]
    public async Task RemoveMember_TheOnlyTwoOwnersRemoveEachOtherAtOnce_LeaveOneOwner()
    {
        var tenant = await _catalog.AddTenantAsync();
        var one = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var other = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var oneId = await _catalog.UserIdOfAsync(one);
        var otherId = await _catalog.UserIdOfAsync(other);

        var responses = await database.AtOnceAsync(
            tenant.Id,
            () => _api.CreateClient(one).RemoveMemberAsync(tenant.Id, otherId),
            () => _api.CreateClient(other).RemoveMemberAsync(tenant.Id, oneId));

        responses.Select(response => response.StatusCode).Order().ShouldBe([HttpStatusCode.NoContent, HttpStatusCode.NotFound]);
        (await CodeOfAsync(responses.Single(response => response.StatusCode == HttpStatusCode.NotFound))).ShouldBe("tenant.not_found");
        (await OwnersOfAsync(tenant.Id)).ShouldBe(1);
    }

    // K3: an entry holds ids and the role, never the member's name or email address.
    [Fact]
    public async Task RemoveMember_AMemberWithAProfile_LeavesTheNameAndTheEmailOutOfTheAuditLog()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _catalog.UserIdOfAsync(
            await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(email: "grace@example.com", firstName: "Grace", lastName: "Hopper")));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, member));

        (await _audit.TenantLogAsync(member)).ShouldNotBeNull();
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "@")).ShouldBe(0);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "Grace")).ShouldBe(0);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "Hopper")).ShouldBe(0);
    }

    private Task<long> IsMemberAsync(Guid tenantId, Guid userId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}' AND user_id = '{userId}'");

    private Task<long> OwnersOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.memberships JOIN control_plane.roles ON roles.id = memberships.role_id
            WHERE memberships.tenant_id = '{tenantId}' AND roles.built_in = 'Owner'
            """);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
