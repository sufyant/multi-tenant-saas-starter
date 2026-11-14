using ControlPlane.Contracts.Invitations;

namespace Audit.Application.AuditLogs.GetAuditInvitationEmails;

public static class GetAuditInvitationEmailsHandler
{
    public static Task<IReadOnlyDictionary<Guid, string>> HandleAsync(
        GetAuditInvitationEmailsQuery query, IInvitationDirectory invitations, CancellationToken cancellationToken) =>
        invitations.FindEmailsAsync(query.InvitationIds, cancellationToken);
}
