using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A person who signed up at the identity provider became a user, while the product has sign-up on (step 40d). Published through the outbox,
/// without a tenant.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the new user, who acted.</param>
[MessageName("user-signed-up")]
public sealed record UserSignedUpEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
