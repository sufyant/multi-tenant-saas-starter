using SharedKernel;

namespace ControlPlane.Application.Members.ListMembers;

public static class ListMembersHandler
{
    public static async Task<Result<ListPage<MemberView>>> HandleAsync(
        ListMembersQuery query,
        IMemberQueries members,
        CancellationToken cancellationToken) =>
        await members.ListMembersAsync(query.List, query.RoleId, cancellationToken);
}
