using ControlPlane.Contracts.Tenants;
using Notifications.Application.Email;

namespace Notifications.Application.Tenants.SendTenantPurgeWarningEmail;

// The email that warns an Owner of a closed tenant's purge, in the Owner's language and time zone (docs/flows/tenant-purge.md): the day
// and the moment of the purge, and the brand's support contact to reopen it before then. The event has this one handler, so its retries
// may be scheduled (W5); it takes the invitation email's retries. The handler takes no DbContext, so Wolverine opens no transaction around
// the call to the email service. Once its retries are spent the event goes to the dead letter queue: the purge still comes at its moment.
public static class SendTenantPurgeWarningEmailHandler
{
    public static Task HandleAsync(TenantPurgeWarningReadyEvent ready, IEmailChannel channel, CancellationToken cancellationToken)
    {
        var (purgesOn, purgesAt) = EmailLanguage.Of(ready.Language).Dates.Write(ready.PurgesAt, ready.TimeZone);

        // The key is the Owner's warning of this purge, so an event that arrives twice sends one email; a tenant closed again purges at
        // another moment, so its warning is a new email.
        var email = EmailTemplate.Of("deletion-warning", ready.Language).Write(
            ready.Email,
            $"purge-warning/{ready.TenantId}/{ready.UserId}/{ready.PurgesAt.UtcTicks}",
            [
                EmailValue.Text("tenantName", ready.TenantName),
                EmailValue.Text("purgesOn", purgesOn),
                EmailValue.Text("purgesAt", purgesAt),
                EmailValue.Contact("supportUrl", EmailTemplate.Brand.SupportUrl),
            ]);
        return channel.SendAsync(email, cancellationToken);
    }
}
