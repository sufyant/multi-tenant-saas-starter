using System.Net.Http.Json;
using System.Net;
using System.Text.Json;
using Audit.Application.AuditLogs.ApplyAuditRetention;
using Audit.Application.AuditLogs.ApplyTenantAuditRetention;
using Audit.Application.AuditLogs.DeleteExpiredEntries;
using Audit.Application.AuditLogs.DeleteExpiredPlatformEntries;
using Audit.Application.Invitations.RecordInvitationRevoked;
using Audit.Application.Invitations.RecordOwnerInvited;
using Audit.Application.Members.RecordMemberInvited;
using Audit.Application.Members.RecordMemberLeft;
using Audit.Application.Members.RecordMemberRemoved;
using Audit.Application.Members.RecordMemberRoleChanged;
using Audit.Application.Members.RecordOwnerMade;
using Audit.Application.Plans.RecordContractStarted;
using Billing.Application.PriceChanges.AnnounceTenantPriceChange;
using Billing.Application.PriceChanges.RecordPriceChange;
using Audit.Application.Plans.RecordPlanCreated;
using Audit.Application.Plans.RecordTenantEntitlementOverrideSet;
using Audit.Application.Plans.RecordTenantPlanChanged;
using Audit.Application.Roles.RecordRoleCreated;
using Audit.Application.Roles.RecordRoleDeleted;
using Audit.Application.Roles.RecordRoleUpdated;
using Audit.Application.SystemAdmins.RecordSystemAdminGranted;
using Audit.Application.SystemAdmins.RecordSystemAdminRevoked;
using Audit.Application.Tenants.RecordTenantClosed;
using Audit.Application.Tenants.RecordTenantCreated;
using Audit.Application.Tenants.RecordTenantPurgeWarned;
using Audit.Application.Tenants.RecordTenantPurged;
using Audit.Application.Tenants.RecordTenantReactivated;
using Audit.Application.Tenants.RecordTenantRenamed;
using Audit.Application.Tenants.RecordTenantReopened;
using Audit.Application.Tenants.RecordTenantSuspended;
using Audit.Application.Users.RecordUserDeleted;
using Audit.Application.Users.RecordUserMembershipRemoved;
using Audit.Application.Users.RecordUserPreferencesChanged;
using Audit.Application.Users.RecordUserRenamed;
using Audit.Application.Users.RecordUserSuspended;
using Audit.Application.FeatureFlags.RecordFeatureFlagAddedToCode;
using Audit.Application.FeatureFlags.RecordFeatureFlagEveryoneElseSet;
using ControlPlane.Application.FeatureFlags.CheckFeatureFlagCleanup;
using ControlPlane.Application.FeatureFlags.SyncFeatureFlags;
using ControlPlane.Application.Invitations.InviteMember;
using ControlPlane.Application.Invitations.InviteOwner;
using ControlPlane.Application.Invitations.OpenSignUp;
using ControlPlane.Application.Invitations.RecordSignUp;
using ControlPlane.Application.Invitations.RevokeInvitation;
using ControlPlane.Application.Invitations.WithdrawSignUp;
using ControlPlane.Application.Members.ChangeMemberRole;
using ControlPlane.Application.Members.MakeOwner;
using ControlPlane.Application.Members.LeaveTenant;
using ControlPlane.Application.Members.RemoveMember;
using ControlPlane.Application.Roles.CreateRole;
using ControlPlane.Application.Roles.DeleteRole;
using ControlPlane.Application.Roles.UpdateRole;
using ControlPlane.Application.SystemAdmins.GrantRootSystemAdmin;
using ControlPlane.Application.Tenants.ChangeTenantPlan;
using ControlPlane.Application.Tenants.CloseTenant;
using ControlPlane.Application.Tenants.CompleteTenantPurge;
using ControlPlane.Application.Tenants.GetPaysByCard;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.ReactivateTenant;
using ControlPlane.Application.Tenants.RenameTenant;
using ControlPlane.Application.Tenants.MoveToDefaultPlan;
using ControlPlane.Application.Tenants.ReopenTenant;
using ControlPlane.Application.Tenants.StartTenantPurge;
using ControlPlane.Application.Tenants.SuspendTenant;
using ControlPlane.Application.Tenants.TenantPurge;
using ControlPlane.Application.Tenants.WarnTenantPurge;
using ControlPlane.Application.Tenants.CreateMyTenant;
using ControlPlane.Application.Tenants.EndTenantTrial;
using Audit.Application.Plans.RecordTenantTrialEnded;
using ControlPlane.Application.Users.ChangePreferences;
using ControlPlane.Application.Users.DeleteIdentityAccount;
using ControlPlane.Application.Users.DeleteUser;
using ControlPlane.Application.Users.DeleteUserWithoutTenant;
using ControlPlane.Application.Users.EraseFormerMember;
using ControlPlane.Application.Users.ListUserMemberships;
using ControlPlane.Application.Users.RemoveUserMembership;
using ControlPlane.Application.Users.RenameUser;
using ControlPlane.Application.Users.SuspendUser;
using Audit.Application.DataExports.RecordDataExportRequested;
using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Subscriptions.ApplySubscription;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Application.Subscriptions.RouteSubscription;
using Billing.Application.Subscriptions.NameScheduleChange;
using Audit.Application.Plans.RecordPaymentFailed;
using Billing.Application.Members.CountSeatsAtMemberChange;
using Billing.Application.Subscriptions.SetSeats;
using Billing.Application.Subscriptions.SyncSubscription;
using Billing.Application.Webhooks.GetWebhookPayload;
using Billing.Application.Webhooks.ReceiveWebhook;
using Billing.Application.Tenants.ResumeBillingAtReopen;
using Billing.Application.Tenants.StopBillingAtClose;
using Billing.Contracts.Subscriptions;
using ControlPlane.Application.Tenants.ChangeBilledPlan;
using ControlPlane.Application.DataExports.CompleteDataExport;
using ControlPlane.Application.DataExports.PrepareDataExport;
using ControlPlane.Application.DataExports.RequestDataExport;
using ControlPlane.Application.Users.DeleteUserFiles;
using ControlPlane.Contracts.DataExports;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Roles;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Email;
using Notifications.Application.Invitations.SendInvitationEmail;
using Notifications.Application.Members.NotifyMemberRoleChanged;
using Notifications.Application.Notifications.DeliverNotification;
using Notifications.Application.Tenants.SendTenantPurgeWarningEmail;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Util;
using Notifications.Application.Plans.NotifyTenantMovedToDefaultPlan;
using Notifications.Application.PriceChanges.NotifyTenantPriceChangeAnnounced;
using Notifications.Application.Subscriptions.NotifyTenantPaymentFailed;
using Notifications.Application.Subscriptions.NotifyTenantPaymentGraceStarted;
using Audit.Application.BillingEmails.RecordBillingEmailChanged;
using Billing.Application.BillingEmails.KeepCustomerEmail;
using Billing.Application.BillingEmails.NameBillingEmailChange;
using Billing.Application.BillingEmails.NameCustomerEmail;

namespace Api.IntegrationTests.Hosting;

