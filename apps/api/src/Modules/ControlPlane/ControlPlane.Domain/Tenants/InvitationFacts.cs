namespace ControlPlane.Domain.Tenants;

/// <summary>
/// What <see cref="Tenant.Invite"/> needs to know of the tenant's rows, read under the tenant's lock (K6i, K6j): how many Owners it has,
/// whether a member has the invited address, how many invitations it sent in the limit's window, and the pending invitations a new one
/// to the address may replace: those to the address and, while the tenant has no Owner, every one.
/// </summary>
internal sealed record InvitationFacts(int Owners, bool AddressIsMember, int SentInWindow, IReadOnlyList<Invitation> Pending);
