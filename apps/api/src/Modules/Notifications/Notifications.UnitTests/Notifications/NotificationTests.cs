using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.UnitTests.Notifications;

// U1, U3: a notification is of a type of the catalog, with exactly the values its texts use, in the bell of its type.
public sealed class NotificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_ATypeOfTheCatalogWithItsValues_TakesTheBellOfItsType()
    {
        var notification = Notification.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationCatalog.MemberRoleChanged, RoleValues(), Now);

        (notification.Bell, notification.ReadAt).ShouldBe((NotificationBell.Tenant, null));
    }

    [Fact]
    public void Create_AValueMissing_IsAMistakeOfOurs()
    {
        var create = () => Notification.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationCatalog.MemberRoleChanged,
            new Dictionary<string, string> { ["tenantName"] = "Acme" }, Now);

        create.ShouldThrow<InvalidOperationException>().Message.ShouldContain("newRoleName, newRoleNameBuiltIn, oldRoleName, oldRoleNameBuiltIn, tenantName");
    }

    [Fact]
    public void Create_ATypeTheCatalogDoesNotKnow_IsAMistakeOfOurs()
    {
        var create = () => Notification.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), null, "tenant.renamed", new Dictionary<string, string>(), Now);

        create.ShouldThrow<InvalidOperationException>().Message.ShouldContain("tenant.renamed");
    }

    // An optional value may be missing: a role change by the platform names no actor.
    [Fact]
    public void Create_AnOptionalValueMissing_IsAccepted()
    {
        var values = RoleValues();
        values.Remove("actorName");

        var notification = Notification.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationCatalog.MemberRoleChanged, values, Now);

        notification.Values.Keys.ShouldNotContain("actorName");
    }

    [Theory]
    [InlineData(NotificationCatalog.TenantPlanChanged, false)]
    [InlineData(NotificationCatalog.MemberRemoved, true)]
    public void Create_TheWrongTenantForItsBell_IsAMistakeOfOurs(string type, bool aboutATenant)
    {
        Guid?[] tenants = [null, Guid.CreateVersion7()];

        var create = () => Notification.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), tenants[Convert.ToInt32(aboutATenant)], type, NotificationTextTests.SampleValues(type), Now);

        create.ShouldThrow<InvalidOperationException>().Message.ShouldContain("wrong tenant");
    }

    [Fact]
    public void MarkRead_Twice_ChangesOnceAndKeepsTheFirstMoment()
    {
        var notification = Notification.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationCatalog.MemberRoleChanged, RoleValues(), Now);

        var first = notification.MarkRead(Now.AddMinutes(1));
        var second = notification.MarkRead(Now.AddMinutes(2));

        (first, second, notification.ReadAt).ShouldBe((StateChange.Changed, StateChange.Unchanged, Now.AddMinutes(1)));
    }

    private static Dictionary<string, string> RoleValues() => new()
    {
        ["tenantName"] = "Acme",
        ["actorName"] = Guid.CreateVersion7().ToString(),
        ["oldRoleName"] = "member",
        ["oldRoleNameBuiltIn"] = "true",
        ["newRoleName"] = "admin",
        ["newRoleNameBuiltIn"] = "true",
    };
}
