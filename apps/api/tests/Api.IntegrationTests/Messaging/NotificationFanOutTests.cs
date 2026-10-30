using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Messaging;

// U3, U4: each of the seven types reaches exactly its recipients when its event arrives, one notification each, in the right bell, and
// never the person who acted. The commands run through the API, so the events carry what ControlPlane publishes. Every system admin of the
// shared database hears every platform event of every test, so a platform type is read only about the test's own tenant.
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
    public async Task NotifyMemberRoleChanged_AnOwnerChangesAMembersRole_WritesOneForTheMemberInTheTenantsBellAndNoneForTheOwner()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var member = await _testData.AddMemberAsync(tenant.Id);
        var (ownerId, memberId) = (await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(member));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).ChangeMemberRoleAsync(tenant.Id, memberId, "admin"));

        (await _rows.OfAsync(ownerId, memberId)).ShouldBe([NotificationRows.Row(memberId, "member.role_changed", tenant.Id, "Tenant")]);
        (await _rows.ValuesOfAsync(memberId, "member.role_changed")).ShouldBe("""{"role": "admin", "tenantName": "Acme Ltd", "roleBuiltIn": "true"}""");
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

    [Fact]
    public async Task NotifyMemberRoleChanged_ThePlatformMakesAMemberAnOwner_WritesOneForTheMember()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id);
        var memberId = await _testData.UserIdOfAsync(member);
        var admin = await _testData.AddSystemAdminAsync();

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(admin, secondFactor: true).MakeOwnerAsync(tenant.Id, memberId));

        (await _rows.OfAsync(memberId)).ShouldBe([NotificationRows.Row(memberId, "member.role_changed", tenant.Id, "Tenant")]);
    }

    [Fact]
    public async Task NotifyMemberRemoved_AnOwnerRemovesAMember_WritesAPersonalOneAboutNoTenantAndSendsTheRequiredEmail()
    {
        var tenant = await _testData.AddTenantAsync();
        var owner = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var email = PlatformTenants.Email();
        var member = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));
        var (ownerId, memberId) = (await _testData.UserIdOfAsync(owner), await _testData.UserIdOfAsync(member));

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(owner).RemoveMemberAsync(tenant.Id, memberId));

        (await _rows.OfAsync(ownerId, memberId)).ShouldBe([NotificationRows.Row(memberId, "member.removed", null, "Personal")]);
        _api.Email.Sent.Where(sent => sent.To == email).Select(sent => sent.Subject).ShouldBe(["You were removed from Acme Ltd"]);
    }

    [Fact]
    public async Task NotifyInvitationAccepted_APersonAcceptsAMembersInvitation_WritesOneForTheInviterAndNoneForThePerson()
    {
        var tenant = await _testData.AddTenantAsync();
        var inviter = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var email = PlatformTenants.Email();
        var invited = await _api.WaitingForMessagesAsync(() => _api.CreateClient(inviter).InviteMemberAsync(tenant.Id, email));

        var (person, _) = await new PlatformTenants(database, _api).AcceptAsync(await PlatformTenants.InvitationIdOfAsync(invited), email, "Grace", "Hopper");

        var (inviterId, personId) = (await _testData.UserIdOfAsync(inviter), await _testData.UserIdOfAsync(person));
        (await _rows.OfAsync(inviterId, personId)).ShouldBe([NotificationRows.Row(inviterId, "invitation.accepted", tenant.Id, "Tenant")]);
        (await _rows.ValuesOfAsync(inviterId, "invitation.accepted")).ShouldBe("""{"memberName": "Grace Hopper", "tenantName": "Acme Ltd"}""");
    }

    // A role-based type: the Owners at that moment get it, the Admin does not, and a member who becomes an Owner later sees no older one.
    [Fact]
    public async Task NotifyTenantPlanChanged_ThePlatformChangesThePlan_WritesOneForEachOwnerThenAndNoneForALaterOwner()
    {
        var tenant = await _testData.AddTenantAsync();
        var (first, second) = (await _testData.AddMemberAsync(tenant.Id, role: "Owner"), await _testData.AddMemberAsync(tenant.Id, role: "Owner"));
        var later = await _testData.AddMemberAsync(tenant.Id, role: "Admin");
        var platform = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
        var plan = await _testData.AddPlanAsync([]);

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
    }

    [Fact]
    public async Task NotifyTenantOwnerJoined_TheFirstOwnerOfANewTenantJoins_WritesOneForEverySystemAdminAndNoneForTheOwner()
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
    }

    [Fact]
    public async Task NotifyTenantStatusChanged_ASystemAdminSuspendsATenant_WritesOneForEveryOtherSystemAdminAndNoneForTheActor()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var (actor, other) = (await _testData.AddSystemAdminAsync(), await _testData.AddSystemAdminAsync());

        await _api.WaitingForMessagesAsync(() => _api.CreateClient(actor, secondFactor: true).SuspendTenantAsync(tenant.Id));

        var (actorId, otherId, memberId) = (await _testData.UserIdOfAsync(actor), await _testData.UserIdOfAsync(other), await _testData.UserIdOfAsync(member));
        (await _rows.AboutAsync(tenant.Id, actorId, otherId, memberId)).ShouldBe([NotificationRows.Row(otherId, "tenant.status_changed", tenant.Id, "Platform")]);
        (await _rows.ValuesOfAsync(otherId, "tenant.status_changed")).ShouldBe("""{"status": "suspended", "tenantName": "Acme Ltd"}""");
    }

    [Fact]
    public async Task NotifyTenantPurged_ThePurgeFinishes_WritesOneForEverySystemAdmin()
    {
        var admin = await _testData.AddSystemAdminAsync();
        var purged = new TenantPurgedEvent(Guid.CreateVersion7(), DateTimeOffset.UtcNow, Guid.CreateVersion7(), "Acme Ltd");

        await PublishAsync(purged);

        var adminId = await _testData.UserIdOfAsync(admin);
        (await _rows.AboutAsync(purged.TenantId, adminId)).ShouldBe([NotificationRows.Row(adminId, "tenant.purged", purged.TenantId, "Platform")]);
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
