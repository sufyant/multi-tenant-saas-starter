using SharedKernel;

namespace ControlPlane.Application.Users.ListUsers;

/// <summary>
/// Every user, a page at a time, sorted by one field, users without a value for it last, then by id. The search matches the
/// email address or the name. The statuses are values of <see cref="UserView.Status"/> (<c>Active</c>, <c>Suspended</c>,
/// <c>Deleted</c>): a user in any of them is listed, and none lists every status. <see cref="IsSystemAdmin"/> keeps the users on the system admin list, or
/// the users not on it, or, null, every user. <see cref="Email"/> keeps the users whose address is this one, in its one form, or, null,
/// every user; the validator checks it. Addresses are not unique, so it may keep more than one. It runs without a tenant.
/// </summary>
[MessageName("list-users")]
public sealed record ListUsersQuery(ListRequest<ListUsersSort> List, IReadOnlyList<string> Statuses, bool? IsSystemAdmin, string? Email);
