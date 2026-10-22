using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.OpenSignUp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Invitations;

// DELETE /v1/tenants/{tenantId}/invitations/{invitationId}: an Owner or an Admin revokes a pending invitation. Its link stops working
// at once (K2).
public sealed class RevokeInvitationTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private SignUpInvitations SignUps => new(_api, _testData, database);

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Owner")]
    [InlineData("Admin")]
    public async Task RevokeInvitation_Pending_CancelsItAndRecordsTheRevoker(string revokerRole)
    {
        var tenant = await _testData.AddTenantAsync();
        var revoker = await _testData.AddMemberAsync(tenant.Id, role: revokerRole);
        var invitation = await _testData.AddInvitationAsync(tenant.Id);

        var revoked = await _api.WaitingForMessagesAsync(() => RevokeAsync(revoker, tenant.Id, invitation));

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Cancelled");
        (await _audit.TenantLogAsync(invitation))
            .ShouldBe($"{tenant.Id}:invitation.revoked|User|{await _testData.UserIdOfAsync(revoker)}|Invitation|{invitation}|-");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // An expired invitation is still pending until someone revokes it.
    [Fact]
    public async Task RevokeInvitation_PendingButExpired_CancelsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _testData.AddInvitationAsync(tenant.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Cancelled");
    }

    // I1: setting a state twice changes nothing, so the second request writes no second record.
    [Fact]
    public async Task RevokeInvitation_Twice_AnswersNoContentAndRecordsOnce()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _testData.AddInvitationAsync(tenant.Id);
        await _api.WaitingForMessagesAsync(() => RevokeAsync(owner, tenant.Id, invitation));

        var again = await _api.WaitingForMessagesAsync(() => RevokeAsync(owner, tenant.Id, invitation));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.TenantLogAsync(invitation)).ShouldNotBeNull().Split(", ").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RevokeInvitation_Accepted_IsAConflict()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _testData.AddInvitationAsync(tenant.Id, status: "Accepted");

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(revoked)).ShouldBe("invitation.not_pending");
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Accepted");
    }

    [Fact]
    public async Task RevokeInvitation_UnknownId_IsNotFound()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");

        var revoked = await RevokeAsync(owner, tenant.Id, Guid.NewGuid());

        revoked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(revoked)).ShouldBe("invitation.not_found");
    }

    // Row level security hides another tenant's invitation, so its id answers as an unknown one.
    [Fact]
    public async Task RevokeInvitation_AnotherTenantsInvitation_IsNotFoundAndStaysPending()
    {
        var tenant = await _testData.AddTenantAsync();
        var other = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _testData.AddInvitationAsync(other.Id);

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    [Fact]
    public async Task RevokeInvitation_AsAMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Member");
        var invitation = await _testData.AddInvitationAsync(tenant.Id);

        var revoked = await RevokeAsync(member, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    // K3: no record of either command holds an email address.
    [Fact]
    public async Task RevokeInvitation_AfterAnInvitation_LeavesTheEmailOutOfTheAuditLog()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var email = $"{Guid.NewGuid():N}@example.com";
        var invited = await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).InviteMemberAsync(tenant.Id, email));
        var invitation = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

        await _api.WaitingForMessagesAsync(() => RevokeAsync(owner, tenant.Id, invitation));

        (await _audit.TenantLogAsync(invitation)).ShouldNotBeNull().Split(", ").Length.ShouldBe(2);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, email)).ShouldBe(0);
        (await _audit.TenantRecordsHoldingAsync(tenant.Id, "@")).ShouldBe(0);
    }

    [Fact]
    public async Task RevokeInvitation_WithAClerkInvitation_WithdrawsItOnce()
    {
        var (tenantId, owner) = await SignUps.TenantWithOwnerAsync();
        var (invitationId, signUp) = await SignUps.InviteAsync(tenantId, owner);

        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));
        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    // Decision 2 of step r4b: nobody revokes an invitation that gives a role stronger than their own.
    [Fact]
    public async Task RevokeInvitation_OfARoleStrongerThanTheRevokers_IsForbiddenAsAnEscalationAndStaysPending()
    {
        var tenant = await _testData.AddTenantAsync();
        var recruiter = await _testData.AddCustomRoleAsync(tenant.Id, "Recruiter", "tenant.invitations.delete", "tenant.members.read", "tenant.profile.read");
        var revoker = await _testData.AddMemberWithRoleAsync(tenant.Id, recruiter);
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, role: "Admin");

        var revoked = await RevokeAsync(revoker, tenant.Id, invitationId);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(revoked)).ShouldBe("role.escalation");
        (await _testData.InvitationStatusOfAsync(invitationId)).ShouldBe("Pending");
    }

    [Fact]
    public async Task RevokeInvitation_OfARoleTheRevokerHoldsExactly_CancelsIt()
    {
        var tenant = await _testData.AddTenantAsync();
        var recruiter = await _testData.AddCustomRoleAsync(tenant.Id, "Recruiter", "tenant.invitations.delete", "tenant.members.read", "tenant.profile.read");
        var revoker = await _testData.AddMemberWithRoleAsync(tenant.Id, recruiter);
        var invitationId = await _testData.AddInvitationAsync(tenant.Id);

        var revoked = await RevokeAsync(revoker, tenant.Id, invitationId);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _testData.InvitationStatusOfAsync(invitationId)).ShouldBe("Cancelled");
    }

    // Decision 2 of step r4b: an Admin does not revoke a pending invitation to the Owner role, which is stronger than the Admin's.
    [Fact]
    public async Task RevokeInvitation_AnAdminOnAPendingOwnerInvitation_IsForbiddenAsAnEscalation()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, role: "Owner");

        var revoked = await RevokeAsync(admin, tenant.Id, invitationId);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(revoked)).ShouldBe("role.escalation");
        (await _testData.InvitationStatusOfAsync(invitationId)).ShouldBe("Pending");
    }

    // I1: a cancelled invitation gives no role any more, so a revoker of a weaker role gets the answer of a second revocation.
    [Fact]
    public async Task RevokeInvitation_ACancelledInvitationOfAStrongerRole_AnswersNoContent()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, role: "Owner", status: "Cancelled");

        var revoked = await RevokeAsync(admin, tenant.Id, invitationId);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    // A finished invitation whose custom role was deleted names no role; revoking it again answers as before.
    [Fact]
    public async Task RevokeInvitation_ACancelledInvitationWhoseRoleWasDeleted_AnswersNoContent()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var roleId = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        var invitationId = await _testData.AddInvitationAsync(tenant.Id, status: "Cancelled", roleId: roleId);
        await database.ExecuteInTenantAsync(tenant.Id, $"DELETE FROM control_plane.roles WHERE id = '{roleId}'");

        var revoked = await RevokeAsync(owner, tenant.Id, invitationId);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<HttpResponseMessage> RevokeAsync(string userId, Guid tenantId, Guid invitationId) =>
        _api.CreateClient(userId).DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation);
}
