using ControlPlane.Application.Members;
using ControlPlane.Application.Members.ListMembers;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.Tenants;
using ControlPlane.Infrastructure.Persistence.Lists;
using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace ControlPlane.Infrastructure.Persistence.Members;

/// <summary>
/// The active tenant's members for the queries, read without tracking. Row level security keeps them to the tenant; the queries add no
/// filter of their own.
/// </summary>
/// <remarks>Public only so that Wolverine's generated code can build it on the handler's own DbContext (W1, W9).</remarks>
public sealed class MemberQueries(ControlPlaneDbContext controlPlane) : IMemberQueries
{
    Task<Membership?> IMemberQueries.FindMembershipAsync(Guid userId, CancellationToken cancellationToken) =>
        controlPlane.TenantMemberships.AsNoTracking().SingleOrDefaultAsync(membership => membership.UserId == userId, cancellationToken);

    // Row level security keeps the list to the active tenant; the query names no tenant. The users table is not under row level security
    // (R6), so the query starts from the memberships and joins each member's user: a user of another tenant never enters it.
    async Task<ListPage<(Guid UserId, string? Email, string? FirstName, string? LastName, BuiltInRole Role, bool Deleted, DateTimeOffset JoinedAt)>> IMemberQueries.ListMembersAsync(
        ListRequest<ListMembersSort> list,
        BuiltInRole? role,
        CancellationToken cancellationToken)
    {
        var members =
            from membership in controlPlane.TenantMemberships
            join memberRole in controlPlane.Roles on membership.RoleId equals memberRole.Id
            join user in controlPlane.Users on membership.UserId equals user.Id
            select new { membership.UserId, user.Email, user.FirstName, user.LastName, memberRole.BuiltIn, Deleted = user.DeletedAt != null, membership.JoinedAt };
        if (role is { } wanted)
        {
            members = members.Where(member => member.BuiltIn == wanted);
        }

        if (list.Search is { } search)
        {
            var pattern = ListQueries.ContainsPattern(search);
            members = members.Where(member =>
                EF.Functions.ILike(member.Email!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(member.FirstName!, pattern, ListQueries.Escape)
                || EF.Functions.ILike(member.LastName!, pattern, ListQueries.Escape));
        }

        // A member without an email address or a last name, such as a deleted user, comes last in both directions.
        var total = await members.CountAsync(cancellationToken);
        var sorted = list.Sort switch
        {
            ListMembersSort.Email => members.OrderBy(member => member.Email == null).ThenBy(member => member.Email, list.Descending),
            ListMembersSort.LastName => members.OrderBy(member => member.LastName == null).ThenBy(member => member.LastName, list.Descending),
            _ => members.OrderBy(
                member => member.BuiltIn == BuiltInRole.Owner ? 0 : member.BuiltIn == BuiltInRole.Admin ? 1 : 2,
                list.Descending),
        };
        var page = await sorted
            .ThenBy(member => member.UserId)
            .Skip(list.Paging.Skip)
            .Take(list.Paging.PageSize)
            .ToListAsync(cancellationToken);

        return ListPage.Of([.. page.Select(member => (member.UserId, member.Email, member.FirstName, member.LastName, member.BuiltIn, member.Deleted, member.JoinedAt))], list.Paging, total);
    }
}
