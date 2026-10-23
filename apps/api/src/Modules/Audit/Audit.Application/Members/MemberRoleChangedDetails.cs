using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.role_changed</c>: the old and the new role, each by its name at that moment, its id and whether it is built
/// in. An entry written before step r4b keeps the name only: its id and its built-in flag are null, and every role was built in then.
/// </summary>
public sealed record MemberRoleChangedDetails(
    string OldRole,
    string NewRole,
    Guid? OldRoleId,
    bool? OldRoleBuiltIn,
    Guid? NewRoleId,
    bool? NewRoleBuiltIn) : AuditDetails;
