using SharedKernel;

namespace ControlPlane.Application.Invitations.ListInvitations;

/// <summary>
/// The pending invitations of the active tenant, expired ones included, a page at a time, sorted by one field, then by id. The search
/// matches the email address. The role is the id of one of the tenant's roles, or null for every role.
/// </summary>
[MessageName("list-invitations")]
public sealed record ListInvitationsQuery(ListRequest<ListInvitationsSort> List, Guid? RoleId);
