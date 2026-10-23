using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.invited</c>: the role the invitation gives, by its name at that moment, its id and whether it is built in,
/// and the invitation it replaced.
/// </summary>
public sealed record MemberInvitedDetails(string Role, Guid? ReplacedInvitationId, Guid RoleId, bool RoleBuiltIn) : AuditDetails;
