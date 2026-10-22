namespace Audit.Api.Tenant.AuditLogs;

/// <summary>The details of <c>member.role_changed</c>: the member's old and new role.</summary>
public sealed record MemberRoleChangedDetailsResponse(AuditRoleResponse OldRole, AuditRoleResponse NewRole) : AuditDetailsResponse;
