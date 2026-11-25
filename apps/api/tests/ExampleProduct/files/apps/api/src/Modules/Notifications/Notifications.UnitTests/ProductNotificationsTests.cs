using Notifications.Domain;

namespace Notifications.UnitTests;

// The example product "Projects" fills the product's notification types (X7). These tests run only in the job "Example product: build and
// test", which copies tests/ExampleProduct/files over the template (X4). NotificationCatalogTests checks the type's texts, its area and its
// name there too.
public sealed class ProductNotificationsTests
{
    [Fact]
    public void DeclareNotificationTypes_AProductsType_IsInTheCatalog() =>
        NotificationCatalog.Of(ProductNotifications.ProjectCreated).Area.ShouldBe("projects");

    [Fact]
    public void SendNotification_AProductsType_FindsItsEntry() =>
        NotificationCatalog.OfProduct(ProductNotifications.ProjectCreated).Actor.ShouldBe("actorName");
}
