namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>member.invited</c>: the role the invitation gives, and the pending invitation it replaced, if any.</summary>
public sealed record MemberInvitedDetailsResponse(AuditRoleResponse Role, Guid? ReplacedInvitationId) : AuditDetailsResponse;
