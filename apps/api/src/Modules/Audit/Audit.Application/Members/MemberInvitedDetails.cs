using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.invited</c>: the role the invitation gives, by its name at that moment, its id and whether it is built in,
/// and the invitation it replaced. An entry written before step r4b keeps the name only: its id and its built-in flag are null, and every role was built in then.
/// </summary>
public sealed record MemberInvitedDetails(string Role, Guid? ReplacedInvitationId, Guid? RoleId, bool? RoleBuiltIn) : AuditDetails;
