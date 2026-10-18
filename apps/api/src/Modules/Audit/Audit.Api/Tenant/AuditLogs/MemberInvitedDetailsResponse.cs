using ControlPlane.Contracts.Members;

namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>member.invited</c>: the role the invitation gives, and the pending invitation it replaced, if any.</summary>
public sealed record MemberInvitedDetailsResponse(RoleResponse Role, Guid? ReplacedInvitationId) : AuditDetailsResponse;
