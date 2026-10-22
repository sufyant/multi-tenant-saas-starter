namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>member.removed</c>: the role the member had.</summary>
public sealed record MemberRemovedDetailsResponse(AuditRoleResponse Role) : AuditDetailsResponse;
