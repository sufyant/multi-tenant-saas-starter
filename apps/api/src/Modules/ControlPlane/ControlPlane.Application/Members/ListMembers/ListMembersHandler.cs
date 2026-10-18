using ControlPlane.Domain.Roles;
using SharedKernel;

namespace ControlPlane.Application.Members.ListMembers;

public static class ListMembersHandler
{
    public static async Task<Result<ListPage<MemberView>>> HandleAsync(
        ListMembersQuery query,
        IMemberQueries members,
        CancellationToken cancellationToken) =>
        await members.ListMembersAsync(query.List, query.Role is { } role ? Enum.Parse<BuiltInRole>(role) : null, cancellationToken);
}
