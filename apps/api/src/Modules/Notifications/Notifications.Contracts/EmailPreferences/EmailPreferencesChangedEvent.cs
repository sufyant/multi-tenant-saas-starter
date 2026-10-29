using SharedKernel;

namespace Notifications.Contracts.EmailPreferences;

/// <summary>
/// A user changed their own email preferences for notifications. Published through the outbox, without a tenant. It carries no value: the
/// audit entry says that the preferences changed, not to what (K3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="UserId">The user id of the user, who acted for themselves.</param>
[MessageName("email-preferences-changed")]
public sealed record EmailPreferencesChangedEvent(Guid EventId, DateTimeOffset OccurredAt, Guid UserId);
