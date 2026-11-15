using Billing.Application.Checkouts;
using Billing.Application.Checkouts.ExpireCheckoutPages;
using Billing.Application.Customers;
using Billing.Application.Subscriptions.CancelSubscription;
using Billing.Application.Subscriptions.CountSeats;
using Billing.Application.Subscriptions.EndGrace;
using Billing.Application.Subscriptions.NamePaymentFailure;
using Billing.Application.Subscriptions.NameScheduleChange;
using Billing.Application.Subscriptions.SetCancelAtPeriodEnd;
using Billing.Contracts.Subscriptions;
using Billing.Domain.Subscriptions;
using SharedKernel;
using Wolverine;

namespace Billing.Application.Subscriptions.ApplySubscription;

// The tenant's customer, the root of its billing, is locked first, then its subscription (K6a, B7), so two syncs of one tenant run one after
// the other. A state the row has already changes nothing and asks nothing of ControlPlane (I1). Each message goes through the outbox in the
// same transaction, and keeps the tenant (W3).
public static class ApplySubscriptionHandler
{
    public static async Task<OutgoingMessages> HandleAsync(
        ApplySubscriptionCommand command,
        ICustomerStore customers,
        ISubscriptionRepository subscriptions,
        ICheckoutRepository checkouts,
        BillingOptions billing,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var state = new SubscriptionState(
            command.CustomerId, command.SubscriptionId, command.PlanId, Enum.Parse<BillingInterval>(command.Interval),
            Enum.Parse<SubscriptionStatus>(command.Status), command.CurrentPeriodEnd, command.CancelAtPeriodEnd,
            command.ScheduledPlanId, command.ScheduledInterval is { } scheduled ? Enum.Parse<BillingInterval>(scheduled) : null,
            command.Payment is { } payment
                ? new FailedPayment(
                    payment.InvoiceId, payment.AmountDue, payment.Currency, payment.FailedAt, payment.Attempts, payment.NextAttemptAt, payment.CardBrand,
                    payment.CardLast4)
                : null);
        var now = time.GetUtcNow();
        // A purge that deleted the customer since the route leaves nothing to apply (B5).
        if (await customers.FindForUpdateAsync(cancellationToken) is not { } customer)
        {
            return [];
        }

        var subscription = await subscriptions.FindForUpdateAsync(cancellationToken);

        // B7: a second subscription while one has not ended came from a page that a race left payable. It ends at once, and support refunds
        // its payment; the tenant keeps the first.
        if (subscription is not null && subscription.SubscriptionId != state.SubscriptionId
            && !Subscription.HasEnded(subscription.Status) && !Subscription.HasEnded(state.Status))
        {
            return [new CancelSubscriptionCommand(state.SubscriptionId)];
        }

        // Step 40, billing per person: each state of a subscription that has not ended brings the provider's count of seats, which is set
        // again when it differs from the members who take a seat, a state the row has too, as a count is no part of the row.
        OutgoingMessages messages = command.People is { } people && !Subscription.HasEnded(state.Status)
            ? [new CountSeatsCommand(state.SubscriptionId, people)]
            : [];
        if (subscription is null)
        {
            subscription = Subscription.Start(state, now);
            subscriptions.Add(subscription);
        }
        else
        {
            // B9, 12.12: a member's cancellation, a scheduled move and their undo are entries of the Activity log, by the member who changed
            // the plan last, once the provider's state shows them. Read before the row takes the state, which it compares with, as the
            // dunning's steps are (B11).
            var scheduleChange = subscription.ScheduleChangeOf(state, customer.ClosedAt is not null);
            var dunning = subscription.DunningOf(state);
            if (subscription.Sync(state, now) == StateChange.Unchanged)
            {
                return messages;
            }

            // B11, 12.12: a payment that failed for the first time on an invoice is an entry of the Activity log; the provider retries it.
            if (dunning.PaymentFailed && subscription.FailedPayment is { } failed)
            {
                messages.Add(new NamePaymentFailureCommand(
                    Guid.CreateVersion7(now), now, command.TenantId, failed.AmountDue, failed.Currency, failed.FailedAt + billing.RetryWindow));
            }

            // B11: a subscription that ended leaves its plan for the default plan, by the member who changed it last, or unpaid.
            if (dunning.End is { } end)
            {
                messages.Add(new PlanEndedEvent(Guid.CreateVersion7(now), now, command.TenantId, command.PaidBy, Unpaid: end == PlanEnd.Unpaid));
            }

            if (scheduleChange is { } change)
            {
                var moves = change is ScheduleChange.Cancelled or ScheduleChange.MoveScheduled;
                messages.Add(new NameScheduleChangeCommand(
                    Guid.CreateVersion7(now), now, command.TenantId, change.ToString(), command.PaidBy, subscription.PlanId,
                    change == ScheduleChange.MoveScheduled ? subscription.ScheduledPlanId : null, moves ? subscription.CurrentPeriodEnd : null));
            }
        }

        // B8: the provider ends in what the closure or the reopen asked, whatever order our calls reached it in, and a subscription of a
        // closed tenant pays nothing after its period, however late its state arrives.
        if (subscription.ClosureCorrection(customer.ClosedAt is not null) is { } cancelAtPeriodEnd)
        {
            messages.Add(new SetCancelAtPeriodEndCommand(subscription.SubscriptionId, cancelAtPeriodEnd));
        }

        // B11: once the provider's retries are over, billing's grace runs, and its end moves the tenant to the default plan.
        if (subscription.StartGrace(now, billing.Grace) is { } graceEndsAt)
        {
            messages.Add(new EndGraceCommand(command.TenantId, graceEndsAt).ScheduledAt(graceEndsAt));
        }

        // B7: a subscription that pays ends the tenant's open checkout, whose pages are expired, so nobody pays twice.
        if (subscription.PaysForItsPlan)
        {
            messages.Add(new PlanPaidEvent(Guid.CreateVersion7(now), now, command.TenantId, subscription.PlanId, command.PaidBy));
            if (await checkouts.FindForUpdateAsync(cancellationToken) is { } checkout)
            {
                checkouts.Remove(checkout);
                messages.Add(new ExpireCheckoutPagesCommand(customer.CustomerId, checkout.Key));
            }
        }

        await subscriptions.SaveChangesAsync(cancellationToken);
        return messages;
    }
}
