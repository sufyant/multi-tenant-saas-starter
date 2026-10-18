using SharedKernel;

namespace ControlPlane.Application.Invitations.ListInvitations;

/// <summary>
/// The pending invitations of the active tenant, expired ones included, a page at a time, sorted by one field, then by id. The search
/// matches the email address. The role is the name of a built-in role (<c>Owner</c>), or null for every role.
/// </summary>
public sealed record ListInvitationsQuery(ListRequest<ListInvitationsSort> List, string? Role);
