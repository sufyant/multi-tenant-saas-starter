using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Wolverine;

namespace Notifications.Application.Usage.SendUsageNotice;

// The tenant's name of this moment and the members who manage its billing at that moment, read through ControlPlane's contracts in the
// tenant's transaction (K3), then the fan-out to them and to the billing email (B14). A tenant that a purge deleted since tells nobody.
public static class SendUsageNoticeHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        SendUsageNoticeCommand notice,
        ITenantNames tenants,
        IMemberDirectory members,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        if (await tenants.FindNameAsync(notice.TenantId, cancellationToken) is not { } tenantName)
        {
            return [];
        }

        return await NotificationFanOut.ToAsync(
            notice.Type,
            notice.EventId,
            notice.OccurredAt,
            notice.TenantId,
            new Dictionary<string, string>(notice.Values) { ["tenantName"] = tenantName },
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken,
            address: await NotificationFanOut.AddressOfAsync(notice.BillingEmail, notice.BillingEmailSetBy, users, cancellationToken));
    }
}
