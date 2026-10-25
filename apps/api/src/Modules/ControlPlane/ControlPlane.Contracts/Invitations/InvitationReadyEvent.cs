using SharedKernel;

namespace ControlPlane.Contracts.Invitations;

/// <summary>
/// The invited person can be told: the identity provider lets the address sign up, and the link is ready to be sent. Published through
/// the outbox, in the invitation's tenant; only Notifications handles it. A tenant's first Owner, an Owner the platform invites and a
/// person a member invites get the same event. It carries what the email shows, as values of that moment: nothing more is stored.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Link">
/// Where the person accepts: the identity provider's sign-up link that leads to the accept link, or the accept link itself for an address
/// that has an account. It carries the invitation's secret, which cannot be produced again later; see the deviations list in
/// ARCHITECTURE.md.
/// </param>
/// <param name="Language">The language of the email, which the invitation keeps.</param>
/// <param name="Lifetime">
/// How long the invitation can be accepted, from its creation. The email of the version before step r14 says it; this version's email
/// writes <paramref name="ExpiresAt"/>. It stays for a worker of that version during the deploy, and goes in a later release.
/// </param>
/// <param name="InviterName">
/// The inviter's first and last name, copied at this moment for the email only; null when the platform invites, because a system admin
/// invites as the platform, and the email names the brand.
/// </param>
/// <param name="RoleName">The name of the role the invitation gives, as the role keeps it: a built-in role's stored name, or the name the Owner typed.</param>
/// <param name="RoleBuiltIn">Whether the role is built in, so the email writes its name in the email's language.</param>
/// <param name="ExpiresAt">When the link stops working.</param>
/// <param name="TimeZone">The IANA name of the inviter's time zone, in which the email writes the expiry; null when it is not set.</param>
[MessageName("invitation-ready")]
public sealed record InvitationReadyEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string TenantName,
    Uri Link,
    Language Language,
    TimeSpan Lifetime,
    string? InviterName,
    string RoleName,
    bool RoleBuiltIn,
    DateTimeOffset ExpiresAt,
    string? TimeZone);
