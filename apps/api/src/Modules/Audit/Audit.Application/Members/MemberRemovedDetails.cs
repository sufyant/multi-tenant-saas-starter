using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.removed</c>: the role the member had, by its name at that moment, its id and whether it is built in. A
/// member's removal and a user's deletion write it, because both remove a member. An entry written before step r4b keeps the name only: its id and its built-in flag are null, and every role was built in then.
/// </summary>
public sealed record MemberRemovedDetails(string Role, Guid? RoleId, bool? RoleBuiltIn) : AuditDetails;
