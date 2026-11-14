namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// What the action was done to: a tenant or a role with its name at that moment, a user with their real name now or null, or an
/// invitation, which has no name. <c>deleted</c> is set for a user only. <c>email</c> is an invitation's address now, read from the
/// invitation; null for any other target, for an invitation whose address a user's deletion erased (K3, A8), and in the tenant's log
/// for a caller without <c>tenant.invitations.read</c>.
/// </summary>
public sealed record AuditTargetResponse(AuditTargetResponseKind Kind, Guid Id, string? Name, bool? Deleted, string? Email);
