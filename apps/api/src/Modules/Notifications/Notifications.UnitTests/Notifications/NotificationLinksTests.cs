using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;

namespace Notifications.UnitTests.Notifications;

// U1: an override leads to the page of its key, and any other key to the tenant's general settings; another type to its own link kind.
public sealed class NotificationLinksTests
{
    [Theory]
    [InlineData("members.max", NotificationLinkKind.TenantMembers)]
    [InlineData("roles.custom", NotificationLinkKind.TenantRoles)]
    [InlineData("roles.custom.max", NotificationLinkKind.TenantRoles)]
    [InlineData("storage.max", NotificationLinkKind.TenantSettings)]
    public void Of_AnOverride_LeadsToThePageOfItsKey(string key, NotificationLinkKind link)
    {
        var values = new Dictionary<string, string> { ["keyName"] = key };

        NotificationLinks.Of(NotificationCatalog.Of(NotificationCatalog.TenantEntitlementOverrideSet), values).ShouldBe(link);
    }

    // The boards' Link rows: a role change and a plan change lead to Settings › General, a removal to the start page.
    [Theory]
    [InlineData(NotificationCatalog.MemberRoleChanged, NotificationLinkKind.TenantSettings)]
    [InlineData(NotificationCatalog.MemberRemoved, NotificationLinkKind.Start)]
    [InlineData(NotificationCatalog.MemberJoined, NotificationLinkKind.TenantMembers)]
    [InlineData(NotificationCatalog.TenantPlanChanged, NotificationLinkKind.TenantSettings)]
    [InlineData(NotificationCatalog.TenantSuspended, NotificationLinkKind.PlatformTenant)]
    public void Of_ATypeWithoutAKey_LeadsToItsOwnLinkKind(string type, NotificationLinkKind link)
    {
        NotificationLinks.Of(NotificationCatalog.Of(type), new Dictionary<string, string>()).ShouldBe(link);
    }
}
