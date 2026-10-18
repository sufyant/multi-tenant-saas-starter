namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>tenant.owner_invited</c>: the invitation's id, and the pending invitation it replaced, if any.</summary>
public sealed record OwnerInvitedDetailsResponse(Guid InvitationId, Guid? ReplacedInvitationId) : AuditDetailsResponse;
