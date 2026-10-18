using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Audit.Application.Invitations.RecordInvitationRevoked;
using Audit.Application.Invitations.RecordOwnerInvited;
using Audit.Application.Members.RecordMemberInvited;
using Audit.Application.Members.RecordMemberRemoved;
using Audit.Application.Members.RecordMemberRoleChanged;
using Audit.Application.Tenants.RecordTenantClosed;
using Audit.Application.Tenants.RecordTenantCreated;
using Audit.Application.Tenants.RecordTenantReactivated;
using Audit.Application.Tenants.RecordTenantRenamed;
using Audit.Application.Tenants.RecordTenantReopened;
using Audit.Application.Tenants.RecordTenantSuspended;
using Audit.Application.Users.RecordUserDeleted;
using Audit.Application.Users.RecordUserMembershipRemoved;
using Audit.Application.Users.RecordUserPreferencesChanged;
using Audit.Application.Users.RecordUserRenamed;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Application.Invitations.InviteMember;
using ControlPlane.Application.Invitations.InviteOwner;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Invitations.RecordSignUp;
using ControlPlane.Application.Invitations.RevokeInvitation;
using ControlPlane.Application.Members.ChangeMemberRole;
using ControlPlane.Application.Members.RemoveMember;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Application.Tenants.CloseTenant;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.ReactivateTenant;
using ControlPlane.Application.Tenants.RenameTenant;
using ControlPlane.Application.Tenants.ReopenTenant;
using ControlPlane.Application.Tenants.SuspendTenant;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users.ChangePreferences;
using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Application.Users.DeleteUser;
using ControlPlane.Application.Users.ListUserMemberships;
using ControlPlane.Application.Users.RemoveUserMembership;
using ControlPlane.Application.Users.RenameUser;
using ControlPlane.Application.Users.SuspendUser;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Email;
using Notifications.Application.Invitations.SendInvitationEmail;
using Notifications.Application.Invitations.SendMemberInvitationEmail;
using Notifications.Contracts.Invitations;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Util;

namespace Api.IntegrationTests.Hosting;

// Section 1: one build output runs as two process types, chosen by Host:Role. The web host serves the API and runs the command a
// request sends; whatever that command sends on is handled by a worker host. The two hosts share a database of their own here, so no
// other test's host takes part.
[RuntimeHandlerCode]
public sealed class HostRoleTests(Database database)
{
    // The handlers of every tenant creation message after the first, as section 6 lists them.
    private static readonly HandlerRun[] HandlersAfterTheFirstStep =
    [
        new(typeof(ActivateTenantCommand), typeof(ActivateTenantHandler)),
        new(typeof(ActivateTenantReply), typeof(TenantCreationSaga)),
        new(typeof(TenantActivatedEvent), typeof(RecordTenantCreatedHandler)),
        new(typeof(OpenOwnerSignUpCommand), typeof(OpenOwnerSignUpHandler)),
        new(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
        new(typeof(OwnerInvitationReadyEvent), typeof(SendInvitationEmailHandler)),
        new(typeof(SendInvitationEmailReply), typeof(TenantCreationSaga)),
    ];

    private static readonly HandlerRun FirstStep = new(typeof(CreateTenantCommand), typeof(CreateTenantHandler));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_RunsEveryStepAfterTheFirstInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" });
        var tenantId = await IdOfAsync(created);
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([FirstStep]);
        runs.In("worker").Distinct().ShouldBe(HandlersAfterTheFirstStep, ignoreOrder: true);
        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
        worker.Email.Sent.ShouldHaveSingleItem().To.ShouldBe("owner@acme.test");
    }

