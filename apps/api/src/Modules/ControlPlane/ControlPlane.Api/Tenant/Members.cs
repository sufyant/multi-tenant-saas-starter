using ControlPlane.Application.Members;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant;

/// <summary>The members of the tenant in the route.</summary>
public static class Members
{
    public static void Map(RouteGroupBuilder tenant) =>
        tenant.MapGet("/members", ListMembers).RequireAuthorization(Permissions.TenantMembersRead);

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<MemberResponse>>> ListMembers(
        int? page,
        int? pageSize,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.Create(page, pageSize);
        if (!paging.IsSuccess)
        {
            return paging.Error;
        }

        var members = await bus.InvokeAsync<Result<ListPage<MemberSummary>>>(new ListMembers(paging.Value), cancellationToken);
        return members.Map(list => list.Map(member => new MemberResponse(member.UserId, member.Role)));
    }
}

/// <summary>A member of the tenant: the user's id and their role, Owner, Admin or Member.</summary>
public sealed record MemberResponse(Guid UserId, string Role);
