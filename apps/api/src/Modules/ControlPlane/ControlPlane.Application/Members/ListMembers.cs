using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Members;

/// <summary>The fields the member list sorts by. The first is the default: Owner, Admin, Member.</summary>
public enum ListMembersSort
{
    Role,
    Email,
    LastName,
}

/// <summary>
/// The members of the active tenant, a page at a time, sorted by one field, then by user id. The search matches the email address,
/// the first name or the last name. The role is the name of a built-in role (<c>Owner</c>), or null for every role.
/// </summary>
public sealed record ListMembers(ListRequest<ListMembersSort> List, string? Role);

/// <summary>
/// A member: the catalog user, the catalog's copy of their profile (A7), the name of the built-in role they hold in the tenant (<c>owner</c>), and whether the
/// identity provider deleted the user. The profile is empty when it did (A8).
/// </summary>
public sealed record MemberSummary(Guid UserId, string? Email, string? FirstName, string? LastName, string Role, bool Deleted);

public static class ListMembersHandler
{
    public static async Task<Result<ListPage<MemberSummary>>> HandleAsync(
        ListMembers query,
        ITenantCatalog catalog,
        CancellationToken cancellationToken) =>
        (await catalog.ListMembersAsync(query.List, query.Role is { } role ? Enum.Parse<BuiltInRole>(role) : null, cancellationToken))
            .Map(member => new MemberSummary(member.UserId, member.Email, member.FirstName, member.LastName, Role.NameOf(member.Role), member.Deleted));
}
