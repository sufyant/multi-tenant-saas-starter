using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

/// <summary>
/// Every user, a page at a time, sorted by one field, users without a value for it last, then by id. The search matches the
/// email address, the first name or the last name. The status is one of <see cref="UserView.Status"/> (<c>Active</c>,
/// <c>Suspended</c>, <c>Deleted</c>), or null for every status. It runs without a tenant.
/// </summary>
[MessageName("list-users")]
public sealed record ListUsersQuery(ListRequest<ListUsersSort> List, string? Status);
