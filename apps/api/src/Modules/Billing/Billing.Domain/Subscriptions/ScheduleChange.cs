namespace Billing.Domain.Subscriptions;

/// <summary>
/// A member's change of what happens to the subscription at the end of its paid period (B9), as the provider's state shows it: a
/// cancellation, its undo, a move to another plan or interval, or the undo of that move. Each is an entry of the Activity log (12.12).
/// </summary>
internal enum ScheduleChange
{
    Cancelled,
    CancellationUndone,
    MoveScheduled,
    MoveUndone,
}
