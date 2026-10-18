using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Members;
using ControlPlane.Application.Members.ChangeMemberRole;
using ControlPlane.Application.Members.ListMembers;
using ControlPlane.Application.Members.RemoveMember;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.Members;

/// <summary>The members of the tenant in the route.</summary>
public static class MemberEndpoints
{
    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/members", ListMembers).RequireAuthorization(Permissions.TenantMembersRead);
        tenant.MapPatch("/members/{userId:guid}", ChangeMemberRole).RequireAuthorization(Permissions.TenantMembersUpdate);
        tenant.MapDelete("/members/{userId:guid}", RemoveMember).RequireAuthorization(Permissions.TenantMembersDelete);
    }

    // Every response that holds a role answers it in this one shape (N7). The built-in role keeps ControlPlane's name, as a request
    // gives it (RoleNameTests).
    internal static RoleResponse ToResponse(RoleView role) => new(Enum.Parse<MemberRole>(role.BuiltIn), role.Name);

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<MemberResponse>>> ListMembers(
        int? page,
        int? pageSize,
        ListSort<ListMembersSort>? sort,
        string? q,
        ListFilter<MemberRole>? role,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var filter = ListFilter.Read(role, nameof(role));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        var members = await bus.InvokeAsync<Result<ListPage<MemberView>>>(new ListMembersQuery(list.Value, filter.Value?.ToString()), cancellationToken);
        return members.Map(found => found.Map(ToResponse));
    }

    // Changing a role sets a state, so it takes no idempotency key (I1): the role the member already has answers 200 again.
    private static async Task<Result<MemberResponse>> ChangeMemberRole(
        Guid userId,
        ChangeMemberRoleRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var changed = await bus.InvokeAsync<Result<MemberView>>(new ChangeMemberRoleCommand(user.Id(), userId, request.Role.ToString()), cancellationToken);
        return changed.Map(ToResponse);
    }

    // Removing sets a state, so it takes no idempotency key (I1).
    private static async Task<Result> RemoveMember(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new RemoveMemberCommand(user.Id(), userId), cancellationToken);

    private static MemberResponse ToResponse(MemberView member) =>
        new(member.UserId, member.Email, member.FirstName, member.LastName, ToResponse(member.Role), member.Deleted, member.JoinedAt);
}
