using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditInvitationEmails;

/// <summary>
/// The addresses of the invitations a page of an audit log points to, read now by ControlPlane in the tenant of the message (K3). The
/// platform's log sends one for each tenant on its page.
/// </summary>
[MessageName("get-audit-invitation-emails")]
public sealed record GetAuditInvitationEmailsQuery(IReadOnlyCollection<Guid> InvitationIds);
