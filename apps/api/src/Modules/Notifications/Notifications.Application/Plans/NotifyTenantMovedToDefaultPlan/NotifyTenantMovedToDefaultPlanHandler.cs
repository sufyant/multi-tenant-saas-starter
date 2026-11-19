using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.Plans.NotifyTenantMovedToDefaultPlan;

// "The organisation moved to the default plan" after an unpaid grace (13.20, 3.18), with the open invoice that pays the plan back, to the
// members who manage its billing at that moment, read in the tenant's transaction. Only the move of an unpaid subscription (Unpaid, B11) is
// this type; the audit log records the same event as tenant.moved_to_default_plan. Any other plan change is tenant.plan_changed's.
public static class NotifyTenantMovedToDefaultPlanHandler
{
    public const string Type = NotificationCatalog.TenantMovedToDefaultPlan;

    public static async Task<OutgoingMessages> HandleAsync(TenantPlanChangedEvent changed, IMemberDirectory members, IUserDirectory users, CancellationToken cancellationToken)
    {
        if (!changed.Unpaid)
        {
            return [];
        }

        var values = new Dictionary<string, string>
        {
            ["tenantName"] = changed.TenantName,
            ["planName"] = changed.OldPlanName,
            ["defaultPlanName"] = changed.NewPlanName,
            ["movedAt"] = BillingValues.Moment(changed.OccurredAt),
        };
        if (changed is { UnpaidAmountDue: { } amountDue, UnpaidCurrency: { } currency })
        {
            values["amountDue"] = NotificationText.MoneyValue(amountDue, currency);
        }

        return await NotificationFanOut.ToAsync(
            Type,
            changed.EventId,
            changed.OccurredAt,
            changed.TenantId,
            values,
            await users.FindContactsAsync(await members.ListBillingManagerIdsAsync(cancellationToken), cancellationToken),
            actorId: null,
            users,
            cancellationToken);
    }
}
