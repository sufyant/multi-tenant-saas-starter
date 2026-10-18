using Audit.Application.AuditLogs;

namespace Audit.Application.Members;

/// <summary>The details of <c>member.invited</c>: the stored name of the role the invitation gives, and the invitation it replaced.</summary>
public sealed record MemberInvitedDetails(string Role, Guid? ReplacedInvitationId) : AuditDetails;
