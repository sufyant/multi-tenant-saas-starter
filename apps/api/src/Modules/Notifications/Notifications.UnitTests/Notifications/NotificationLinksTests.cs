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

    [Fact]
    public void Of_ATypeWithoutAKey_LeadsToItsOwnLinkKind()
    {
        NotificationLinks.Of(NotificationCatalog.Of(NotificationCatalog.TenantPlanChanged), new Dictionary<string, string>()).ShouldBe(NotificationLinkKind.TenantPlan);
    }
}
