using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Tenants.StartTenantPurge;
using ControlPlane.Application.Tenants.WarnTenantPurge;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.Tenants;

// POST /v1/platform/tenants/{tenantId}/close: a system admin closes a tenant through the system door (K4). Closing cancels the tenant's
// pending invitations and withdraws their Clerk invitations, in the same transaction and under the tenant's lock (K6h). A change of
// status is announced, and Audit records it under the tenant; a request that changes nothing announces nothing. ClosedTenantTests
// follows what a closed tenant means to its members.
public sealed class CloseTenantTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    // The clock starts at the real now: a closure schedules its warning and its purge weeks after it, by Wolverine's clock, so neither
    // comes during a run on the shared database (section 9, rule 10).
    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private SignUpInvitations SignUps => new(_api, _testData, database);

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
        var tenant = await _testData.AddTenantAsync(status: status);

        var response = await (await SystemAdminAsync()).CloseTenantAsync(tenant.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (body.GetProperty("id").GetGuid(), body.GetProperty("status").GetString(), body.GetProperty("closedAt").GetDateTimeOffset())
            .ShouldBe((tenant.Id, "closed", _time.GetUtcNow()));
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
    }

    // K4: the closure schedules, in its transaction, the warning at the closure plus the reopen period less the warning's lead, and the
    // purge's start at the closure plus the reopen period. Each carries the tenant and the closure.
    [Fact]
    public async Task CloseTenant_ATenant_SchedulesTheWarningAndThePurgeWithTheClosure()
    {
        var tenant = await _testData.AddTenantAsync();
        var closedAt = _time.GetUtcNow();

        var scheduled = await ScheduledByTheClosureAsync(tenant.Id, await SystemAdminAsync());

        scheduled.ShouldBe(
        [
            (new WarnTenantPurgeCommand(tenant.Id, closedAt), closedAt + TimeSpan.FromDays(23)),
            (new StartTenantPurgeCommand(tenant.Id, closedAt), closedAt + TimeSpan.FromDays(30)),
        ],
        ignoreOrder: true);
    }

    // K4: a reopening cancels no scheduled message. The first closure's warning and purge find the tenant closed at another moment and do
    // nothing; the second closure schedules its own, and its warning reaches the Owner.
    [Fact]
    public async Task CloseTenant_ReopenedAndClosedAgain_SchedulesItsOwnAndTheFirstClosuresDoNothing()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddUserAsync(email: $"{Guid.NewGuid():N}@acme.test");
        await _testData.AddMemberAsync(tenant.Id, owner, role: "Owner");
        var admin = await SystemAdminAsync();
        var first = await ScheduledByTheClosureAsync(tenant.Id, admin);
        _time.Advance(TimeSpan.FromMinutes(1));
        await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenant.Id));
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = await ScheduledByTheClosureAsync(tenant.Id, admin);

        await DeliverAsync(tenant.Id, first.Select(message => message.Message));
        var firstsWarnedNobody = (_api.Email.Sent.Count, await PurgeWarningsAsync(tenant.Id), await _testData.TenantStatusOfAsync(tenant.Id));
        await DeliverAsync(tenant.Id, second.Select(message => message.Message).OfType<WarnTenantPurgeCommand>());

        second.Select(message => message.Message).ShouldBe(
            [new WarnTenantPurgeCommand(tenant.Id, _time.GetUtcNow()), new StartTenantPurgeCommand(tenant.Id, _time.GetUtcNow())], ignoreOrder: true);
        firstsWarnedNobody.ShouldBe((0, 0L, "Closed"));
        _api.Email.Sent.ShouldHaveSingleItem().To.ShouldBe((await _testData.UserOfAsync(owner)).Email);
        (await PurgeWarningsAsync(tenant.Id)).ShouldBe(1L);
    }

    // Closing sets a state (I1): a closed tenant answers as it is, keeps the moment of its first closure, and writes nothing again.
    [Fact]
    public async Task CloseTenant_Twice_KeepsTheFirstMomentAndIsRecordedOnceInEachLog()
    {
        var tenant = await _testData.AddTenantAsync();
        var admin = await SystemAdminAsync();
        var closedAt = _time.GetUtcNow();
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));
        _time.Advance(TimeSpan.FromDays(1));

        var again = await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant.Id));

        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ClosedAtOfAsync(again)).ShouldBe(closedAt);
        (await AuditEntryCountAsync(tenant.Id)).ShouldBe(2);
    }

    // K2: closing cancels every pending invitation, expired or not, in the same transaction; a used or a cancelled one stays as it is.
    [Fact]
    public async Task CloseTenant_WithInvitations_CancelsEveryPendingOne()
    {
        var tenant = await _testData.AddTenantAsync();
        var pending = await _testData.AddInvitationAsync(tenant.Id);
        var expired = await _testData.AddInvitationAsync(tenant.Id, expiresAt: _time.GetUtcNow().AddDays(-1));
        var accepted = await _testData.AddInvitationAsync(tenant.Id, status: "Accepted");

        (await (await SystemAdminAsync()).CloseTenantAsync(tenant.Id)).StatusCode.ShouldBe(HttpStatusCode.OK);

        (await _testData.InvitationStatusOfAsync(pending)).ShouldBe("Cancelled");
        (await _testData.InvitationStatusOfAsync(expired)).ShouldBe("Cancelled");
        (await _testData.InvitationStatusOfAsync(accepted)).ShouldBe("Accepted");
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // K6h: an invitation passed the tenant door while the tenant was active, then waited for the tenant's lock while
    // a system admin closed the tenant. Under the lock it finds the tenant closed, refuses, and leaves no pending invitation on it.
    [Fact]
    public async Task CloseTenantAndInviteMember_AtOnceWithTheClosureFirst_LeaveNoPendingInvitation()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await SystemAdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.CloseTenantAsync(tenant.Id), () => owner.InviteMemberAsync(tenant.Id, $"{Guid.NewGuid():N}@example.com"));

        responses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.closed");
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(0);
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // The other order: the invitation takes the lock first and is created, then the closure cancels it.
    [Fact]
    public async Task CloseTenantAndInviteMember_AtOnceWithTheInvitationFirst_LeaveNoPendingInvitation()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var admin = await SystemAdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => owner.InviteMemberAsync(tenant.Id, $"{Guid.NewGuid():N}@example.com"), () => admin.CloseTenantAsync(tenant.Id));

        (responses[0].StatusCode, responses[1].StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.OK));
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
        await _summaries.ShouldMatchTheRowsAsync(tenant.Id);
    }

    // K6g: every tenant command that locks the tenant row asks again, under the lock, whether the tenant is active. A command that
    // waited for the lock while the tenant was closed is refused and changes nothing.
    [Theory]
    [InlineData("InviteMember")]
    [InlineData("RevokeInvitation")]
    [InlineData("ChangeMemberRole")]
    [InlineData("RemoveMember")]
    [InlineData("CreateRole")]
    [InlineData("UpdateRole")]
    [InlineData("DeleteRole")]
    public async Task TenantCommand_WaitingWhileTheTenantCloses_IsForbiddenAndChangesNothing(string command)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id));
        var invitation = await _testData.AddInvitationAsync(tenant.Id);
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        var members = await MembersOfAsync(tenant.Id);
        var roles = await RolesOfAsync(tenant.Id);
        var admin = await SystemAdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.CloseTenantAsync(tenant.Id), () => Send(command, owner, tenant.Id, member, invitation, role));

        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.closed");
        (await MembersOfAsync(tenant.Id)).ShouldBe(members);
        (await RolesOfAsync(tenant.Id)).ShouldBe(roles);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
        (await PendingInvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    // K6g: the same under a suspension, which a member is told as tenant.suspended.
    [Theory]
    [InlineData("InviteMember")]
    [InlineData("RevokeInvitation")]
    [InlineData("ChangeMemberRole")]
    [InlineData("RemoveMember")]
    [InlineData("CreateRole")]
    [InlineData("UpdateRole")]
    [InlineData("DeleteRole")]
    public async Task TenantCommand_WaitingWhileTheTenantIsSuspended_IsForbiddenAndChangesNothing(string command)
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await _testData.UserIdOfAsync(await _testData.AddMemberAsync(tenant.Id));
        var invitation = await _testData.AddInvitationAsync(tenant.Id);
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.members.read");
        var members = await MembersOfAsync(tenant.Id);
        var roles = await RolesOfAsync(tenant.Id);
        var admin = await SystemAdminAsync();

        var responses = await database.OneAfterTheOtherAsync(
            tenant.Id, () => admin.SuspendTenantAsync(tenant.Id), () => Send(command, owner, tenant.Id, member, invitation, role));

        responses[1].StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.suspended");
        (await MembersOfAsync(tenant.Id)).ShouldBe(members);
        (await RolesOfAsync(tenant.Id)).ShouldBe(roles);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(1);
        (await _testData.InvitationStatusOfAsync(invitation)).ShouldBe("Pending");
    }

    [Fact]
    public async Task CloseTenant_WithPendingInvitations_WithdrawsEachClerkInvitationOnce()
    {
        var (tenantId, owner) = await SignUps.TenantWithOwnerAsync();
        var (_, first) = await SignUps.InviteAsync(tenantId, owner);
        var (_, second) = await SignUps.InviteAsync(tenantId, owner);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenantId}/close", null, Cancellation));
        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenantId}/close", null, Cancellation));

        _api.Identity.Withdrawn.Where(withdrawn => withdrawn == first || withdrawn == second).ShouldBe([first, second], ignoreOrder: true);
    }

    private static Task<HttpResponseMessage> Send(string command, HttpClient owner, Guid tenantId, Guid member, Guid invitation, Guid role) => command switch
    {
        "CreateRole" => owner.CreateRoleAsync(tenantId, "Auditor", ["tenant.profile.read", "tenant.audit.read"]),
        "UpdateRole" => owner.UpdateRoleAsync(tenantId, role, "Finance", ["tenant.profile.read", "tenant.audit.read"]),
        "DeleteRole" => owner.DeleteRoleAsync(tenantId, role),
        "InviteMember" => owner.InviteMemberAsync(tenantId, $"{Guid.NewGuid():N}@example.com"),
        "RevokeInvitation" => owner.DeleteAsync($"/v1/tenants/{tenantId}/invitations/{invitation}", Cancellation),
        "ChangeMemberRole" => owner.ChangeMemberRoleAsync(tenantId, member, "admin"),
        "RemoveMember" => owner.RemoveMemberAsync(tenantId, member),
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };

    private static async Task<DateTimeOffset> ClosedAtOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("closedAt").GetDateTimeOffset();

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    // The messages the closure scheduled, with the moment each is scheduled for; Wolverine keeps them for later.
    private async Task<List<(object Message, DateTimeOffset? At)>> ScheduledByTheClosureAsync(Guid tenantId, HttpClient admin)
    {
        var session = await _api.TrackMessagesAsync(() => admin.CloseTenantAsync(tenantId));
        return [.. session.Scheduled.Envelopes().Select(envelope => (envelope.Message!, envelope.ScheduledTime))];
    }

    // Delivers the messages now, in the tenant, as Wolverine delivers a scheduled message when its moment comes.
    private async Task DeliverAsync(Guid tenantId, IEnumerable<object> messages)
    {
        foreach (var message in messages)
        {
            await _api.WaitingForMessagesAsync(async () =>
            {
                using var scope = _api.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), message, Cancellation);
                return tenantId;
            });
        }
    }

    private Task<long> PurgeWarningsAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = 'tenant.purge_warned'");

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private Task<string?> MembersOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(user_id || ':' || role_id, ', ' ORDER BY user_id) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'");

    // Every role of the tenant with its name and permissions, so a command that changed a role, or added or deleted one, shows.
    private Task<string?> RolesOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(roles.id || ':' || roles.name || ':' || coalesce(granted.permissions, '-'), ', ' ORDER BY roles.id)
            FROM control_plane.roles
            LEFT JOIN (SELECT role_id, string_agg(permission, ' ' ORDER BY permission) AS permissions FROM control_plane.role_permissions GROUP BY role_id) AS granted
                ON granted.role_id = roles.id
            WHERE roles.tenant_id = '{tenantId}'
            """);

    private Task<long> InvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<long> PendingInvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}' AND status = 'Pending'");

    private Task<long> AuditEntryCountAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}') + (SELECT count(*) FROM audit.platform_entries WHERE target_id = '{tenantId}')");
}