// Section 1: one build output runs as two process types, chosen by Host:Role. The web host serves the API and runs the command a
// request sends; whatever that command sends on is handled by a worker host. The two hosts share a database of their own here, so no
// other test's host takes part.
[RuntimeHandlerCode("The hosts have the test policy RecordHandlerRuns and the probe handlers of ProbePinged, ProbeAnnounced and FailProbe.")]
public sealed class HostRoleTests(Database database)
{
    // The handlers of every message the tenant creation sends, as docs/flows/tenant-creation.md lists them.
    private static readonly HandlerRun[] HandlersAfterTheCreation =
    [
        new(typeof(TenantCreatedEvent), typeof(RecordTenantCreatedHandler)),
        new(typeof(OpenSignUpCommand), typeof(OpenSignUpHandler)),
        new(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
        new(typeof(InvitationReadyEvent), typeof(SendInvitationEmailHandler)),
    ];

    private static readonly HandlerRun Creation = new(typeof(CreateTenantCommand), typeof(CreateTenantHandler));

    // The handlers of every message a closure sends once its reopen period ends, as docs/flows/tenant-purge.md, lists them: the warning,
    // the purge's pivot, each module's purge and its reply to the saga, ControlPlane's last step and its reply, the platform's entry, and
    // the deletion of the user left without a tenant.
    private static readonly HandlerRun[] HandlersOfThePurge =
    [
        new(typeof(TenantClosedEvent), typeof(RecordTenantClosedHandler)),
        new(typeof(TenantClosedEvent), typeof(StopBillingAtCloseHandler)),
        new(typeof(WarnTenantPurgeCommand), typeof(WarnTenantPurgeHandler)),
        new(typeof(TenantPurgeWarnedEvent), typeof(RecordTenantPurgeWarnedHandler)),
        new(typeof(TenantPurgeWarningReadyEvent), typeof(SendTenantPurgeWarningEmailHandler)),
        new(typeof(StartTenantPurgeCommand), typeof(StartTenantPurgeHandler)),
        new(typeof(PurgeTenantDataCommand), typeof(Audit.Application.Tenants.PurgeTenantData.PurgeTenantDataHandler)),
        new(typeof(PurgeTenantDataCommand), typeof(Notifications.Application.Tenants.PurgeTenantData.PurgeTenantDataHandler)),
        new(typeof(PurgeTenantDataCommand), typeof(Api.Storage.PurgeTenantFilesHandler)),
        new(typeof(PurgeTenantDataCommand), typeof(Billing.Application.Tenants.PurgeTenantData.PurgeTenantDataHandler)),
        new(typeof(PurgeTenantDataReply), typeof(TenantPurgeSaga)),
        new(typeof(CompleteTenantPurgeCommand), typeof(CompleteTenantPurgeHandler)),
        new(typeof(CompleteTenantPurgeReply), typeof(TenantPurgeSaga)),
        new(typeof(TenantPurgedEvent), typeof(RecordTenantPurgedHandler)),
        new(typeof(DeleteUserWithoutTenantCommand), typeof(DeleteUserWithoutTenantHandler)),
        new(typeof(UserDeletedEvent), typeof(RecordUserDeletedHandler)),
        new(typeof(DeleteIdentityAccountCommand), typeof(DeleteIdentityAccountHandler)),
        new(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler)),
    ];

    // The handlers of every message a person's data export sends after its request, as docs/flows/personal-data-export.md lists them,
    // but the notification's, which WithoutNotificationsIn leaves out; the email that the test waits for is its last step.
    private static readonly HandlerRun[] HandlersOfTheExport =
    [
        new(typeof(DataExportRequestedEvent), typeof(RecordDataExportRequestedHandler)),
        new(typeof(PrepareDataExportCommand), typeof(PrepareDataExportHandler)),
        new(typeof(CompleteDataExportCommand), typeof(CompleteDataExportHandler)),
    ];

    // A reopen period of seconds: the warning comes 2 s after the closure and the purge 10 s after it, by Wolverine's clock.
    private static readonly Dictionary<string, string?> ShortReopenPeriod = new()
    {
        ["ControlPlane:Tenants:ReopenPeriod"] = "00:00:10",
        ["ControlPlane:Tenants:PurgeWarningLead"] = "00:00:08",
    };

