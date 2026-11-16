using SharedKernel;

namespace Billing.Application.PriceChanges.MoveToNewPrice;

/// <summary>"Move to the new price" (6.16): the plan's payers on another price move to its latest change's prices after 30 days' notice (B13).</summary>
/// <param name="ActorId">Our user id of the system admin.</param>
[MessageName("move-to-new-price")]
public sealed record MoveToNewPriceCommand(Guid ActorId, Guid PlanId);
