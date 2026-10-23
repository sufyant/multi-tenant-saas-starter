using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.role_changed</c>: the old and the new role, each by its name at that moment, its id and whether it is built
/// in.
/// </summary>
public sealed record MemberRoleChangedDetails(
    string OldRole,
    string NewRole,
    Guid OldRoleId,
    bool OldRoleBuiltIn,
    Guid NewRoleId,
    bool NewRoleBuiltIn) : AuditDetails;