    // Sign-up and billing on, with a trial of two seconds, so a test sees its end.
    private static readonly Dictionary<string, string?> ShortTrial = new(SignUpSettings.On.Concat(BillingRequests.On))
    {
        ["ControlPlane:Trial:Length"] = "00:00:02",
    };

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_RunsEveryMessageAfterTheCreationInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" });
        await Waiting.UntilAsync(() => worker.Email.Sent.Count > 0);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([Creation]);
        runs.In("worker").Distinct().ShouldBe(HandlersAfterTheCreation, ignoreOrder: true);
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
        var queued = await database.ScalarAsync<long>(
            """
            SELECT count(*) FROM (
                SELECT message_type FROM wolverine.wolverine_queue_messages
                UNION ALL SELECT message_type FROM wolverine.wolverine_outgoing_envelopes) AS waiting
            WHERE message_type = 'open-sign-up'
            """,
            database: name);

        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        await Waiting.UntilAsync(() => worker.Email.Sent.Count > 0);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        queued.ShouldBe(1L);
        runs.In("web").ShouldBe([Creation]);
        worker.Email.Sent.ShouldHaveSingleItem().To.ShouldBe("owner@acme.test");
    }

    // O3: the Audit handler of the creation runs in the worker, once.
    [Fact]
    public async Task CreateTenant_OnAWebAndAWorkerHost_WritesOneTenantCreatedAuditEntry()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var web = Host(name, "web", new HandlerRuns());
        await using var worker = Host(name, "worker", new HandlerRuns());
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var tenantId = await IdOfAsync(await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = "acme", ownerEmail = "owner@acme.test" }));
        await Waiting.UntilAsync(async () => await TenantCreatedEntriesAsync(name, tenantId) > 0);

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
        var tenant = await new TestData(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.SuspendTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendTenantCommand), typeof(SuspendTenantHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(TenantSuspendedEvent), typeof(RecordTenantSuspendedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.suspended")).ShouldBe(1L);
    }

    // O3: a tenant's plan change is announced from the web host, and its Audit handler runs in the worker, once, in both logs.
    [Fact]
    public async Task ChangeTenantPlan_OnAWebAndAWorkerHost_WritesOneAuditEntryInEachLogInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new TestData(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, SeedPlans.Free);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        // B12: the web first asks Billing whether the tenant pays by card (GetPaysByCardQuery, inline), then changes the plan.
        runs.In("web").ShouldBe(
        [
            new HandlerRun(typeof(GetPaysByCardQuery), typeof(GetPaysByCardHandler)),
            new HandlerRun(typeof(ChangeTenantPlanCommand), typeof(ChangeTenantPlanHandler)),
        ]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(TenantPlanChangedEvent), typeof(RecordTenantPlanChangedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.plan_changed")).ShouldBe(1L);
    }

    // B4 to B6: the web host keeps Stripe's event, and the worker reads the subscription, applies it, changes the plan and records it. Each
    // host has a fake Stripe of its own, so the subscription is the worker's.
    [Fact]
    public async Task ReceiveWebhook_OnAWebAndAWorkerHost_KeepsTheEventInTheWebAndChangesThePlanInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var planId = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, planId)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, planId));
        runs.Clear();

        var received = await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);

        received.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe(
            [
                new HandlerRun(typeof(GetWebhookPayloadQuery), typeof(GetWebhookPayloadHandler)),
                new HandlerRun(typeof(ReceiveWebhookCommand), typeof(ReceiveWebhookHandler)),
            ],
            ignoreOrder: true);
        // The expiries of the customer's open pages run beside the plan's change, the reservation's one too, so they are waited for apart.
        await Waiting.UntilAsync(() => worker.Billing.Expiries.Contains(($"cus_{tenant.Id:N}", web.Billing.Checkouts.Single().Key)));
        runs.WithoutNotificationsIn("worker").Where(run => run.Handler != typeof(ExpireCheckoutPagesHandler)).ShouldBe(
            [
                new HandlerRun(typeof(SyncSubscriptionCommand), typeof(SyncSubscriptionHandler)),
                new HandlerRun(typeof(RouteSubscriptionCommand), typeof(RouteSubscriptionHandler)),
                new HandlerRun(typeof(ApplySubscriptionCommand), typeof(ApplySubscriptionHandler)),
                new HandlerRun(typeof(PlanPaidEvent), typeof(ChangeBilledPlanHandler)),
                new HandlerRun(typeof(TenantPlanChangedEvent), typeof(RecordTenantPlanChangedHandler)),
            ],
            ignoreOrder: true);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed")).ShouldBe(1L);
    }

    // B8: the closure and the reopen of a paying tenant reach Stripe from the worker: the end at the period's end, then its undo.
    [Fact]
    public async Task CloseAndReopenTenant_APayingTenantOnAWebAndAWorkerHost_EndAndKeepItsSubscriptionInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var planId = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, planId)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, planId));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        await admin.CloseTenantAsync(tenant.Id);
        await Waiting.UntilAsync(() => Task.FromResult(!worker.Billing.PeriodEnds.IsEmpty));
        await admin.ReopenTenantAsync(tenant.Id);
        await Waiting.UntilAsync(() => Task.FromResult(worker.Billing.PeriodEnds.Count > 1));

        worker.Billing.PeriodEnds.ShouldBe([($"sub_{tenant.Id:N}", true), ($"sub_{tenant.Id:N}", false)]);
        runs.In("worker").Count(run => run.Handler == typeof(SetCancelAtPeriodEndHandler)).ShouldBe(2);
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(SetCancelAtPeriodEndHandler));
    }

    // B13: a plan edit on the web host; the price change, its notice and both audit entries run in the worker. The move is scheduled 30
    // days away by the real clock, so no run reaches it.
    [Fact]
    public async Task UpdatePlan_APriceChangeOnAWebAndAWorkerHost_TellsThePayerInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var planName = $"Team {Guid.NewGuid():N}";
        var planId = await testData.AddPlanAsync([], name: planName);
        await testData.SetPlanPricesAsync(planId, 0, 1000, 0, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, planId)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, planId) with { BaseAmount = 0, PerPersonAmount = 1000 });
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        (await admin.PatchAsJsonAsync(
            $"/v1/platform/plans/{planId}",
            new
            {
                name = planName,
                isDefault = false,
                entitlements = new Dictionary<string, object>(),
                prices = new { baseMonthly = 0, perPersonMonthly = 1200, baseYearly = 0, perPersonYearly = 12000 },
                priceChange = new { who = "everyone_after_notice" },
            },
            TestContext.Current.CancellationToken)).EnsureSuccessStatusCode();
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.price_change_announced") > 0);

        runs.In("worker").ShouldContain(run => run.Handler == typeof(RecordPriceChangeHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(AnnounceTenantPriceChangeHandler));
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(RecordPriceChangeHandler) || run.Handler == typeof(AnnounceTenantPriceChangeHandler));
        await Waiting.UntilAsync(async () => await NotificationsAsync(name, tenant.Id, "tenant.price_change_announced") > 0);
        runs.In("worker").ShouldContain(run => run.Handler == typeof(NotifyTenantPriceChangeAnnouncedHandler));
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(NotifyTenantPriceChangeAnnouncedHandler));
    }

    // B12: the switch to Contract runs in the web host; Stripe's end at the period's end and both audit entries run in the worker.
    [Fact]
    public async Task SetBilledBy_APayingTenantOnAWebAndAWorkerHost_EndsItsSubscriptionAndRecordsItInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var planId = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(planId, 900, 1000, 9000, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, planId)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, planId));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        (await admin.SetBilledByAsync(tenant.Id, "contract")).EnsureSuccessStatusCode();
        await Waiting.UntilAsync(async () => !worker.Billing.PeriodEnds.IsEmpty && await AuditEntriesAsync(name, tenant.Id, "tenant.contract_started") > 0);

        worker.Billing.PeriodEnds.ShouldBe([($"sub_{tenant.Id:N}", true)]);
        runs.In("worker").ShouldContain(run => run.Handler == typeof(RecordContractStartedHandler));
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(SetCancelAtPeriodEndHandler) || run.Handler == typeof(RecordContractStartedHandler));
    }

    // B9, 12.12: a member's cancellation is the web host's call to Stripe; the sync, the names and both audit entries run in the worker.
    [Fact]
    public async Task ChangePlan_ACancellationOnAWebAndAWorkerHost_RecordsItInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var team = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(team, 900, 1000, 9000, 10000);
        var free = await database.ScalarAsSuperuserAsync<Guid>("SELECT plan_id FROM control_plane.default_plans", name);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, team)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, team));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, team) with { CancelAtPeriodEnd = true });

        var answer = await web.CreateClient(owner).ChangePlanAsync(tenant.Id, free);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_cancelled") > 0);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(NameScheduleChangeHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(NameScheduleChangeHandler));
    }

    // B9: a member's upgrade is the web host's call to Stripe; the sync that follows, and the plan change it brings, run in the worker.
    [Fact]
    public async Task ChangePlan_OnAWebAndAWorkerHost_SyncsTheSubscriptionInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var team = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(team, 900, 1000, 9000, 10000);
        var business = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(business, 9900, 800, 99000, 8000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, team)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, team));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, business));

        var answer = await web.CreateClient(owner).ChangePlanAsync(tenant.Id, business);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 1);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        web.Billing.MovesNow.Select(move => move.Order.PlanId).ShouldBe([business]);
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(SyncSubscriptionHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(SyncSubscriptionHandler));
    }

    // B10: a removal on the web host sets the seats at Stripe in the worker, and the event's Audit handler still runs beside the seats'.
    [Fact]
    public async Task RemoveMember_APayingTenantOnAWebAndAWorkerHost_SetsItsSeatsAndRecordsTheRemovalInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await testData.UserIdOfAsync(await testData.AddMemberAsync(tenant.Id));
        var team = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(team, 0, 1000, 0, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, team)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, team));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);

        var removed = await web.CreateClient(owner).RemoveMemberAsync(tenant.Id, member);
        await Waiting.UntilAsync(() => !worker.Billing.Seats.IsEmpty);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        worker.Billing.Seats.ShouldBe([($"sub_{tenant.Id:N}", 1)]);
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(SetSeatsHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(CountSeatsAtMemberChangeHandler));
    }

    // B11: Stripe's events of a failed payment, of the end of its retries and of the unpaid subscription's end arrive at the web host; the
    // failed payment's entry and the move to the default plan run in the worker, through Audit and ControlPlane, once each, and so do the
    // bells and emails of the three (13.18 to 13.20).
    [Fact]
    public async Task ReceiveWebhook_AFailedPaymentThenItsEndOnAWebAndAWorkerHost_RecordBothAndTellInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var team = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(team, 900, 1000, 9000, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, team)).EnsureSuccessStatusCode();
        worker.Billing.Put(BillingRequests.Subscription(tenant.Id, team));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "checkout.session.completed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.plan_changed") > 0);
        var failedAt = DateTimeOffset.UtcNow.AddDays(-1);
        worker.Billing.Put(BillingRequests.PastDue(tenant.Id, team, failedAt, 1, failedAt.AddDays(3)));

        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.payment_failed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.payment_failed") > 0);
        worker.Billing.Put(BillingRequests.PastDue(tenant.Id, team, failedAt, 4, null));
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "invoice.payment_failed", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await NotificationsAsync(name, tenant.Id, "tenant.payment_grace_started") > 0);
        worker.Billing.Put(BillingRequests.PastDue(tenant.Id, team, failedAt, 4, null) with { Status = "Canceled" });
        await web.CreateClient().SendWebhookAsync($"evt_{Guid.NewGuid():N}", "customer.subscription.deleted", $"sub_{tenant.Id:N}");
        await Waiting.UntilAsync(async () => await NotificationsAsync(name, tenant.Id, "tenant.moved_to_default_plan") > 0);

        (await AuditEntriesAsync(name, tenant.Id, "tenant.payment_failed"), await AuditEntriesAsync(name, tenant.Id, "tenant.moved_to_default_plan")).ShouldBe((1L, 1L));
        (await NotificationsAsync(name, tenant.Id, "tenant.payment_failed"), await NotificationsAsync(name, tenant.Id, "tenant.payment_grace_started"),
            await NotificationsAsync(name, tenant.Id, "tenant.moved_to_default_plan")).ShouldBe((1L, 1L, 1L));
        Type[] tells = [typeof(NotifyTenantPaymentFailedHandler), typeof(NotifyTenantPaymentGraceStartedHandler), typeof(NotifyTenantMovedToDefaultPlanHandler)];
        runs.In("worker").Select(run => run.Handler).Intersect(tells).Count().ShouldBe(3);
        runs.In("web").ShouldNotContain(run => tells.Contains(run.Handler));
        runs.In("web").ShouldNotContain(run => run.Handler == typeof(MoveToDefaultPlanHandler) || run.Handler == typeof(RecordPaymentFailedHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(MoveToDefaultPlanHandler));
        runs.In("worker").ShouldContain(run => run.Handler == typeof(RecordPaymentFailedHandler));
    }

    // B14: a billing email set on the web host; its entry in the tenant's log and Stripe's customer email run in the worker, once each.
    [Fact]
    public async Task SetBillingEmail_OnAWebAndAWorkerHost_RecordsItAndTellsStripeInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: BillingRequests.On);
        await using var worker = Host(name, "worker", runs, settings: BillingRequests.On);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var team = await testData.AddPlanAsync([]);
        await testData.SetPlanPricesAsync(team, 900, 1000, 9000, 10000);
        (await web.CreateClient(owner).OpenCheckoutSessionAsync(tenant.Id, team)).EnsureSuccessStatusCode();
        var accounts = PlatformTenants.Email();

        (await web.CreateClient(owner).SetBillingEmailAsync(tenant.Id, accounts)).EnsureSuccessStatusCode();
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.billing_email_changed") > 0);
        await Waiting.UntilAsync(() => Task.FromResult(worker.Billing.CustomerEmails.Any(email => email.Email == accounts)));

        (await AuditEntriesAsync(name, tenant.Id, "tenant.billing_email_changed")).ShouldBe(1);
        Type[] steps = [typeof(NameBillingEmailChangeHandler), typeof(RecordBillingEmailChangedHandler), typeof(NameCustomerEmailHandler), typeof(KeepCustomerEmailHandler)];
        runs.In("worker").Select(run => run.Handler).Intersect(steps).Count().ShouldBe(4);
        runs.In("web").ShouldNotContain(run => steps.Contains(run.Handler));
    }

    // K14: a worker runs the audit retention at its start, and a web host never does. The tenant's first row holds every entry: a row
    // stays only for a tenant with an entry.
    [Fact]
    public async Task ApplyAuditRetention_OnAWebAndAWorkerHost_RunsInTheWorkerAtItsStart()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var tenant = await new TestData(database, name).AddTenantAsync();
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id)
            VALUES (gen_random_uuid(), '{tenant.Id}', now(), 'Platform', 'Command', 'tenant.suspended', 'Tenant', '{tenant.Id}')
            """,
            name);
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, auditRetention: true);
        await using var worker = Host(name, "worker", runs, auditRetention: true);
        _ = web.Services;
        _ = worker.Services;

        await Waiting.UntilAsync(async () => await RetentionDaysAsync(name, tenant.Id) == 365
            && runs.In("worker").Any(run => run.Handler == typeof(DeleteExpiredPlatformEntriesHandler)));

        runs.In("web").ShouldBeEmpty();
        runs.In("worker").Select(run => run.Handler).ShouldBe([typeof(ApplyAuditRetentionHandler), typeof(ApplyTenantAuditRetentionHandler), typeof(DeleteExpiredEntriesHandler), typeof(DeleteExpiredPlatformEntriesHandler)], ignoreOrder: true);
    }

    // G2, G6: a worker syncs the flags and looks at their cleanup at its start, and a web host never does; the weekly bell of the
    // system admins is written in the worker.
    [Fact]
    public async Task SyncAndCheckFeatureFlags_OnAWebAndAWorkerHost_RunInTheWorkerAtItsStartAndTellTheSystemAdmins()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await AddSystemAdminAsync(name);
        await OwnFlags.AddFlagAsync(database, name, "roles.bulk_edit", everyoneElse: true, daysAgo: 40);
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, featureFlagJob: true);
        await using var worker = Host(name, "worker", runs, featureFlagJob: true);
        _ = web.Services;
        _ = worker.Services;

        await Waiting.UntilAsync(async () =>
            await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM notifications.notifications WHERE type = 'feature_flags.ready_to_remove'", name) == 1
            && runs.In("worker").Any(run => run.Handler == typeof(RecordFeatureFlagAddedToCodeHandler)));

        runs.In("web").ShouldBeEmpty();
        runs.WithoutNotificationsIn("worker").Select(run => run.Handler).ShouldBe(
            [typeof(SyncFeatureFlagsHandler), typeof(RecordFeatureFlagAddedToCodeHandler), typeof(CheckFeatureFlagCleanupHandler)], ignoreOrder: true);
    }

    // G5: a rule's change is made in the web host, and its audit entry is written in the worker.
    [Fact]
    public async Task SetFeatureFlagEveryoneElse_OnAWebAndAWorkerHost_WritesOnePlatformEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await OwnFlags.AddFlagAsync(database, name, "emails.send", type: "KillSwitch", everyoneElse: true);
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        (await admin.SetFeatureFlagEveryoneElseAsync("emails.send", false)).EnsureSuccessStatusCode();

        await Waiting.UntilAsync(() => runs.In("worker").Any(run => run.Handler == typeof(RecordFeatureFlagEveryoneElseSetHandler)));
        runs.In("worker").Select(run => run.Handler).ShouldBe([typeof(RecordFeatureFlagEveryoneElseSetHandler)]);
        (await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM audit.platform_entries WHERE operation = 'feature_flag.everyone_else_off'", name)).ShouldBe(1);
    }

    // K14: a plan change that shortens the period is announced from the web host, and the worker's Audit handler starts the hold.
    [Fact]
    public async Task ChangeTenantPlan_ToAShorterPeriodOnAWebAndAWorkerHost_StartsTheHoldInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var testData = new TestData(database, name);
        var plan = await testData.AddPlanAsync(new() { ["audit.retention.days"] = 90 });
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await testData.AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var changed = await admin.ChangeTenantPlanAsync(tenant.Id, plan);
        await Waiting.UntilAsync(async () => await RetentionDaysAsync(name, tenant.Id) == 90);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await database.ScalarAsSuperuserAsync<bool>(
            $"""
            SELECT held_until = (SELECT occurred_at + interval '30 days' FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.plan_changed')
            FROM audit.tenant_retentions WHERE tenant_id = '{tenant.Id}'
            """,
            name)).ShouldBeTrue();
    }

    // O3: an override and a new plan are announced from the web host, and their Audit handlers run in the worker, once: the plan in the
    // platform's log only, because it belongs to no tenant, and the override in both logs.
    [Fact]
    public async Task SetOverrideAndCreatePlan_OnAWebAndAWorkerHost_WriteTheirEntriesInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var tenant = await new TestData(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var set = await admin.SetOverrideAsync(tenant.Id, "members.max", PlanRequests.Max(50));
        var created = await admin.CreatePlanAsync("Enterprise", new Dictionary<string, object>());
        var planId = await IdOfAsync(created);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, planId, "plan.created") > 0
            && await PlatformEntriesAsync(name, tenant.Id, "tenant.entitlement_override_set") > 0);

        (set.StatusCode, created.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.Created));
        runs.WithoutNotificationsIn("worker").ShouldBe(
            [
                new HandlerRun(typeof(EntitlementOverrideSetEvent), typeof(RecordTenantEntitlementOverrideSetHandler)),
                new HandlerRun(typeof(PlanCreatedEvent), typeof(RecordPlanCreatedHandler)),
            ],
            ignoreOrder: true);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.entitlement_override_set")).ShouldBe(1L);
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
        var tenant = await new TestData(database, name).AddTenantAsync();
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var closed = await admin.CloseTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.closed") > 0);

        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(CloseTenantCommand), typeof(CloseTenantHandler))]);
        // Billing hears the closure too (B8), beside the audit entry this test waits for, so it may not have run yet.
        runs.WithoutNotificationsIn("worker").Where(run => run.Handler != typeof(StopBillingAtCloseHandler)).ShouldBe([new HandlerRun(typeof(TenantClosedEvent), typeof(RecordTenantClosedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.closed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.closed")).ShouldBe(1L);
    }

    // S13, W5: the template's saga in the production layout. The web host closes the tenant and, through the outbox, schedules the warning
    // and the purge; a worker runs every step after them: the warning and its email, the purge's pivot, each module's purge, the saga,
    // ControlPlane's last step, and the deletion of the Owner it left without a tenant.
    [Fact]
    public async Task CloseTenant_TheReopenPeriodEndsOnAWebAndAWorkerHost_WarnsAndPurgesInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: ShortReopenPeriod);
        await using var worker = Host(name, "worker", runs, settings: ShortReopenPeriod);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = await testData.AddUserAsync(email: "owner@acme.test");
        await testData.AddMemberAsync(tenant.Id, owner, role: "Owner");
        var ownerId = await testData.UserIdOfAsync(owner);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var closed = await admin.CloseTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, ownerId, "user.deleted") > 0 && worker.Identity.Deleted.Contains(owner)
            && runs.In("worker").Contains(new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler))));

        closed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(CloseTenantCommand), typeof(CloseTenantHandler))]);
        runs.WithoutNotificationsIn("worker").Distinct().ShouldBe(HandlersOfThePurge, ignoreOrder: true);
        worker.Email.Sent.Where(email => email.IdempotencyKey.StartsWith("purge-warning/", StringComparison.Ordinal)).Select(email => email.To).ShouldBe(["owner@acme.test"]);
        (await testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Purged");
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.purged")).ShouldBe(1L);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenant.Id}'", name)).ShouldBe(0L);
    }

    // Step 40d: the web host creates a user's own tenant and, through the outbox, schedules the end of its trial; a worker ends the trial
    // and records it.
    [Fact]
    public async Task CreateMyTenant_TheTrialEndsOnAWebAndAWorkerHost_EndsItAndRecordsItInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs, settings: ShortTrial);
        await using var worker = Host(name, "worker", runs, settings: ShortTrial);
        _ = worker.Services;
        var person = await new TestData(database, name).AddUserAsync(email: "kestrel@example.com");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/me/tenants")
        {
            Content = JsonContent.Create(new { name = "Kestrel Analytics", slug = "kestrel" }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var created = await web.CreateClient(person).SendAsync(request, Cancellation);
        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, tenantId, "tenant.trial_ended") > 0);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldContain(new HandlerRun(typeof(CreateMyTenantCommand), typeof(CreateMyTenantHandler)));
        runs.In("web").ShouldNotContain(new HandlerRun(typeof(EndTenantTrialCommand), typeof(EndTenantTrialHandler)));
        runs.In("worker").ShouldContain(new HandlerRun(typeof(EndTenantTrialCommand), typeof(EndTenantTrialHandler)));
        runs.In("worker").ShouldContain(new HandlerRun(typeof(TenantTrialEndedEvent), typeof(RecordTenantTrialEndedHandler)));
        (await AuditEntriesAsync(name, tenantId, "tenant.trial_ended")).ShouldBe(1L);
    }

    // The web host takes the request; the worker prepares the file with every module's exporter, which it takes from the message's scope,
    // marks the export ready and tells the person (docs/flows/personal-data-export.md).
    [Fact]
    public async Task RequestDataExport_OnAWebAndAWorkerHost_PreparesTheFileInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var user = await testData.AddUserAsync(email: "ada@acme.test");
        var userId = await testData.UserIdOfAsync(user);

        var requested = await DataExportRequests.RequestAsync(web, user);
        await Waiting.UntilAsync(() => Task.FromResult(worker.Email.Sent.Any(email => email.To == "ada@acme.test")));

        requested.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([new HandlerRun(typeof(RequestDataExportCommand), typeof(RequestDataExportHandler))]);
        runs.WithoutNotificationsIn("worker").Distinct().ShouldBe(HandlersOfTheExport, ignoreOrder: true);
        (await database.ScalarAsSuperuserAsync<string>($"SELECT status FROM control_plane.stored_files WHERE user_id = '{userId}'", name)).ShouldBe("Ready");
        (await ObjectStorage.KeysAsync(SharedKernel.FileKeys.UserExports(userId))).Count.ShouldBe(1);
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
        var tenant = await new TestData(database, name).AddTenantAsync();
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
        var tenant = await new TestData(database, name).AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var reopened = await admin.ReopenTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.reopened") > 0);

        reopened.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ReopenTenantCommand), typeof(ReopenTenantHandler))]);
        // Billing hears the reopen too (B8), beside the audit entry this test waits for, so it may not have run yet.
        runs.WithoutNotificationsIn("worker").Where(run => run.Handler != typeof(ResumeBillingAtReopenHandler)).ShouldBe([new HandlerRun(typeof(TenantReopenedEvent), typeof(RecordTenantReopenedHandler))]);
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
        var testData = new TestData(database, name);
        var userId = await testData.UserIdOfAsync(await testData.AddUserAsync());
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var suspended = await admin.SuspendUserAsync(userId);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, userId, "user.suspended") > 0);

        suspended.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(SuspendUserCommand), typeof(SuspendUserHandler))]);
        runs.In("worker").ShouldBe([new HandlerRun(typeof(UserSuspendedEvent), typeof(RecordUserSuspendedHandler))]);
        (await PlatformEntriesAsync(name, userId, "user.suspended")).ShouldBe(1L);
    }

    // O3, K3: the system door lets a root system admin in from the web host, in the request, and the grant's Audit handler runs in the
    // worker, once, and writes the platform's log by the platform.
    [Fact]
    public async Task VisitTheSystemDoor_ARootOnAWebAndAWorkerHost_GrantsInTheWebAndRecordsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var root = $"user_{Guid.NewGuid():N}";
        web.Identity.AddAccount(root, ApiFactory.RootEmail);

        var visit = await web.CreateClient(root, secondFactor: true).GetAsync("/v1/platform/users", Cancellation);
        var userId = await new TestData(database, name).UserIdOfAsync(root);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(name, userId, "system_admin.granted") > 0);

        visit.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldContain(new HandlerRun(typeof(GrantRootSystemAdminCommand), typeof(GrantRootSystemAdminHandler)));
        runs.In("worker").ShouldBe([new HandlerRun(typeof(SystemAdminGrantedEvent), typeof(RecordSystemAdminGrantedHandler))]);
        (await PlatformEntriesAsync(name, userId, "system_admin.granted")).ShouldBe(1L);
    }

    // O3, K3: a grant and a revocation run in the web host, and their Audit handlers in the worker, once each.
    [Fact]
    public async Task GrantAndRevokeSystemAdmin_OnAWebAndAWorkerHost_WriteOnePlatformEntryEachInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var userId = await testData.UserIdOfAsync(await testData.AddUserAsync());
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);
        var grant = new HttpRequestMessage(HttpMethod.Post, $"/v1/platform/users/{userId}/system-admin");
        grant.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());

        var granted = await admin.SendAsync(grant, Cancellation);
        var revoked = await admin.DeleteAsync($"/v1/platform/users/{userId}/system-admin", Cancellation);
        await Waiting.UntilAsync(async () =>
            await PlatformEntriesAsync(name, userId, "system_admin.granted") > 0 && await PlatformEntriesAsync(name, userId, "system_admin.revoked") > 0);

        (granted.StatusCode, revoked.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.NoContent));
        runs.In("worker").ShouldBe(
            [new HandlerRun(typeof(SystemAdminGrantedEvent), typeof(RecordSystemAdminGrantedHandler)), new HandlerRun(typeof(SystemAdminRevokedEvent), typeof(RecordSystemAdminRevokedHandler))],
            ignoreOrder: true);
        (await PlatformEntriesAsync(name, userId, "system_admin.granted"), await PlatformEntriesAsync(name, userId, "system_admin.revoked")).ShouldBe((1L, 1L));
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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var user = await testData.AddMemberAsync(tenant.Id);
        var userId = await testData.UserIdOfAsync(user);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var renamed = await admin.PatchAsJsonAsync($"/v1/platform/users/{userId}", new { name = "Ada Lovelace" }, Cancellation);
        var deleted = await admin.DeleteAsync($"/v1/platform/users/{userId}", Cancellation);
        await Waiting.UntilAsync(async () => !worker.Identity.Deleted.IsEmpty && await PlatformEntriesAsync(name, userId, "user.deleted") > 0
            && await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0 && await PlatformEntriesAsync(name, userId, "user.renamed") > 0);
        await Waiting.UntilAsync(() => runs.In("worker").Contains(new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler))));

        (renamed.StatusCode, deleted.StatusCode).ShouldBe((HttpStatusCode.OK, HttpStatusCode.NoContent));
        runs.In("web").ShouldBe(
            [
                new HandlerRun(typeof(RenameUserCommand), typeof(RenameUserHandler)),
                new HandlerRun(typeof(ListUserMembershipsQuery), typeof(ListUserMembershipsHandler)),
                new HandlerRun(typeof(RemoveUserMembershipCommand), typeof(RemoveUserMembershipHandler)),
                new HandlerRun(typeof(DeleteUserCommand), typeof(DeleteUserHandler)),
            ],
            ignoreOrder: true);
        runs.WithoutNotificationsIn("worker").ShouldBe(
            [
                new HandlerRun(typeof(UserRenamedEvent), typeof(RecordUserRenamedHandler)),
                new HandlerRun(typeof(UserMembershipRemovedEvent), typeof(RecordUserMembershipRemovedHandler)),
                new HandlerRun(typeof(UserDeletedEvent), typeof(RecordUserDeletedHandler)),
                new HandlerRun(typeof(DeleteIdentityAccountCommand), typeof(DeleteIdentityAccountHandler)),
                new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler)),
            ],
            ignoreOrder: true);
        worker.Identity.Deleted.ShouldBe([user]);
        (await PlatformEntriesAsync(name, userId, "user.renamed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, userId, "user.deleted")).ShouldBe(1L);
        (await AuditEntriesAsync(name, tenant.Id, "member.removed")).ShouldBe(1L);
    }

    // A8: a user's deletion cancels a pending invitation to their address in a tenant they leave, in the web host. The identity provider's
    // invitation behind it is withdrawn in the worker, once, as for a revocation.
    [Fact]
    public async Task DeleteUser_APendingInvitationToTheirAddressOnAWebAndAWorkerHost_WithdrawsItsSignUpOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await testData.AddMemberAsync(tenant.Id, await testData.AddUserAsync(email: email));
        var pending = await testData.AddInvitationAsync(tenant.Id, email);
        var signUp = $"inv_{Guid.NewGuid():N}";
        await database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET sign_up_invitation_id = '{signUp}' WHERE id = '{pending}'", database: name);
        var userId = await testData.UserIdOfAsync(user);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var deleted = await admin.DeleteAsync($"/v1/platform/users/{userId}", Cancellation);
        await Waiting.UntilAsync(async () => !worker.Identity.Deleted.IsEmpty && !worker.Identity.Withdrawn.IsEmpty
            && await PlatformEntriesAsync(name, userId, "user.deleted") > 0 && await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0);
        await Waiting.UntilAsync(() => runs.In("worker").Contains(new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler))));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.WithoutNotificationsIn("worker").ShouldBe(
            [
                new HandlerRun(typeof(UserMembershipRemovedEvent), typeof(RecordUserMembershipRemovedHandler)),
                new HandlerRun(typeof(WithdrawSignUpCommand), typeof(WithdrawSignUpHandler)),
                new HandlerRun(typeof(UserDeletedEvent), typeof(RecordUserDeletedHandler)),
                new HandlerRun(typeof(DeleteIdentityAccountCommand), typeof(DeleteIdentityAccountHandler)),
                new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler)),
            ],
            ignoreOrder: true);
        worker.Identity.Withdrawn.ShouldBe([signUp]);
        web.Identity.Withdrawn.ShouldBeEmpty();
    }

    // A8: a user's deletion erases their address in a tenant they left before. The web host erases the user; the erasure in that tenant
    // and the withdrawal of the sign-up it cancelled run in the worker, once each.
    [Fact]
    public async Task DeleteUser_AnInvitationAcceptedInATenantTheyLeftOnAWebAndAWorkerHost_ErasesItAndWithdrawsItsSignUpInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = await testData.AddUserAsync(email: email);
        await testData.AddInvitationAsync(tenant.Id, email, status: "Accepted", acceptedBy: user);
        var pending = await testData.AddInvitationAsync(tenant.Id, email);
        var signUp = $"inv_{Guid.NewGuid():N}";
        await database.ExecuteInTenantAsync(tenant.Id, $"UPDATE control_plane.invitations SET sign_up_invitation_id = '{signUp}' WHERE id = '{pending}'", database: name);
        var userId = await testData.UserIdOfAsync(user);
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var deleted = await admin.DeleteAsync($"/v1/platform/users/{userId}", Cancellation);
        await Waiting.UntilAsync(async () => !worker.Identity.Deleted.IsEmpty && !worker.Identity.Withdrawn.IsEmpty
            && await PlatformEntriesAsync(name, userId, "user.deleted") > 0);
        await Waiting.UntilAsync(() => runs.In("worker").Contains(new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler))));

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe(
            [
                new HandlerRun(typeof(ListUserMembershipsQuery), typeof(ListUserMembershipsHandler)),
                new HandlerRun(typeof(DeleteUserCommand), typeof(DeleteUserHandler)),
            ],
            ignoreOrder: true);
        runs.WithoutNotificationsIn("worker").ShouldBe(
            [
                new HandlerRun(typeof(UserDeletedEvent), typeof(RecordUserDeletedHandler)),
                new HandlerRun(typeof(DeleteIdentityAccountCommand), typeof(DeleteIdentityAccountHandler)),
                new HandlerRun(typeof(DeleteUserFilesCommand), typeof(DeleteUserFilesHandler)),
                new HandlerRun(typeof(EraseFormerMemberCommand), typeof(EraseFormerMemberHandler)),
                new HandlerRun(typeof(WithdrawSignUpCommand), typeof(WithdrawSignUpHandler)),
            ],
            ignoreOrder: true);
        worker.Identity.Withdrawn.ShouldBe([signUp]);
        web.Identity.Withdrawn.ShouldBeEmpty();
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
        var testData = new TestData(database, name);
        var user = await testData.AddUserAsync();
        var userId = await testData.UserIdOfAsync(user);

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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(
            await testData.AddMemberAsync(tenant.Id, await testData.AddUserAsync(name: "Ada Lovelace", language: "tr"), role: "Owner"));

        var invited = await owner.InviteMemberAsync(tenant.Id, "grace@navy.test", role: (await testData.RoleIdOfAsync(tenant.Id, "Member")).ToString());
        await Waiting.UntilAsync(async () => worker.Email.Sent.Count > 0 && await AuditEntriesAsync(name, tenant.Id, "member.invited") > 0);

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        runs.In("web").ShouldBe([new HandlerRun(typeof(InviteMemberCommand), typeof(InviteMemberHandler))]);
        runs.In("worker").ShouldBe(
            [
                new HandlerRun(typeof(MemberInvitedEvent), typeof(RecordMemberInvitedHandler)),
                new HandlerRun(typeof(OpenSignUpCommand), typeof(OpenSignUpHandler)),
                new HandlerRun(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
                new HandlerRun(typeof(InvitationReadyEvent), typeof(SendInvitationEmailHandler)),
            ],
            ignoreOrder: true);
        var email = worker.Email.Sent.ShouldHaveSingleItem();
        (email.To, email.Subject).ShouldBe(("grace@navy.test", "Ada Lovelace sizi Acme Ltd kuruluşuna davet etti"));
        email.Text.Split("\n\n")[1].ShouldBe("Ada Lovelace sizi Acme Ltd kuruluşuna Üye olarak katılmaya davet etti.");
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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        await testData.AddInvitationAsync(tenant.Id, role: "Owner");
        var admin = web.CreateClient(await testData.AddSystemAdminAsync(), secondFactor: true);

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
                new HandlerRun(typeof(OpenSignUpCommand), typeof(OpenSignUpHandler)),
                new HandlerRun(typeof(RecordSignUpCommand), typeof(RecordSignUpHandler)),
                new HandlerRun(typeof(InvitationReadyEvent), typeof(SendInvitationEmailHandler)),
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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var invitation = await testData.AddInvitationAsync(tenant.Id);

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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await testData.UserIdOfAsync(await testData.AddMemberAsync(tenant.Id));

        var changed = await owner.ChangeMemberRoleAsync(tenant.Id, member, (await testData.RoleIdOfAsync(tenant.Id, "Admin")).ToString());
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.role_changed") > 0);

        changed.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ChangeMemberRoleCommand), typeof(ChangeMemberRoleHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(MemberRoleChangedEvent), typeof(RecordMemberRoleChangedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.role_changed")).ShouldBe(1L);
    }

    // U6: the worker writes the notification, and its commit publishes the signal on Valkey; the web host that holds the member's stream hears
    // it and writes it to that stream. Without Valkey the worker's signal would stay in the worker. The stream's first comment line shows it
    // is open, and its subscription is in place.
    [Fact]
    public async Task StreamNotifications_OnAWebAndAWorkerHostWithValkey_TheWorkersNotificationReachesTheWebStream()
    {
        await using var valkey = new Valkey();
        await valkey.InitializeAsync();
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        Dictionary<string, string?> settings = new()
        {
            ["ConnectionStrings:Cache"] = valkey.ConnectionString,
            ["Notifications:LiveStream:KeepAliveInterval"] = "00:00:00.500",
        };
        await using var web = Host(name, "web", runs, settings: settings);
        await using var worker = Host(name, "worker", runs, settings: settings);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await testData.AddMemberAsync(tenant.Id);
        await using var stream = await LiveStream.OpenAsync(web.CreateClient(member));
        (await stream.NextAsync(TimeSpan.FromSeconds(10))).ShouldBe(LiveStream.KeepAlive);

        await owner.ChangeMemberRoleAsync(tenant.Id, await testData.UserIdOfAsync(member), (await testData.RoleIdOfAsync(tenant.Id, "Admin")).ToString());

        (await stream.NextEventAsync(TimeSpan.FromSeconds(20))).ShouldBe($$"""
            event: notifications
            data: {"scope":"tenant","tenantId":"{{tenant.Id}}"}
            """);
        runs.In("worker").ShouldContain(new HandlerRun(typeof(MemberRoleChangedEvent), typeof(NotifyMemberRoleChangedHandler)));
        runs.In("worker").ShouldContain(new HandlerRun(typeof(DeliverNotificationCommand), typeof(DeliverNotificationHandler)));
        runs.In("web").ShouldNotContain(new HandlerRun(typeof(DeliverNotificationCommand), typeof(DeliverNotificationHandler)));
    }

    // Owner recovery (docs/flows/owner-recovery.md): the platform's new Owner is accepted by the web host, and its records in both logs run in the worker,
    // once: tenant.owner_made in the platform's log and member.role_changed in the tenant's.
    [Fact]
    public async Task MakeOwner_OnAWebAndAWorkerHost_RecordsInBothLogsOnceInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var member = await testData.UserIdOfAsync(await testData.AddMemberAsync(tenant.Id));
        var admin = web.CreateClient(await testData.AddSystemAdminAsync(), secondFactor: true);

        var made = await admin.MakeOwnerAsync(tenant.Id, member);
        await Waiting.UntilAsync(async () =>
            await AuditEntriesAsync(name, tenant.Id, "member.role_changed") > 0 && await PlatformEntriesAsync(name, tenant.Id, "tenant.owner_made") > 0);

        made.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(MakeOwnerCommand), typeof(MakeOwnerHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(OwnerMadeEvent), typeof(RecordOwnerMadeHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.role_changed")).ShouldBe(1L);
        (await PlatformEntriesAsync(name, tenant.Id, "tenant.owner_made")).ShouldBe(1L);
    }

    // O3: each change to a custom role is announced from the web host, and its Audit handler runs in the worker, once (step r4b).
    [Fact]
    public async Task DeleteRole_AfterACreationAndAnUpdateOnAWebAndAWorkerHost_WritesOneAuditEntryEachInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner"));

        var created = await owner.CreateRoleAsync(tenant.Id, "Billing", ["tenant.profile.read", "tenant.members.read"]);
        var roleId = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();
        var updated = await owner.UpdateRoleAsync(tenant.Id, roleId, "Finance", ["tenant.profile.read", "tenant.members.read", "tenant.audit.read"]);
        var deleted = await owner.DeleteRoleAsync(tenant.Id, roleId);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "role.deleted") > 0
            && await AuditEntriesAsync(name, tenant.Id, "role.created") > 0
            && await AuditEntriesAsync(name, tenant.Id, "role.updated") > 0);

        (created.StatusCode, updated.StatusCode, deleted.StatusCode).ShouldBe((HttpStatusCode.Created, HttpStatusCode.OK, HttpStatusCode.NoContent));
        runs.In("web").ShouldBe(
        [
            new HandlerRun(typeof(CreateRoleCommand), typeof(CreateRoleHandler)),
            new HandlerRun(typeof(UpdateRoleCommand), typeof(UpdateRoleHandler)),
            new HandlerRun(typeof(DeleteRoleCommand), typeof(DeleteRoleHandler)),
        ]);
        runs.In("worker").ShouldBe(
            [
                new HandlerRun(typeof(RoleCreatedEvent), typeof(RecordRoleCreatedHandler)),
                new HandlerRun(typeof(RoleUpdatedEvent), typeof(RecordRoleUpdatedHandler)),
                new HandlerRun(typeof(RoleDeletedEvent), typeof(RecordRoleDeletedHandler)),
            ],
            ignoreOrder: true);
        (await AuditEntriesAsync(name, tenant.Id, "role.created"), await AuditEntriesAsync(name, tenant.Id, "role.updated"), await AuditEntriesAsync(name, tenant.Id, "role.deleted"))
            .ShouldBe((1L, 1L, 1L));
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
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        var owner = web.CreateClient(await testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var member = await testData.UserIdOfAsync(await testData.AddMemberAsync(tenant.Id));

        var removed = await owner.RemoveMemberAsync(tenant.Id, member);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.removed") > 0);

        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe([new HandlerRun(typeof(RemoveMemberCommand), typeof(RemoveMemberHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(MemberRemovedEvent), typeof(RecordMemberRemovedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.removed")).ShouldBe(1L);
    }

    // O3: a leave is announced from the web host, and its Audit handler runs in the worker, once.
    [Fact]
    public async Task LeaveTenant_OnAWebAndAWorkerHost_WritesOneAuditEntryInTheWorker()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        var runs = new HandlerRuns();
        await using var web = Host(name, "web", runs);
        await using var worker = Host(name, "worker", runs);
        _ = worker.Services;
        var testData = new TestData(database, name);
        var tenant = await testData.AddTenantAsync();
        await testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = web.CreateClient(await testData.AddMemberAsync(tenant.Id));

        var left = await member.LeaveTenantAsync(tenant.Id);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "member.left") > 0);

        left.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        runs.In("web").ShouldBe([new HandlerRun(typeof(LeaveTenantCommand), typeof(LeaveTenantHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(MemberLeftEvent), typeof(RecordMemberLeftHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "member.left")).ShouldBe(1L);
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
        var tenant = await new TestData(database, name).AddTenantAsync(status: "Suspended");
        var admin = web.CreateClient(await AddSystemAdminAsync(name), secondFactor: true);

        var reactivated = await admin.PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation);
        await Waiting.UntilAsync(async () => await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated") > 0);

        reactivated.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.In("web").ShouldBe([new HandlerRun(typeof(ReactivateTenantCommand), typeof(ReactivateTenantHandler))]);
        runs.WithoutNotificationsIn("worker").ShouldBe([new HandlerRun(typeof(TenantReactivatedEvent), typeof(RecordTenantReactivatedHandler))]);
        (await AuditEntriesAsync(name, tenant.Id, "tenant.reactivated")).ShouldBe(1L);
    }

    // The spike's T08_separated_handlers_commit_independently in the production layout (O3, W4): each handler of an event runs in a
    // worker, in its own transaction. The one that throws goes to the dead letter queue; the other commits its row once.
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

    // Only the audit retention's own test keeps the job in its hosts.
    private ApiFactory Host(
        string databaseName,
        string role,
        HandlerRuns runs,
        HostPorts? ports = null,
        IReadOnlyDictionary<string, string?>? settings = null,
        bool auditRetention = false,
        bool featureFlagJob = false) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?>(settings ?? new Dictionary<string, string?>()) { ["Host:Role"] = role },
        configureServices: services =>
        {
            HandlerRunsOfHost.Register(role, runs)(services);
            if (!featureFlagJob)
            {
                ApiFactory.RemoveFeatureFlagJob(services);
            }

            if (!auditRetention)
            {
                ApiFactory.RemoveAuditRetention(services);
            }
        },
        ports: ports);


    // A host with the probe module's DbContext, the two handlers of ProbePinged, and a handler that writes and one that always fails.
    private ApiFactory ProbeHost(string databaseName, string role, HandlerRuns runs) => new(
        database.ConnectionStringFor(DatabaseRoles.Application, databaseName),
        settings: new Dictionary<string, string?> { ["Host:Role"] = role },
        configureServices: services =>
        {
            HandlerRunsOfHost.Register(role, runs)(services);
            ApiFactory.RemoveAuditRetention(services);
            ApiFactory.RemoveFeatureFlagJob(services);
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

    private Task<long> NotificationsAsync(string databaseName, Guid tenantId, string type) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM notifications.notifications WHERE tenant_id = '{tenantId}' AND type = '{type}'", databaseName);

    private Task<long> AuditEntriesAsync(string databaseName, Guid tenantId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = '{operation}'", databaseName);

    // The tenant's Activity log period in its row of the retention; 0 while it has no row or no limit.
    private async Task<int> RetentionDaysAsync(string databaseName, Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<int?>($"SELECT days FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'", databaseName) ?? 0;

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
}
