using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>A system admin reactivated a suspended catalog user. Published through the outbox, without a tenant.</summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The catalog user id of the reactivated user.</param>
/// <param name="ReactivatedBy">The catalog user id of the system admin who reactivated the user.</param>
[MessageName("user-reactivated")]
public sealed record UserReactivatedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, Guid ReactivatedBy);
