using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>The details of <c>member.role_changed</c>: the stored names of the old and the new role.</summary>
public sealed record MemberRoleChangedDetails(string OldRole, string NewRole) : AuditDetails;
