using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>
/// The details of <c>member.removed</c>: the stored name of the role the member had. A member's removal and a user's deletion write
/// it, because both remove a member.
/// </summary>
public sealed record MemberRemovedDetails(string Role) : AuditDetails;
