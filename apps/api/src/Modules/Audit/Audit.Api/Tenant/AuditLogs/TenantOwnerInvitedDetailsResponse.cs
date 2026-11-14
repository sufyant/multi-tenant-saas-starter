namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// The details of <c>tenant.owner_invited</c>: the invitation's id, the pending invitation it replaced, if any, and the invitation's address
/// now, read from the invitation; null when a user's deletion erased it (K3, A8), and in the tenant's log for a caller without
/// <c>tenant.invitations.read</c>.
/// </summary>
public sealed record TenantOwnerInvitedDetailsResponse(Guid InvitationId, Guid? ReplacedInvitationId, string? Email) : AuditDetailsResponse;
