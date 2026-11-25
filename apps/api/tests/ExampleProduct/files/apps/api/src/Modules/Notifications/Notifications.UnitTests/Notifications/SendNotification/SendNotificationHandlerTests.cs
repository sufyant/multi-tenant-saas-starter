using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications.DeliverNotification;
using Notifications.Application.Notifications.SendNotification;
using Notifications.Contracts.Notifications;
using Notifications.Domain;
using SharedKernel;
using Wolverine;

namespace Notifications.UnitTests.Notifications.SendNotification;

// X7: a product module tells of its event with SendNotificationCommand. The handler reads the tenant's name of that moment and delivers to
// each person the module named but the actor; a purged tenant tells nobody. These tests run only in the job "Example product: build and
// test", because the template has no product type (X4).
public sealed class SendNotificationHandlerTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();

    private static readonly Guid Actor = Guid.CreateVersion7();

    private static readonly Guid Reader = Guid.CreateVersion7();

    [Fact]
    public async Task SendNotification_AProductsType_DeliversToEveryoneButTheActorWithTheTenantsName()
    {
        var messages = await SendAsync(new FakeTenantNames("Acme"));

        messages.OfType<DeliveryMessage<DeliverNotificationCommand>>()
            .Select(message => $"{message.Message.UserId} {message.Message.TenantId} {message.Message.Values["tenantName"]} {message.Message.Values["projectName"]}")
            .ShouldBe([$"{Reader} {Tenant} Acme Apollo"]);
    }

    [Fact]
    public async Task SendNotification_APurgedTenant_TellsNobody()
    {
        var messages = await SendAsync(new FakeTenantNames(null));

        messages.ShouldBeEmpty();
    }

    private static Task<OutgoingMessages> SendAsync(ITenantNames tenants) => SendNotificationHandler.HandleAsync(
        new SendNotificationCommand(
            ProductNotifications.ProjectCreated,
            Guid.CreateVersion7(),
            DateTimeOffset.UnixEpoch,
            Tenant,
            new Dictionary<string, string> { ["projectName"] = "Apollo", ["actorName"] = Actor.ToString() },
            [Actor, Reader],
            Actor),
        tenants,
        new FakeUserDirectory(),
        TestContext.Current.CancellationToken);

    private sealed class FakeTenantNames(string? name) : ITenantNames
    {
        public Task<string?> FindNameAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(name);
    }

    private sealed class FakeUserDirectory : IUserDirectory
    {
        public Task<Result> CheckActiveAsync(string externalUserId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<Guid, UserName>> FindNamesAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, UserName>>(userIds.ToDictionary(id => id, _ => new UserName("Ada", Deleted: false)));

        public Task<IReadOnlyList<UserContact>> FindContactsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UserContact>>([.. userIds.Select(id => new UserContact(id, $"{id}@example.com", "Ada", Language.En, Active: true))]);

        public Task<UserContact?> FindContactAsync(string externalUserId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<Guid>> ListSystemAdminIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
