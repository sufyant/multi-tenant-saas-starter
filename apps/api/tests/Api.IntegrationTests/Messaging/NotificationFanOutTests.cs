using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Notifications.DeliverNotification;
using Notifications.Domain;
using SharedKernel;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// U3, U4: each type reaches exactly its recipients when its event arrives, one notification each, in the right bell, and never the person
// who acted. It keeps a person by user id only, never a name (K3, U2). The commands run through the API, so the events carry what
// ControlPlane publishes. Every system admin of the shared database hears every platform event of every test, so a platform type is read
// only about the test's own tenant.
public sealed class NotificationFanOutTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly NotificationRows _rows = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task NotifyMemberRoleChanged_AnOwnerChangesAMembersRole_WritesOneForTheMemberWithTheOwnerByIdAndBothRoles()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        var (ownerId, memberId) = (await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(member));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, memberId, "admin"));

        (await _rows.OfAsync(ownerId, memberId)).ShouldBe([NotificationRows.Row(memberId, "member.role_changed", tenant.Id, "Tenant")]);
        (await _rows.ValueListOfAsync(memberId, "member.role_changed")).ShouldBe(
            $"actorName={ownerId}, newRoleName=admin, newRoleNameBuiltIn=true, oldRoleName=member, oldRoleNameBuiltIn=true, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyMemberRoleChanged_AnOwnerChangesTheirOwnRole_WritesNone()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var ownerId = await _testData.UserIdOfAsync(owner);

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, ownerId, "admin"));

        (await _rows.OfAsync(ownerId)).ShouldBeEmpty();
    }

    // The tenant's Activity names the platform, not the system admin, so the notification keeps no actor.
    [Fact]
    public async Task NotifyMemberRoleChanged_ThePlatformMakesAMemberAnOwner_WritesOneForTheMemberWithoutAnActor()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var memberId = await _testData.UserIdOfAsync(member);
        var admin = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(admin, secondFactor: true).MakeOwnerAsync(tenant.Id, memberId));

        (await _rows.OfAsync(memberId)).ShouldBe([NotificationRows.Row(memberId, "member.role_changed", tenant.Id, "Tenant")]);
        (await _rows.ValueListOfAsync(memberId, "member.role_changed")).ShouldBe(
            "newRoleName=owner, newRoleNameBuiltIn=true, oldRoleName=member, oldRoleNameBuiltIn=true, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyMemberRemoved_AnOwnerRemovesAMember_WritesAPersonalOneWithTheOwnerByIdAndSendsTheRequiredEmail()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(name: "Ada Lovelace"), role: "Owner");
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));
        var (ownerId, memberId) = (await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(member));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, memberId));

        (await _rows.OfAsync(ownerId, memberId)).ShouldBe([NotificationRows.Row(memberId, "member.removed", null, "Personal")]);
        (await _rows.ValueListOfAsync(memberId, "member.removed")).ShouldBe($"actorName={ownerId}, tenantName=Acme Ltd");
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Text).ShouldHaveSingleItem().ShouldContain("Ada Lovelace");
    }

    // The platform removes a member only in the user's own deletion: the person is deleted and nobody is left to read a notification.
    [Fact]
    public async Task NotifyMemberRemoved_ASystemAdminDeletesAMember_WritesNone()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var memberId = await _testData.UserIdOfAsync(member);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        (await _api.WaitingForMessagesAsync(() => admin.DeleteAsync($"/v1/platform/users/{memberId}", Cancellation))).EnsureSuccessStatusCode();

        (await _rows.CountAboutAsync(tenant.Id, "member.removed"), (await _rows.OfAsync(memberId)).Length).ShouldBe((0L, 0));
    }

    [Fact]
    public async Task NotifyMemberJoined_APersonAcceptsAMembersInvitation_WritesOneForTheInviterWithTheMemberByIdAndTheirRole()
    {
        var tenant = await _testData.AddTenantAsync();
        var inviter = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var email = PlatformTenants.Email();
        var invited = await _api.WaitingForMessagesAsync(() => _api.CreateClient(inviter).InviteMemberAsync(tenant.Id, email));

        var (person, _) = await new PlatformTenants(database, _api).AcceptAsync(await PlatformTenants.InvitationIdOfAsync(invited), email, "Grace Hopper");

        var (inviterId, personId) = (await _testData.UserIdOfAsync(inviter), await _testData.UserIdOfAsync(person));
        (await _rows.OfAsync(inviterId, personId)).ShouldBe([NotificationRows.Row(inviterId, "member.joined", tenant.Id, "Tenant")]);
        (await _rows.ValueListOfAsync(inviterId, "member.joined")).ShouldBe(
            $"memberName={personId}, roleName=member, roleNameBuiltIn=true, tenantName=Acme Ltd");
    }

    // The platform's Owner invitation names no inviter in the tenant: only the system admins hear of it, as tenant.owner_joined.
    [Fact]
    public async Task NotifyMemberJoined_ThePlatformsOwnerInvitationIsAccepted_WritesNone()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var tenants = new PlatformTenants(database, _api);
        var email = PlatformTenants.Email();
        var tenantId = await tenants.CreateAsync(_api.CreateClient(admin, secondFactor: true), email);

        await tenants.AcceptAsync(await tenants.LatestInvitationAsync(tenantId), email);

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _rows.CountAboutAsync(tenantId, "member.joined")).ShouldBe(0);
        (await _rows.AboutAsync(tenantId, adminId)).ShouldBe([NotificationRows.Row(adminId, "tenant.owner_joined", tenantId, "Platform")]);
    }

    // A role-based type: the Owners at that moment get it, the Admin does not, and a member who becomes an Owner later sees no older one.
    [Fact]
    public async Task NotifyTenantPlanChanged_ThePlatformChangesThePlan_WritesOneForEachOwnerThenWithBothPlansAndNoneForALaterOwner()
    {
        var tenant = await _testData.AddTenantAsync();
        var (first, second) = (await _testData.AddMemberAsync(tenant.Id, role: "Owner"), await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var later = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var plan = await _testData.AddPlanAsync([], $"Scale {Guid.NewGuid():N}");

        var (firstId, secondId, laterId) = (await _testData.UserIdOfAsync(first), await _testData.UserIdOfAsync(second), await _testData.UserIdOfAsync(later));

        await _api.WaitingForMessagesAsync(() => platform.ChangeTenantPlanAsync(tenant.Id, plan));
        await _api.WaitingForMessagesAsync(() => platform.MakeOwnerAsync(tenant.Id, laterId));

        (await _rows.OfAsync(firstId, secondId, laterId)).Order().ShouldBe(
            new[]
            {
                NotificationRows.Row(firstId, "tenant.plan_changed", tenant.Id, "Tenant"),
                NotificationRows.Row(secondId, "tenant.plan_changed", tenant.Id, "Tenant"),
                NotificationRows.Row(laterId, "member.role_changed", tenant.Id, "Tenant"),
            }.Order());
        (await _rows.ValueListOfAsync(firstId, "tenant.plan_changed")).ShouldStartWith("newPlanName=Scale ");
        (await _rows.ValueListOfAsync(firstId, "tenant.plan_changed")).ShouldEndWith(", oldPlanName=Pro, tenantName=Acme Ltd");
    }

    // K14: a plan change that shortens the Activity log period names the new period and the moment the older entries go, the event's
    // moment plus the hold of 30 days.
    [Fact]
    public async Task NotifyTenantPlanChanged_APlanThatShortensTheActivityLog_WritesThePeriodAndWhenTheOlderEntriesGo()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var plan = await _testData.AddPlanAsync(new() { ["audit.retention.days"] = 90 }, $"Short {Guid.NewGuid():N}");
        var before = DateTimeOffset.UtcNow;

        await _api.WaitingForMessagesAsync(() => platform.ChangeTenantPlanAsync(tenant.Id, plan));

        var values = await _rows.ValueListOfAsync(await _testData.UserIdOfAsync(owner), "tenant.plan_changed");
        values.ShouldContain(", retentionDays=90, retentionGoesAt=");
        var goesAt = DateTimeOffset.Parse(values.Split("retentionGoesAt=")[1].Split(", ")[0], System.Globalization.CultureInfo.InvariantCulture);
        goesAt.ShouldBeInRange(before.AddDays(30), DateTimeOffset.UtcNow.AddDays(30));
    }

    [Fact]
    public async Task NotifyTenantEntitlementOverrideSet_AnOverrideWithAnEnd_WritesOneForEachOwnerWithTheKeyTheValueAndTheEnd()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var end = new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);

        await _api.WaitingForMessagesAsync(() => platform.SetOverrideAsync(tenant.Id, "members.max", new { maximum = 50 }, end));

        var ownerId = await _testData.UserIdOfAsync(owner);
        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "tenant.entitlement_override_set", tenant.Id, "Tenant")]);
        (await _rows.ValueListOfAsync(ownerId, "tenant.entitlement_override_set")).ShouldBe(
            "expiresAt=2027-01-01T00:00:00.0000000+00:00, keyName=members.max, tenantName=Acme Ltd, value=50");
    }

    [Fact]
    public async Task NotifyTenantEntitlementOverrideSet_AnOverrideWithoutAnEnd_KeepsNoEnd()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        await _api.WaitingForMessagesAsync(() => platform.SetOverrideAsync(tenant.Id, "roles.custom", new { enabled = false }));

        (await _rows.ValueListOfAsync(await _testData.UserIdOfAsync(owner), "tenant.entitlement_override_set")).ShouldBe(
            "keyName=roles.custom, tenantName=Acme Ltd, value=off");
    }

    // The plan's value applies again; a key the plan has no value of is closed, and reads as "not set". The plan has roles.custom on, so
    // an override of its limit can be set (E1).
    [Theory]
    [InlineData("members.max", "unlimited")]
    [InlineData("roles.custom.max", "not_set")]
    public async Task NotifyTenantEntitlementOverrideRemoved_TheOverrideGoes_WritesOneForEachOwnerWithThePlansValue(string key, string value)
    {
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new() { ["roles.custom"] = true, ["members.max"] = TestData.Unlimited }));
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        await _api.WaitingForMessagesAsync(() => platform.SetOverrideAsync(tenant.Id, key, new { maximum = 5 }));

        await _api.WaitingForMessagesAsync(() => platform.RemoveOverrideAsync(tenant.Id, key));

        (await _rows.ValueListOfAsync(await _testData.UserIdOfAsync(owner), "tenant.entitlement_override_removed")).ShouldBe(
            $"keyName={key}, tenantName=Acme Ltd, value={value}");
    }

    [Fact]
    public async Task NotifyTenantOwnerJoined_TheFirstOwnerOfANewTenantJoins_WritesOneForEverySystemAdminWithTheOwnerById()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var other = await _testData.AddSystemAdminAsync();
        var tenants = new PlatformTenants(database, _api);
        var email = PlatformTenants.Email();
        var tenantId = await tenants.CreateAsync(_api.CreateClient(admin, secondFactor: true), email);

        var (owner, _) = await tenants.AcceptAsync(await tenants.LatestInvitationAsync(tenantId), email);

        var (adminId, otherId, ownerId) = (await _testData.UserIdOfAsync(admin), await _testData.UserIdOfAsync(other), await _testData.UserIdOfAsync(owner));
        (await _rows.AboutAsync(tenantId, adminId, otherId, ownerId)).Order().ShouldBe(
            new[]
            {
                NotificationRows.Row(adminId, "tenant.owner_joined", tenantId, "Platform"),
                NotificationRows.Row(otherId, "tenant.owner_joined", tenantId, "Platform"),
            }.Order());
        (await _rows.ValueListOfAsync(otherId, "tenant.owner_joined", tenantId)).ShouldEndWith($"ownerName={ownerId}, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantSuspended_ASystemAdminSuspendsATenant_WritesOneForEveryOtherSystemAdminWithTheActorById()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).SuspendTenantAsync(tenant.Id));

        var (actorId, otherId, memberId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other), await _testData.UserIdOfAsync(member));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId, memberId)).ShouldBe([NotificationRows.Row(otherId, "tenant.suspended", tenant.Id, "Platform")]);
        (await _rows.ValueListOfAsync(otherId, "tenant.suspended", tenant.Id)).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantReactivated_ASystemAdminReactivatesATenant_WritesOneForEveryOtherSystemAdmin()
    {
        var tenant = await _testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var (actorId, otherId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId)).ShouldBe([NotificationRows.Row(otherId, "tenant.reactivated", tenant.Id, "Platform")]);
        (await _rows.ValueListOfAsync(otherId, "tenant.reactivated", tenant.Id)).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
    }

    // The deletion date is the end of the reopen period: 30 days after the closure.
    [Fact]
    public async Task NotifyTenantClosed_ASystemAdminClosesATenant_WritesOneForEveryOtherSystemAdminWithTheDeletionDate()
    {
        var tenant = await _testData.AddTenantAsync();
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).CloseTenantAsync(tenant.Id));

        var (actorId, otherId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId)).ShouldBe([NotificationRows.Row(otherId, "tenant.closed", tenant.Id, "Platform")]);
        // The table keeps the moment to the microsecond, the event to the tick: the two agree to the second.
        var reopenableUntil = await database.ScalarAsSuperuserAsync<string>(
            $"SELECT to_char((closed_at + interval '30 days') AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') FROM control_plane.tenants WHERE id = '{tenant.Id}'");
        var values = await _rows.ValueListOfAsync(otherId, "tenant.closed", tenant.Id);
        values.ShouldStartWith($"actorName={actorId}, reopenableUntil={reopenableUntil}.");
        values.ShouldEndWith("+00:00, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantReopened_ASystemAdminReopensATenant_WritesOneForEveryOtherSystemAdmin()
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).ReopenTenantAsync(tenant.Id));

        var (actorId, otherId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId)).ShouldBe([NotificationRows.Row(otherId, "tenant.reopened", tenant.Id, "Platform")]);
        (await _rows.ValueListOfAsync(otherId, "tenant.reopened", tenant.Id)).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantPurged_ThePurgeFinishes_WritesOneForEverySystemAdmin()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), "Acme Ltd");

        await PublishAsync(purged);

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _rows.AboutAsync(purged.TenantId, adminId)).ShouldBe([NotificationRows.Row(adminId, "tenant.purged", purged.TenantId, "Platform")]);
        (await _rows.ValueListOfAsync(adminId, "tenant.purged", purged.TenantId)).ShouldBe("tenantName=Acme Ltd");
    }

    // The Owners hear of their organisation's status in the personal bell and by email, because the tenant door may refuse them (U5).
    // The platform acts, so no system admin is named; an Admin is no Owner, and the acting system admin gets no Owner's notification.
    [Fact]
    public async Task NotifyTenantSuspendedForOwners_ASystemAdminSuspendsATenant_WritesAPersonalOneForEachOwnerAndEmailsThem()
    {
        var tenant = await _testData.AddTenantAsync();
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email), role: "Owner");
        var second = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var actor = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).SuspendTenantAsync(tenant.Id));

        var (ownerId, secondId, memberId, actorId) = (
            await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(second), await _testData.UserIdOfAsync(member), await _testData.UserIdOfAsync(actor));
        (await _rows.OfAsync(ownerId, secondId, memberId)).Order().ShouldBe(
            new[]
            {
                NotificationRows.Row(ownerId, "tenant.suspended_for_owners", null, "Personal"),
                NotificationRows.Row(secondId, "tenant.suspended_for_owners", null, "Personal"),
            }.Order());
        (await _rows.ValueListOfAsync(ownerId, "tenant.suspended_for_owners")).ShouldBe("tenantName=Acme Ltd");
        (await _rows.OfAsync(actorId)).ShouldAllBe(row => !row.Contains("_for_owners"));
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["Acme Ltd was suspended"]);
    }

    // Reactivated leads into the organisation, so the personal notification keeps the tenant it tells of (the maintainer's decision of
    // 2026-11-06).
    [Fact]
    public async Task NotifyTenantReactivatedForOwners_ASystemAdminReactivatesATenant_WritesAPersonalOneAboutTheTenantForEachOwner()
    {
        var tenant = await _testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var actor = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var ownerId = await _testData.UserIdOfAsync(owner);
        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "tenant.reactivated_for_owners", tenant.Id, "Personal")]);
        (await _rows.ValueListOfAsync(ownerId, "tenant.reactivated_for_owners")).ShouldBe("tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantClosedForOwners_ASystemAdminClosesATenant_WritesAPersonalOneForEachOwnerWithTheDeletionDate()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var actor = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).CloseTenantAsync(tenant.Id));

        var ownerId = await _testData.UserIdOfAsync(owner);
        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "tenant.closed_for_owners", null, "Personal")]);
        var reopenableUntil = await database.ScalarAsSuperuserAsync<string>(
            $"SELECT to_char((closed_at + interval '30 days') AT TIME ZONE 'UTC', 'YYYY-MM-DD\"T\"HH24:MI:SS') FROM control_plane.tenants WHERE id = '{tenant.Id}'");
        var values = await _rows.ValueListOfAsync(ownerId, "tenant.closed_for_owners");
        values.ShouldStartWith($"reopenableUntil={reopenableUntil}.");
        values.ShouldEndWith("+00:00, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantReopenedForOwners_ASystemAdminReopensATenant_WritesAPersonalOneAboutTheTenantForEachOwner()
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow.AddDays(-1));
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var actor = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).ReopenTenantAsync(tenant.Id));

        var ownerId = await _testData.UserIdOfAsync(owner);
        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "tenant.reopened_for_owners", tenant.Id, "Personal")]);
        (await _rows.ValueListOfAsync(ownerId, "tenant.reopened_for_owners")).ShouldBe("tenantName=Acme Ltd");
    }

    // The purge took the memberships away, so the event names the Owners it read before (TenantPurgedEvent.OwnerIds).
    [Fact]
    public async Task NotifyTenantPurgedForOwners_ThePurgeFinishes_WritesAPersonalOneForEachOwnerItNames()
    {
        var owner = await _testData.AddUserAsync();
        var ownerId = await _testData.UserIdOfAsync(owner);
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), "Acme Ltd", [ownerId]);

        await PublishAsync(purged);

        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "tenant.purged_for_owners", null, "Personal")]);
        (await _rows.ValueListOfAsync(ownerId, "tenant.purged_for_owners")).ShouldBe("tenantName=Acme Ltd");
    }

    // An event of the version before step 37a names no Owner, so it tells none.
    [Fact]
    public async Task NotifyTenantPurgedForOwners_AnEventWithoutOwners_WritesNone()
    {
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), $"Gone {Guid.NewGuid():N}");

        await PublishAsync(purged);

        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM notifications.notifications WHERE type = 'tenant.purged_for_owners' AND \"values\"->>'tenantName' = '{purged.TenantName}'"))
            .ShouldBe(0);
    }

    // O4: a copy of an event writes no second notification and sends no second email.
    [Fact]
    public async Task NotifyTenantPurged_TheEventArrivesTwice_WritesOne()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), "Acme Ltd");
        await PublishAsync(purged, purged);

        (await _rows.AboutAsync(purged.TenantId, await _testData.UserIdOfAsync(admin))).Length.ShouldBe(1);
    }

    // U4, U5: delivery takes a type's channels from the catalog only. A new member is team news, so it goes to the bell alone, even to a
    // recipient with an address.
    [Fact]
    public async Task DeliverNotification_ATypeOfTheWebOnly_WritesTheRowAndSendsNoEmail()
    {
        var email = PlatformTenants.Email();
        var owner = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, await _testData.AddUserAsync(email: email), role: "Owner");
        var ownerId = await _testData.UserIdOfAsync(owner);
        var tenantId = Guid.CreateVersion7();
        var deliver = new DeliverNotificationCommand(
            Guid.CreateVersion7(),
            NotificationCatalog.MemberJoined,
            ownerId,
            tenantId,
            new Dictionary<string, string>
            {
                ["tenantName"] = "Acme Ltd",
                ["memberName"] = Guid.CreateVersion7().ToString(),
                ["roleName"] = "member",
                ["roleNameBuiltIn"] = "true",
            },
            DateTimeOffset.UtcNow,
            email,
            Language.En);

        await SendAsync(deliver);

        (await _rows.OfAsync(ownerId)).ShouldBe([NotificationRows.Row(ownerId, "member.joined", tenantId, "Tenant")]);
        _api.Email.Sent.Where(sent => sent.To == email).ShouldBeEmpty();
    }

    // U4, U5: a changed role is a change of the member's access, so it goes to the bell and by email, as the catalog says.
    [Fact]
    public async Task DeliverNotification_AChangedRole_WritesTheRowAndSendsOneEmail()
    {
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, await _testData.AddUserAsync(email: email));
        var memberId = await _testData.UserIdOfAsync(member);
        var tenantId = Guid.CreateVersion7();
        var deliver = new DeliverNotificationCommand(
            Guid.CreateVersion7(),
            NotificationCatalog.MemberRoleChanged,
            memberId,
            tenantId,
            new Dictionary<string, string>
            {
                ["tenantName"] = "Acme Ltd",
                ["oldRoleName"] = "member",
                ["oldRoleNameBuiltIn"] = "true",
                ["newRoleName"] = "admin",
                ["newRoleNameBuiltIn"] = "true",
            },
            DateTimeOffset.UtcNow,
            email,
            Language.En);

        await SendAsync(deliver);

        (await _rows.OfAsync(memberId)).ShouldBe([NotificationRows.Row(memberId, "member.role_changed", tenantId, "Tenant")]);
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["Your role in Acme Ltd changed"]);
    }

    // U4, O4: a removed member's notification goes to the bell and by email, as the catalog says. A copy of the delivery finds the row,
    // so it writes no second row and sends no second email.
    [Fact]
    public async Task DeliverNotification_TwiceForATypeOfTheWebAndTheEmail_WritesOneRowAndSendsOneEmail()
    {
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, await _testData.AddUserAsync(email: email));
        var memberId = await _testData.UserIdOfAsync(member);
        var deliver = new DeliverNotificationCommand(
            Guid.CreateVersion7(),
            NotificationCatalog.MemberRemoved,
            memberId,
            null,
            new Dictionary<string, string> { ["tenantName"] = "Acme Ltd", ["actorName"] = Guid.CreateVersion7().ToString() },
            DateTimeOffset.UtcNow,
            email,
            Language.En);

        await SendAsync(deliver, deliver);

        (await _rows.OfAsync(memberId)).ShouldBe([NotificationRows.Row(memberId, "member.removed", null, "Personal")]);
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["You were removed from Acme Ltd"]);
    }

    // The events, as ControlPlane publishes them, and every message they cause.
    private Task<int> PublishAsync(params object[] events) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            foreach (var published in events)
            {
                await bus.PublishAsync(published);
            }

            return events.Length;
        });

    // The messages, as a fan-out sends them, and every message they cause.
    private Task<int> SendAsync(params object[] messages) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();
            foreach (var message in messages)
            {
                await bus.SendAsync(message);
            }

            return messages.Length;
        });
}
