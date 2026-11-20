using System.Globalization;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Notifications.Application.Email;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Tenants.NotifyTenantCreated;

// The welcome (13.25, 3.19), to the person who created an organisation of their own, with the trial it started while billing is on. A
// system admin's creation tells nobody: the Owner hears of the organisation through the invitation. The person acted, but the welcome is
// for them, so no actor is left out. The product's name is the brand's, as it is at that moment.
public static class NotifyTenantCreatedHandler
{
    public const string Type = NotificationCatalog.TenantCreated;

    public static async Task<OutgoingMessages> HandleAsync(TenantCreatedEvent created, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (!created.ByItsOwner)
        {
            return [];
        }

        var values = new Dictionary<string, string> { ["tenantName"] = created.Name, ["productName"] = EmailTemplate.Brand.Name };
        if (created is { TrialEndsAt: { } endsAt, PlanName: { } planName })
        {
            values["planName"] = planName;
            values["trialDays"] = Math.Round((endsAt - created.OccurredAt).TotalDays).ToString(CultureInfo.InvariantCulture);
            values["endsAt"] = BillingValues.Moment(endsAt);
        }

        return await NotificationFanOut.ToAsync(
            Type,
            created.EventId,
            created.OccurredAt,
            created.TenantId,
            values,
            await users.FindContactsAsync([created.CreatedBy], cancellationToken),
            actorId: null,
            users,
            cancellationToken);
    }
}
