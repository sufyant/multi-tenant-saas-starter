using SharedKernel;

namespace Billing.Application.Subscriptions.UndoScheduledChange;

/// <summary>
/// Drops the move or the end that a member scheduled at the end of the period (B9), so the subscription goes on as it is. The provider's
/// event of the change then brings the state to the tenant's row.
/// </summary>
[MessageName("undo-scheduled-change")]
public sealed record UndoScheduledChangeCommand(string SubscriptionId);
