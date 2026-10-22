namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.Invite"/> did: the new invitation, which is not saved yet, and the pending invitations it replaced, which it
/// cancelled. The replaced ones are saved first, so the partial unique index never sees two pending invitations (K2).
/// </summary>
internal sealed record Invited(Invitation Invitation, IReadOnlyList<Invitation> Replaced);
