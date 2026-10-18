using SharedKernel;

namespace ControlPlane.Contracts.Users;

/// <summary>
/// A user changed their own language or time zone. Published through the outbox, without a tenant. It carries no value: the
/// audit entry says that the preferences changed, not to what.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
[MessageName("user-preferences-changed")]
public sealed record UserPreferencesChangedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
