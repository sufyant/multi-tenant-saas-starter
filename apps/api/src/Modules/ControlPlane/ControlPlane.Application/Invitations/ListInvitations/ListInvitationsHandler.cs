using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Invitations.ListInvitations;

public static class ListInvitationsHandler
{
    public static async Task<Result<ListPage<InvitationView>>> HandleAsync(
        ListInvitationsQuery query,
        IInvitationQueries invitations,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return (await invitations.ListPendingInvitationsAsync(query.List, query.Role is { } role ? Enum.Parse<BuiltInRole>(role) : null, cancellationToken))
            .Map(found => InvitationView.Of(found.Invitation, found.Inviter, now));
    }
}