    [Fact]
    public async Task CreateTenant_OnAWebHostAlone_WaitsUntilAWorkerStarts()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);
        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" });
        var tenantId = await IdOfAsync(created);
        var waiting = await SagaStateAsync(name, tenantId);
        var queued = await database.ScalarAsync<long>(
            """
            SELECT count(*) FROM (
                SELECT message_type FROM wolverine.wolverine_queue_messages
                UNION ALL SELECT message_type FROM wolverine.wolverine_outgoing_envelopes) AS waiting
            WHERE message_type = 'activate-tenant'
            """,
            database: name);

        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");

        waiting.ShouldBe("Activating");
        queued.ShouldBe(1L);
        runs.In("web").ShouldBe([FirstStep]);
        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
    }

    // O3, W4: the Audit handler of the activation runs beside the saga's, in the worker, once.
    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_WritesOneTenantCreatedAuditEntry()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var web = Host(name, "web", new HandlerRuns());
        await using var worker = Host(name, "worker", new HandlerRuns());
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var tenantId = await IdOfAsync(await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" }));
        await Waiting.UntilAsync(async () => await SagaStateAsync(name, tenantId) == "Completed");
        await Waiting.UntilAsync(async () => await TenantCreatedEntriesAsync(name, tenantId) > 0);

        (await SagaStateAsync(name, tenantId)).ShouldBe("Completed");
        (await TenantCreatedEntriesAsync(name, tenantId)).ShouldBe(1L);
    }

    // O3: a suspension is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task SuspendTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.SuspendTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendTenantCommand), typeof(SuspendTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantSuspendedEvent), typeof(RecordTenantSuspendedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
    }

    // O3: a closure is announced from the web host, and its Audit handler runs in the worker, once, in both logs.
    [Fact]
    public async Task CloseTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInEachLogInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var closed = await admin.CloseTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.closed") > 0);

        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(CloseTenantCommand), typeof(CloseTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantClosedEvent), typeof(RecordTenantClosedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.closed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.closed")).ShouldBe(1L);
    }

    // O3: a new name is announced from the web host, and its Audit handler runs in the worker, once, in both logs.
    [Fact]
    public async Task RenameTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInEachLogInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var renamed = await admin.RenameTenantAsync(tenant.Id, "Globex Corp");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.renamed") > 0);

        renamed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(RenameTenantCommand), typeof(RenameTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantRenamedEvent), typeof(RecordTenantRenamedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.renamed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.renamed")).ShouldBe(1L);
    }

    // O3: a reopening is announced from the web host, and its Audit handler runs in the worker, once, in both logs.
    [Fact]
    public async Task ReopenTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInEachLogInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var reopened = await admin.ReopenTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.reopened") > 0);

        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ReopenTenantCommand), typeof(ReopenTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantReopenedEvent), typeof(RecordTenantReopenedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.reopened")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.reopened")).ShouldBe(1L);
    }

    // O3, K3: a user's suspension is announced from the web host without a tenant, and its Audit handler runs in the worker, once, and
    // writes the platform's log.
    [Fact]
    public async Task SuspendUser_OnAWebAndAWorkerHost_WritesOnePlatformEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var userId = await catalog.UserIdOfAsync(await catalog.AddUserAsync());
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.PostAsync($"/v1/platform/users/{userId}/suspend", null, Cancellation);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, userId, "user.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendUserCommand), typeof(SuspendUserHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(UserSuspendedEvent), typeof(RecordUserSuspendedHandler))]);
        (await PlatformEntriesAsync(name, userId, "user.suspended")).ShouldBe(1L);
    }

    // O3, K3: a user's rename and deletion run in the web host, step by step for the deletion. Their Audit handlers run in the worker,
    // once each, and so does the identity provider's deletion, which has scheduled retries (W5).
    [Fact]
    public async Task RenameAndDeleteUser_OnAWebAndAWorkerHost_RecordAndDeleteAtTheIdentityProviderInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        var user = await catalog.AddMemberAsync(tenant.Id);
        var userId = await catalog.UserIdOfAsync(user);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var renamed = await admin.PatchAsJsonAsync($"/v1/platform/users/{userId}", new { firstName = "Ada", lastName = "Lovelace" }, Cancellation);
        var deleted = await admin.DeleteAsync($"/v1/platform/users/{userId}", Cancellation);
        await Waiting.UntilAsync(async () => !worker.Identity.Deleted.IsEmpty && await PlatformEntriesAsync(name, userId, "user.deleted") > 0
            && await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0 && await PlatformEntriesAsync(name, userId, "user.renamed") > 0);

        (renamed.StatusCode, deleted.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.NoContent));
        runs.In("web").ShouldBe(
            [
                new HandlerRun(typeof(RenameUserCommand), typeof(RenameUserHandler)),
                new HandlerRun(typeof(ListUserMembershipsQuery), typeof(ListUserMembershipsHandler)),
                new HandlerRun(typeof(RemoveUserMembershipCommand), typeof(RemoveUserMembershipHandler)),
                new HandlerRun(typeof(DeleteUserCommand), typeof(DeleteUserHandler)),
            ],
            ignoreOrder: true);
        runs.In("worker").ShouldBe(
            [
                new HandlerRun(typeof(UserRenamedEvent), typeof(RecordUserRenamedHandler)),
                new HandlerRun(typeof(UserMembershipRemovedEvent), typeof(RecordUserMembershipRemovedHandler)),
                new HandlerRun(typeof(UserDeletedEvent), typeof(RecordUserDeletedHandler)),
                new HandlerRun(typeof(DeleteIdentityAccountCommand), typeof(DeleteIdentityAccountHandler)),
            ],
            ignoreOrder: true);
        worker.Identity.Deleted.ShouldBe([user]);
        (await PlatformEntriesAsync(name, userId, "user.renamed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, userId, "user.deleted")).ShouldBe(1L);
        (await AuditEntriesAsync(name, tenant.Id, "member.removed")).ShouldBe(1L);
    }

    // O3, K3: a user changes their preferences in the web host. Audit records the change in the worker, once.
    [Fact]
    public async Task ChangePreferences_OnAWebAndAWorkerHost_RecordsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var user = await catalog.AddUserAsync();
        var userId = await catalog.UserIdOfAsync(user);

        var changed = await web.CreateClient(user).PatchAsJsonAsync("/v1/me/preferences", new { language = "ar", timeZone = "Asia/Dubai" }, Cancellation);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, userId, "user.preferences_changed") > 0);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ChangePreferencesCommand), typeof(ChangePreferencesHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(UserPreferencesChangedEvent), typeof(RecordUserPreferencesChangedHandler))]);
        (await PlatformEntriesAsync(name, userId, "user.preferences_changed")).ShouldBe(1L);
    }

    // O3, W4, W5: an invitation is created in the web host. Audit records its event in the worker, once. The identity provider's sign-up
    // runs in the worker, once, then the email: each message has one handler, so its retries may be scheduled. The language and the
    // lifetime travel through the PostgreSQL queue to the email.
    [Fact]
    public async Task InviteMember_OnAWebAndAWorkerHost_RecordsAndEmailsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        var owner = web.CreateClient(await catalog.AddMemberAsync(tenant.Id, await catalog.AddUserAsync(language: "tr"), role: "Owner"));

        var invited = await owner.InviteMemberAsync(tenant.Id, "grace@navy.test");
        await Waiting.UntilAsync(async () => worker.Email.Sent.Count > 0 && await AuditEntriesAsync(name, tenant.Id, "member.invited") > 0);

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([new HandlerRun(typeof(InviteMemberCommand), typeof(InviteMemberHandler))]);
        runs.In("worker").ShouldBe(
            [
                new HandlerRun(typeof(MemberInvitedEvent), typeof(RecordMemberInvitedHandler)),
                new HandlerRun(typeof(OpenMemberSignUpCommand), typeof(OpenMemberSignUpHandler)),
                new HandlerRun(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
                new HandlerRun(typeof(MemberInvitationReadyEvent), typeof(SendMemberInvitationEmailHandler)),
            ],
            ignoreOrder: true);
        var email = worker.Email.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe(("grace@navy.test", "Acme Ltd adlı kuruluşa davet edildiniz"));
        email.Text.ShouldEndWith("Bağlantı 7 gün geçerlidir.");
        (await AuditEntriesAsync(name, tenant.Id, "member.invited")).ShouldBe(1L);
    }

    // Step 20k: the platform's Owner invitation is accepted by the web host; its records in both logs, the identity provider's sign-up and
    // the email run in the worker, once each.
    [Fact]
    public async Task InviteOwner_OnAWebAndAWorkerHost_RecordsInBothLogsAndEmailsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        await catalog.AddInvitationAsync(tenant.Id, role: "Owner");
        var admin = web.CreateClient(await catalog.AddSystemAdminAsync(), secondFactor: true);

        var invited = await admin.InviteOwnerAsync(tenant.Id, "grace@navy.test");
        await Waiting.UntilAsync(async () =>
            worker.Email.Sent.Count > 0
            && await AuditEntriesAsync(name, tenant.Id, "tenant.owner_invited") > 0
            && await PlatformEntriesAsync(name, tenant.Id, "tenant.owner_invited") > 0);

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([new HandlerRun(typeof(InviteOwnerCommand), typeof(InviteOwnerHandler))]);
        runs.In("worker").ShouldBe(
            [
                new HandlerRun(typeof(OwnerInvitedEvent), typeof(RecordOwnerInvitedHandler)),
                new HandlerRun(typeof(OpenMemberSignUpCommand), typeof(OpenMemberSignUpHandler)),
                new HandlerRun(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
                new HandlerRun(typeof(MemberInvitationReadyEvent), typeof(SendMemberInvitationEmailHandler)),
            ],
            ignoreOrder: true);
        worker.Email.Sent.ShouldHaveSingleItem().To.ShouldBe("grace@navy.test");
        (await AuditEntriesAsync(name, tenant.Id, "tenant.owner_invited")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.owner_invited")).ShouldBe(1L);
    }

    // O3: a revocation is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task RevokeInvitation_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        var owner = web.CreateClient(await catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var invitation = await catalog.AddInvitationAsync(tenant.Id);

        var revoked = await owner.DeleteAsync($"/v1/tenants/{tenant.Id}/invitations/{invitation}", Cancellation);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "invitation.revoked") > 0);

        revoked.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe([new HandlerRun(typeof(RevokeInvitationCommand), typeof(RevokeInvitationHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(InvitationRevokedEvent), typeof(RecordInvitationRevokedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "invitation.revoked")).ShouldBe(1L);
    }

    // O3: a role change is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task ChangeMemberRole_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        var owner = web.CreateClient(await catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await catalog.UserIdOfAsync(await catalog.AddMemberAsync(tenant.Id));

        var changed = await owner.ChangeMemberRoleAsync(tenant.Id, member, "admin");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.role_changed") > 0);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ChangeMemberRoleCommand), typeof(ChangeMemberRoleHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(MemberRoleChangedEvent), typeof(RecordMemberRoleChangedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.role_changed")).ShouldBe(1L);
    }

    // O3: a removal is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task RemoveMember_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var catalog = new Catalog(database, name);
        var tenant = await catalog.AddTenantAsync();
        var owner = web.CreateClient(await catalog.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await catalog.UserIdOfAsync(await catalog.AddMemberAsync(tenant.Id));

        var removed = await owner.RemoveMemberAsync(tenant.Id, member);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe([new HandlerRun(typeof(RemoveMemberCommand), typeof(RemoveMemberHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(MemberRemovedEvent), typeof(RecordMemberRemovedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.removed")).ShouldBe(1L);
    }

    // O3: a reactivation is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task ReactivateTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new Catalog(database, name).AddTenantAsync(status: "Suspended");
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var reactivated = await admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated") > 0);

        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ReactivateTenantCommand), typeof(ReactivateTenantHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(TenantReactivatedEvent), typeof(RecordTenantReactivatedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated")).ShouldBe(1L);
    }

    // Spike T8 in the production layout (O3, W4): each handler of an event runs in a worker, in its own transaction. The one that
    // throws goes to the dead letter queue; the other commits its row once.
    [Fact]
    public async Task PublishAnEventWithTwoHandlers_OnAWebAndAWorkerHost_OneThrowsTheOtherCommitsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        var runs = new HandlerRuns();
        await using var web = ProbeHost(name, "web", runs);
        await using var worker = ProbeHost(name, "worker", runs);
        _ = worker.Services;
        var tenant = Guid.NewGuid();
        var value = $"ping-{Guid.NewGuid():N}";

        await using (var scope = web.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>()
                .PublishAsync(new ProbePinged(value), new DeliveryOptions { TenantId = tenant.ToString() });
        }

        await Waiting.UntilAsync(async () => await DeadLettersOfAsync<ProbePinged>(name) > 0);
        await Waiting.UntilAsync(async () => await ProbesAsync(name, $"recorded:{value}") > 0);

        (await ProbesAsync(name, $"recorded:{value}")).ShouldBe(1L);
        (await ProbesAsync(name, $"failed:{value}")).ShouldBe(0L);
        (await DeadLettersOfAsync<ProbePinged>(name)).ShouldBe(1L);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(ProbePinged), typeof(RecordPingHandler))]);
        runs.In("web").ShouldBeEmpty();
    }

    // Requirement 6 in the production layout: a message that always fails is moved aside to the dead letter queue, and the queue does
    // not block. The messages sent after it are still handled.
    [Fact]
    public async Task SendMessages_FirstAlwaysFailsOnAWebAndAWorkerHost_IsDeadLetteredAndTheNextAreHandled()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await database.CreateProbesAsync(name);
        await using var web = ProbeHost(name, "web", new HandlerRuns());
        await using var worker = ProbeHost(name, "worker", new HandlerRuns());
        _ = worker.Services;
        var tenant = new DeliveryOptions { TenantId = Guid.NewGuid().ToString() };
        var first = $"first-{Guid.NewGuid():N}";
        var second = $"second-{Guid.NewGuid():N}";

        await using (var scope = web.Services.CreateAsyncScope())
        {
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            await bus.SendAsync(new FailProbe(), tenant);
            await bus.SendAsync(new ProbeAnnounced(first), tenant);
            await bus.SendAsync(new ProbeAnnounced(second), tenant);
        }

        await Waiting.UntilAsync(async () => await DeadLettersOfAsync<FailProbe>(name) > 0);
        await Waiting.UntilAsync(async () => await ProbesAsync(name, $"announced:{first}") + await ProbesAsync(name, $"announced:{second}") == 2);

        (await DeadLettersOfAsync<FailProbe>(name)).ShouldBe(1L);
        (await ProbesAsync(name, $"announced:{first}")).ShouldBe(1L);
        (await ProbesAsync(name, $"announced:{second}")).ShouldBe(1L);
    }

    // A worker serves the health endpoints on its management port. It opens no public port, so it serves no API
    // (ManagementPortTests).
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task CheckHealth_OnAWorkerHost_IsHealthy(string path)
    {
        await using var worker = Host(await database.CreateMigratedDatabaseAsync(), "worker", new HandlerRuns(), HostPorts.Free());

        var response = await worker.ManagementClient().GetAsync(path, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage created) =>
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    private ApiFactory Host(string databaseName, string role, HandlerRuns runs, HostPorts? ports = null) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?> { ["Host:Role"] = role },
        configureServices: HandlerRunsOfHost.Register(role, runs),
        ports: ports);

    // A host with the probe module's DbContext, the two handlers of ProbePinged, and a handler that writes and one that always fails.
    private ApiFactory ProbeHost(string databaseName, string role, HandlerRuns runs) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?> { ["Host:Role"] = role },
        configureServices: services =>
        {
            HandlerRunsOfHost.Register(role, runs)(services);
            services.AddDbContextWithWolverineIntegration<ProbeDbContext>(
                (provider, options) => options.UseModuleDatabase(provider, ProbeDbContext.Schema),
                TenancyServiceCollectionExtensions.MessageSchema);
            services.ConfigureWolverine(options =>
            {
                options.Discovery.IncludeType(typeof(RecordPingHandler));
                options.Discovery.IncludeType(typeof(FailOnPingHandler));
                options.Discovery.IncludeType(typeof(ProbeAnnouncedHandler));
                options.Discovery.IncludeType(typeof(FailProbeHandler));
            });
        });

    private Task<long> TenantCreatedEntriesAsync(string databaseName, Guid tenantId) => AuditEntriesAsync(databaseName, tenantId, "tenant.created");

    private Task<long> AuditEntriesAsync(string databaseName, Guid tenantId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = '{operation}'", databaseName);

    private Task<long> PlatformEntriesAsync(string databaseName, Guid targetId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{targetId}' AND operation = '{operation}'", databaseName);

    private Task<long> ProbesAsync(string databaseName, string value) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM probes.probes WHERE value = '{value}'", databaseName);

    private Task<long> DeadLettersOfAsync<TMessage>(string databaseName) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM wolverine.wolverine_dead_letters WHERE message_type = '{typeof(TMessage).ToMessageTypeName()}'", databaseName);

    private async Task<string> AddSystemAdminAsync(string databaseName)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.users (id, external_id, language) VALUES ('{Guid.NewGuid()}', '{externalId}', 'en');
            INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{externalId}';
            """,
            database: databaseName);

        return externalId;
    }

    private Task<string?> SagaStateAsync(string databaseName, Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT state FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}'", databaseName);
}
