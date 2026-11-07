using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>The details of <c>member.left</c>: the role the member had, by its name at that moment, its id and whether it is built in.</summary>
public sealed record MemberLeftDetails(string Role, Guid RoleId, bool RoleBuiltIn) : AuditDetails;
