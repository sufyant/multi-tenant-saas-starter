using SharedKernel;

namespace Billing.Application.Subscriptions.BeginPlanChange;

/// <summary>
/// The tenant's side of a member's plan change, its cancellation or their undo, before the call to the provider (B9), in the tenant of the
/// route: the tenant is open and its subscription active, the member is named as the one who changes it, and a member's own end, or its
/// undo, is no closure's (B8). Answers the subscription as billing holds it.
/// </summary>
/// <param name="ActorId">The user id of the member.</param>
/// <param name="ByMemberEnd">True for a cancellation and for an undo: the end at the period's end is then the member's, not the closure's.</param>
/// <param name="Undo">True for the undo, which needs a scheduled move or a cancellation (409 <c>billing.nothing_scheduled</c>).</param>
[MessageName("begin-plan-change")]
public sealed record BeginPlanChangeCommand(Guid ActorId, bool ByMemberEnd, bool Undo = false);
