using SharedKernel;

namespace ControlPlane.Contracts;

/// <summary>
/// The person a member invited can be told: the identity provider lets the address sign up, and the link is ready to be sent. Published
/// through the outbox, in the invitation's tenant; only Notifications handles it.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Link">
/// Where the person accepts: the identity provider's sign-up link that leads to the accept link, or the accept link itself for an address
/// that has an account. It carries the invitation's secret, which cannot be produced again later; see the deviations list in
/// ARCHITECTURE.md.
/// </param>
/// <param name="Language">
/// The language of the email, which the invitation keeps. Null in a message written before step 20j, which is sent in English.
/// </param>
/// <param name="Lifetime">
/// How long the invitation can be accepted, from its creation, so the email says how long the link works. Null in a message written
/// before step 20j, whose email does not say it.
/// </param>
public sealed record MemberInvitationReady(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string TenantName,
    Uri Link,
    Language? Language,
    TimeSpan? Lifetime);
