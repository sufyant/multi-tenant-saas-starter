namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>member.left</c>: the role the member had.</summary>
public sealed record MemberLeftDetailsResponse(AuditRoleResponse Role) : AuditDetailsResponse;
