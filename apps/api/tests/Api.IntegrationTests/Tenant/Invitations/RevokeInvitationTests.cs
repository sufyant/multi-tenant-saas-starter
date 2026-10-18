using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.ExpireSignUp;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Tenant.Invitations;

// DELETE /v1/tenants/{tenantId}/invitations/{invitationId}: an Owner or an Admin revokes a pending invitation. Its link stops working
// at once (K2).
public sealed class RevokeInvitationTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly TenantSummaries _summaries = new(database);
    private readonly AuditEntries _audit = new(database);
    private ApiFactory _api = null!;

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
        var tenant = await _catalog.AddTenantAsync();
        var revoker = await _catalog.AddMemberAsync(tenant.Id, role: revokerRole);
        var invitation = await _catalog.AddInvitationAsync(tenant.Id);

        var revoked = await _api.WaitingForMessagesAsync(() => RevokeAsync(revoker, tenant.Id, invitation));

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Cancelled");
        (await _audit.TenantLogAsync(invitation))
            .ShouldBe($"{tenant.Id}:invitation.revoked|User|{await _catalog.UserIdOfAsync(revoker)}|Invitation|{invitation}|-");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // An expired invitation is still pending until someone revokes it.
    [Fact]
    public async Task RevokeInvitation_PendingButExpired_CancelsIt()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _catalog.AddInvitationAsync(tenant.Id, expiresAt: DateTimeOffset.UtcNow.AddDays(-1));

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Cancelled");
    }

    // I1: setting a state twice changes nothing, so the second request writes no second record.
    [Fact]
    public async Task RevokeInvitation_Twice_AnswersNoContentAndRecordsOnce()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _catalog.AddInvitationAsync(tenant.Id);
        await _api.WaitingForMessagesAsync(() => RevokeAsync(owner, tenant.Id, invitation));

        var again = await _api.WaitingForMessagesAsync(() => RevokeAsync(owner, tenant.Id, invitation));

        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _audit.TenantLogAsync(invitation)).ShouldNotBeNull().Split(", ").ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RevokeInvitation_Accepted_IsAConflict()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _catalog.AddInvitationAsync(tenant.Id, status: "Accepted");

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(revoked)).ShouldBe("invitation.not_pending");
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Accepted");
    }

    [Fact]
    public async Task RevokeInvitation_UnknownId_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var revoked = await RevokeAsync(owner, tenant.Id, Guid.NewGuid());

        revoked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(revoked)).ShouldBe("invitation.not_found");
    }

    // Row level security hides another tenant's invitation, so its id answers as an unknown one.
    [Fact]
    public async Task RevokeInvitation_AnotherTenantsInvitation_IsNotFoundAndStaysPending()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
        var invitation = await _catalog.AddInvitationAsync(other.Id);

        var revoked = await RevokeAsync(owner, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    [Fact]
    public async Task RevokeInvitation_AsAMember_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var member = await _catalog.AddMemberAsync(tenant.Id, role: "Member");
        var invitation = await _catalog.AddInvitationAsync(tenant.Id);

        var revoked = await RevokeAsync(member, tenant.Id, invitation);

        revoked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    // K3: no record of either command holds an email address.
    [Fact]
    public async Task RevokeInvitation_AfterAnInvitation_LeavesTheEmailOutOfTheAuditLog()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");
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
        var (tenantId, owner) = await TenantWithOwnerAsync();
        var (invitationId, signUp) = await InviteAsync(tenantId, owner);

        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));
        await _api.WaitingForMessagesAsync(() => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == signUp).ShouldHaveSingleItem();
    }

    private Task<HttpResponseMessage> RevokeAsync(string userId, Guid tenantId, Guid invitationId) =>
        _api.CreateClient(userId).DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitationId}", Cancellation);

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<(Guid TenantId, HttpClient Owner)> TenantWithOwnerAsync()
    {
        var tenant = await _catalog.AddTenantAsync();
        return (tenant.Id, _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner")));
    }

    // An invitation to a person without an account, with its email sent: its id and the id of Clerk's invitation.
    private async Task<(Guid InvitationId, string SignUp)> InviteAsync(Guid tenantId, HttpClient owner, string? email = null)
    {
        email ??= NewEmail();
        var invited = await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenantId, email));
        var invitationId = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        return (invitationId, (await SignUpInvitationIdAsync(invitationId))!);
    }

    private Task<string?> SignUpInvitationIdAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT sign_up_invitation_id FROM control_plane.invitations WHERE id = '{invitationId}'");

    private static string NewEmail() => $"{Guid.NewGuid():N}@example.com";
}
