using System.Globalization;
using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.Options;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.TermsVersions.NotifyTermsVersionCreated;

// "Our terms of use change", to every user who may accept, by email, when a version that requires no acceptance is created. A version
// that requires acceptance tells nobody here: the gate after sign-in asks for it. The system admin who created it is told nothing of their
// own action (U3). The deliveries go at the pace of the setting, because the fan-out reaches every user at once.
public static class NotifyTermsVersionCreatedHandler
{
    public const string Type = NotificationCatalog.TermsVersionCreated;

    public static async Task<OutgoingMessages> HandleAsync(
        TermsVersionCreatedEvent created,
        ITermsVersionDirectory terms,
        IUserDirectory users,
        IOptions<NotificationPaceOptions> pace,
        CancellationToken cancellationToken)
    {
        if (created.RequiresAcceptance)
        {
            return [];
        }

        var values = new Dictionary<string, string>
        {
            ["effectiveDate"] = created.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["url"] = created.Url,
        };
        if (created.Summary is { } summary)
        {
            values["summary"] = summary;
        }

        var recipients = await users.FindContactsAsync(await terms.ListActiveUserIdsAsync(cancellationToken), cancellationToken);
        return await NotificationFanOut.ToAsync(
            Type,
            created.EventId,
            created.OccurredAt,
            tenantId: null,
            values,
            recipients,
            created.CreatedBy,
            users,
            cancellationToken,
            pace.Value.DeliveriesPerSecond);
    }
}
