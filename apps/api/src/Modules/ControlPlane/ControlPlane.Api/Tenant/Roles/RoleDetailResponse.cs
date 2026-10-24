namespace ControlPlane.Api.Tenant.Roles;

/// <summary>
/// A role of the tenant as the role endpoints answer it: what <see cref="RoleResponse"/> holds, the names of the role's permissions, in
/// ordinal order, and how many members have the role and how many pending invitations give it, an expired one included. DeleteRole
/// refuses a role while either count is above zero (role.in_use).
/// </summary>
public sealed record RoleDetailResponse(Guid Id, string Name, bool BuiltIn, IReadOnlyList<string> Permissions, int MemberCount, int PendingInvitationCount);
