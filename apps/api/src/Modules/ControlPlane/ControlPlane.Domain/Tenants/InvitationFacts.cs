namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.Invite"/> needs to know of the tenant's rows, read under the tenant's lock (K6i, K6j): how many Owners it has
/// (members whose role holds <c>tenant.owners.manage</c>, A1),
/// whether a member has the invited address, how many invitations it sent in the limit's window, and every pending invitation, of which
/// the new one replaces those its inviter's rule names.
/// </summary>
internal sealed record InvitationFacts(int Owners, bool AddressIsMember, int SentInWindow, IReadOnlyList<Invitation> Pending);
