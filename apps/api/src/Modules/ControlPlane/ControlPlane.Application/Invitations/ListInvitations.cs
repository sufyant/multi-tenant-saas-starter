using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Invitations;

/// <summary>The fields the invitation list sorts by. The first is the default: the one that expires first comes first.</summary>
public enum ListInvitationsSort
{
    ExpiresAt,
    Email,
}

/// <summary>
/// The pending invitations of the active tenant, expired ones included, a page at a time, sorted by one field, then by id. The search
/// matches the email address. The role is the name of a built-in role (<c>Owner</c>), or null for every role.
/// </summary>
public sealed record ListInvitationsQuery(ListRequest<ListInvitationsSort> List, string? Role);

public static class ListInvitationsHandler
{
    public static async Task<Result<ListPage<InvitationView>>> HandleAsync(
        ListInvitationsQuery query,
        ITenantCatalog catalog,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        return (await catalog.ListPendingInvitationsAsync(query.List, query.Role is { } role ? Enum.Parse<BuiltInRole>(role) : null, cancellationToken))
            .Map(found => InvitationView.Of(found.Invitation, found.Inviter, now));
    }
}
