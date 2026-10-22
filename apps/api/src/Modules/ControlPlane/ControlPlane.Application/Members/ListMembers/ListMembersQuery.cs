using SharedKernel;

namespace ControlPlane.Application.Members.ListMembers;

/// <summary>
/// The members of the active tenant, a page at a time, sorted by one field, then by user id. The search matches the email address,
/// the first name or the last name. The role is the id of one of the tenant's roles, or null for every role.
/// </summary>
[MessageName("list-members")]
public sealed record ListMembersQuery(ListRequest<ListMembersSort> List, Guid? RoleId);
