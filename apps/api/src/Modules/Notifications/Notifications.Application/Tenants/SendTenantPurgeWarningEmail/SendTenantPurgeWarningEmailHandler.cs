using ControlPlane.Contracts.Tenants;
using Notifications.Application.Email;

namespace Notifications.Application.Tenants.SendTenantPurgeWarningEmail;

// The email that warns an Owner of a closed tenant's purge, in the Owner's language and time zone (docs/flows/tenant-purge.md). The event has this one
// handler, so its retries may be scheduled (W5); it takes the invitation email's retries. The handler takes no DbContext, so Wolverine
// opens no transaction around the call to the email service. Once its retries are spent the event goes to the dead letter queue: the
// purge still comes at its moment.
public static class SendTenantPurgeWarningEmailHandler
{
    public static Task HandleAsync(TenantPurgeWarningReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken) =>
        channel.SendAsync(
            TenantPurgeWarningEmailTexts.Write(
                ready.Email,
                ready.TenantName,
                ready.Language,
                ready.PurgesAt,
                ready.TimeZone,
                ready.PurgesAt - ready.OccurredAt,
                ready.TenantId,
                ready.UserId),
            cancellationToken);
}
