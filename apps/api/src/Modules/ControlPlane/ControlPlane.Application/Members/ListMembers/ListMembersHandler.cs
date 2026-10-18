using ControlPlane.Application.Ports;
using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Members.ListMembers;

public static class ListMembersHandler
{
    public static async Task<Result<ListPage<MemberView>>> HandleAsync(
        ListMembersQuery query,
        ITenantCatalog catalog,
        CancellationToken cancellationToken) =>
        (await catalog.ListMembersAsync(query.List, query.Role is { } role ? Enum.Parse<BuiltInRole>(role) : null, cancellationToken))
            .Map(member => new MemberView(member.UserId, member.Email, member.FirstName, member.LastName, RoleView.Of(member.Role), member.Deleted, member.JoinedAt));
}
