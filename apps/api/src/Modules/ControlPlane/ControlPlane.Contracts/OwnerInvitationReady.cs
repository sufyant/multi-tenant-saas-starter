namespace ControlPlane.Contracts;

/// <summary>
/// A new tenant's first owner can be invited: the identity provider lets the address sign up, and the link is ready to be sent to them.
/// Published through the outbox, in the new tenant, after the tenant's activation.
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="Link">
/// Where the owner accepts: the identity provider's sign-up link that leads to the accept link, or the accept link itself for an address
/// that has an account. It carries the invitation's secret, which cannot be produced again later; see the deviations list in
/// ARCHITECTURE.md.
/// </param>
public sealed record OwnerInvitationReady(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid InvitationId,
    string Email,
    string TenantName,
    Uri Link);
