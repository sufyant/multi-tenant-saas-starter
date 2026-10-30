using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
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
        var owner = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(firstName: "Ada", lastName: "Lovelace"), role: "Owner");
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

        var (person, _) = await new PlatformTenants(database, _api).AcceptAsync(await PlatformTenants.InvitationIdOfAsync(invited), email, "Grace", "Hopper");

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

        (await _rows.CountAboutAsync(tenantId, "member.joined"), await _rows.CountAboutAsync(tenantId, "tenant.owner_joined")).ShouldBe(
            (0L, await database.ScalarAsSuperuserAsync<long>("SELECT count(*) FROM control_plane.system_admins")));
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

    // The plan's value applies again; a key the plan has no value of is closed, and reads as "not set".
    [Theory]
    [InlineData("members.max", "unlimited")]
    [InlineData("roles.custom.max", "not_set")]
    public async Task NotifyTenantEntitlementOverrideRemoved_TheOverrideGoes_WritesOneForEachOwnerWithThePlansValue(string key, string value)
    {
        var tenant = await _testData.AddTenantAsync(planId: await _testData.AddPlanAsync(new() { ["members.max"] = TestData.Unlimited }));
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
        (await _rows.ValueListOfAsync(otherId, "tenant.owner_joined")).ShouldEndWith($"ownerName={ownerId}, tenantName=Acme Ltd");
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
        (await _rows.ValueListOfAsync(otherId, "tenant.suspended")).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantReactivated_ASystemAdminReactivatesATenant_WritesOneForEveryOtherSystemAdmin()
    {
        var tenant = await _testData.AddTenantAsync(status: "Suspended", suspensionReason: "Unpaid invoices");
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).PostAsync($"/v1/platform/tenants/{tenant.Id}/reactivate", null, Cancellation));

        var (actorId, otherId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId)).ShouldBe([NotificationRows.Row(otherId, "tenant.reactivated", tenant.Id, "Platform")]);
        (await _rows.ValueListOfAsync(otherId, "tenant.reactivated")).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
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
        var values = await _rows.ValueListOfAsync(otherId, "tenant.closed");
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
        (await _rows.ValueListOfAsync(otherId, "tenant.reopened")).ShouldBe($"actorName={actorId}, tenantName=Acme Ltd");
    }

    [Fact]
    public async Task NotifyTenantPurged_ThePurgeFinishes_WritesOneForEverySystemAdmin()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), "Acme Ltd");

        await PublishAsync(purged);

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _rows.AboutAsync(purged.TenantId, adminId)).ShouldBe([NotificationRows.Row(adminId, "tenant.purged", purged.TenantId, "Platform")]);
        (await _rows.ValueListOfAsync(adminId, "tenant.purged")).ShouldBe("tenantName=Acme Ltd");
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
}
