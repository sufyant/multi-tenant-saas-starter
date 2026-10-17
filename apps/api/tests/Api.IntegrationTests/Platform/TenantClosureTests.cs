using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// A system admin closes a tenant and reopens it within the reopen period, 30 days by default, through the system door (K4). Closing
// cancels the tenant's pending invitations in the same transaction. A change of status is announced, and Audit records it under the
// tenant; a request that changes nothing announces nothing. ClosedTenantTests follows what a closed tenant means to its members.
public sealed class TenantClosureTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly TimeSpan ReopenPeriod = TimeSpan.FromDays(30);

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 16, 9, 0, 0, TimeSpan.Zero));
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public async Task CloseTenant_ActiveOrSuspended_AnswersTheTenantClosedAtThatMoment(string status)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).CloseTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("id").GetGuid(), body.GetProperty("status").GetString(), body.GetProperty("closedAt").GetDateTimeOffset())
            .ShouldBe((tenant.Id, "closed", _time.GetUtcNow()));
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
    }

    // Closing sets a state (I1): a closed tenant answers as it is, keeps the moment of its first closure, and writes nothing again.
    [Fact]
    public async Task CloseTenant_Twice_KeepsTheFirstMomentAndIsRecordedOnceInEachLog()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        var closedAt = _time.GetUtcNow();
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));
        _time.Advance(TimeSpan.FromDays(1));

        var again = await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ClosedAtOfAsync(again)).ShouldBe(closedAt);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    public async Task CloseTenant_ProvisioningOrCancelled_IsAConflictAndChangesNothing(string status)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).CloseTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_active");
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    [Theory]
    [InlineData("close")]
    [InlineData("reopen")]
    public async Task CloseOrReopenTenant_UnknownTenant_IsNotFound(string action)
    {
        var response = await (await AdminAsync()).PostAsync($"/v1/platform/tenants/{Guid.NewGuid()}/{action}", null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_found");
    }

    // K3: the platform's log names the system admin; the tenant's own log says only that the platform acted.
    [Fact]
    public async Task CloseAndReopenTenant_EachChange_IsRecordedOnceInBothLogs()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddSystemAdminAsync();
        var client = _api.CreateClient(admin, secondFactor: true);

        await _api.WaitingForMessagesAsync(() => client.CloseTenantAsync(tenant.Id));
        _time.Advance(TimeSpan.FromMinutes(1));
        await _api.WaitingForMessagesAsync(() => client.ReopenTenantAsync(tenant.Id));

        var adminId = await _catalog.UserIdOfAsync(admin);
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBe(
            $"tenant.closed|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd, tenant.reopened|User|{adminId}|Tenant|{tenant.Id}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBe(
            $"{tenant.Id}:tenant.closed|Platform|-|Tenant|{tenant.Id}|Acme Ltd, {tenant.Id}:tenant.reopened|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
    }

    // K2: closing cancels every pending invitation, expired or not, in the same transaction; a used or a cancelled one stays as it is.
    [Fact]
    public async Task CloseTenant_WithInvitations_CancelsEveryPendingOne()
    {
        var tenant = await _catalog.AddTenantAsync();
        var pending = await _catalog.AddInvitationAsync(tenant.Id);
        var expired = await _catalog.AddInvitationAsync(tenant.Id, expiresAt: _time.GetUtcNow().AddDays(-1));
        var accepted = await _catalog.AddInvitationAsync(tenant.Id, status: "Accepted");

        (await (await AdminAsync()).CloseTenantAsync(tenant.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _catalog.InvitationStatusOfAsync(pending)).ShouldBe("Cancelled");
        (await _catalog.InvitationStatusOfAsync(expired)).ShouldBe("Cancelled");
        (await _catalog.InvitationStatusOfAsync(accepted)).ShouldBe("Accepted");
    }

    // K2: an invitation the closure cancelled answers as a revoked one, and stays cancelled after a reopening.
    [Fact]
    public async Task AcceptInvitation_AfterTheTenantClosed_IsNotFoundAndStaysSoAfterAReopening()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await AdminAsync();
        var invitee = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount(invitee, email);
        var invitation = await IdOfAsync(await _api.WaitingForMessagesAsync(() => owner.InviteMemberAsync(tenant.Id, email)));
        await admin.CloseTenantAsync(tenant.Id);

        var whileClosed = await AcceptAsync(invitee, _api.Email.CodeSentFor(invitation));
        await admin.ReopenTenantAsync(tenant.Id);
        var afterReopening = await AcceptAsync(invitee, _api.Email.CodeSentFor(invitation));

        (whileClosed.StatusCode, afterReopening.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        (await CodeOfAsync(whileClosed)).ShouldBe("invitation.not_found");
        (await CodeOfAsync(afterReopening)).ShouldBe("invitation.not_found");
    }

    // The period ends at the moment of the closure plus the period, so a second before it the tenant still comes back.
    [Fact]
    public async Task ReopenTenant_WithinThePeriod_AnswersTheTenantActive()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        await admin.CloseTenantAsync(tenant.Id);
        _time.Advance(ReopenPeriod - TimeSpan.FromSeconds(1));

        var response = await admin.ReopenTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("status").GetString(), body.GetProperty("closedAt").ValueKind).ShouldBe(("active", JsonValueKind.Null));
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe("Active");
    }

    [Fact]
    public async Task ReopenTenant_WhenThePeriodEnds_IsAConflictAndStaysClosed()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));
        _time.Advance(ReopenPeriod);

        var response = await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenant.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.reopen_period_ended");
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    // Reopening goes to active and forgets the closure and the reason of a suspension before it.
    [Fact]
    public async Task ReopenTenant_ClosedAfterASuspension_ForgetsTheReason()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await AdminAsync();
        await admin.SuspendTenantAsync(tenant.Id);
        var closed = await admin.CloseTenantAsync(tenant.Id);

        var reopened = await admin.ReopenTenantAsync(tenant.Id);

        (await SuspensionReasonOfAsync(closed)).ShouldBe("Unpaid invoices");
        (await SuspensionReasonOfAsync(reopened)).ShouldBeNull();
    }

    // Reopening sets a state (I1): an active tenant, as a first reopening leaves it, answers as it is and writes nothing.
    [Fact]
    public async Task ReopenTenant_Active_AnswersAsItIsAndRecordsNothing()
    {
        var tenant = await _catalog.AddTenantAsync();

        var response = await _api.WaitingForMessagesAsync(async () => await (await AdminAsync()).ReopenTenantAsync(tenant.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("active");
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(0);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Cancelled")]
    [InlineData("Suspended")]
    public async Task ReopenTenant_NeitherClosedNorActive_IsAConflictAndChangesNothing(string status)
    {
        var tenant = await _catalog.AddTenantAsync(status: status);

        var response = await (await AdminAsync()).ReopenTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(response)).ShouldBe("tenant.not_closed");
        (await _catalog.TenantStatusOfAsync(tenant.Id)).ShouldBe(status);
    }

    // K6: an invitation passed the tenant door while the tenant was active, then waited for the tenant's lock while
    // a system admin closed the tenant. Under the lock it finds the tenant closed, refuses, and leaves no pending invitation on it.
    [Fact]
    public async Task CloseTenantAndInviteMember_AtOnceWithTheClosureFirst_LeaveNoPendingInvitation()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await AdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.CloseTenantAsync(tenant.Id), () => owner.InviteMemberAsync(tenant.Id, $"{Guid.NewGuid():N}@example.com"));

        responses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.closed");
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    // The other order: the invitation takes the lock first and is created, then the closure cancels it.
    [Fact]
    public async Task CloseTenantAndInviteMember_AtOnceWithTheInvitationFirst_LeaveNoPendingInvitation()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await AdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => owner.InviteMemberAsync(tenant.Id, $"{Guid.NewGuid():N}@example.com"), () => admin.CloseTenantAsync(tenant.Id));

        (responses[0].StatusCode, responses[1].StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.OK));
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
    }

    // K6: every tenant command that locks the tenant row asks again, under the lock, whether the tenant is active. A command that
    // waited for the lock while the tenant was closed is refused and changes nothing.
    [Theory]
    [InlineData("InviteMember")]
    [InlineData("RevokeInvitation")]
    [InlineData("ChangeMemberRole")]
    [InlineData("RemoveMember")]
    public async Task TenantCommand_WaitingWhileTheTenantCloses_IsForbiddenAndChangesNothing(string command)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id));
        var invitation = await _catalog.AddInvitationAsync(tenant.Id);
        var members = await MembersOfAsync(tenant.Id);
        var admin = await AdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.CloseTenantAsync(tenant.Id), () => Send(command, owner, tenant.Id, member, invitation));

        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.closed");
        (await MembersOfAsync(tenant.Id)).ShouldBe(members);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    // K6: the same under a suspension, which a member is told as tenant.suspended.
    [Theory]
    [InlineData("InviteMember")]
    [InlineData("RevokeInvitation")]
    [InlineData("ChangeMemberRole")]
    [InlineData("RemoveMember")]
    public async Task TenantCommand_WaitingWhileTheTenantIsSuspended_IsForbiddenAndChangesNothing(string command)
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _catalog.UserIdOfAsync(await _catalog.AddMemberAsync(tenant.Id));
        var invitation = await _catalog.AddInvitationAsync(tenant.Id);
        var members = await MembersOfAsync(tenant.Id);
        var admin = await AdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.SuspendTenantAsync(tenant.Id), () => Send(command, owner, tenant.Id, member, invitation));

        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.suspended");
        (await MembersOfAsync(tenant.Id)).ShouldBe(members);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
        (await _catalog.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    private static Task<HttpResponseMessage> Send(string command, HttpClient owner, Guid tenantId, Guid member, Guid invitation) => command switch
    {
        "InviteMember" => owner.InviteMemberAsync(tenantId, $"{Guid.NewGuid():N}@example.com"),
        "RevokeInvitation" => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitation}", Cancellation),
        "ChangeMemberRole" => owner.ChangeMemberRoleAsync(tenantId, member, "admin"),
        "RemoveMember" => owner.RemoveMemberAsync(tenantId, member),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<HttpResponseMessage> AcceptAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName = "Ada", lastName = "Lovelace" }, Cancellation);

    private Task<string?> MembersOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(user_id || ':' || role_id, ', ' ORDER BY user_id) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'");

    private Task<long> InvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<long> PendingInvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}' AND status = 'Pending'");

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");

    private static async Task<Guid> IdOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    private static async Task<DateTimeOffset> ClosedAtOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("closedAt").GetDateTimeOffset();

    private static async Task<string?> SuspensionReasonOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("suspensionReason").GetString();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
