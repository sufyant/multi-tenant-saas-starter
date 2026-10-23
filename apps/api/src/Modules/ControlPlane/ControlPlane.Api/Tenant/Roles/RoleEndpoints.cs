using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Members;
using ControlPlane.Application.Roles;
using ControlPlane.Application.Roles.CreateRole;
using ControlPlane.Application.Roles.DeleteRole;
using ControlPlane.Application.Roles.ListRoles;
using ControlPlane.Application.Roles.UpdateRole;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Tenant.Roles;

/// <summary>The roles of the tenant in the route: its built-in roles, and the custom roles it builds from the tenant pool (A3).</summary>
public static class RoleEndpoints
{
    // RFC 9110, section 15.3.2: the API has no GET for one role, so a new role's 201 sends no Location, as an invitation's does.
    private const string NoLocation =
        "Answers 201 with the role and without Location: the API has no GET for one role, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/roles", ListRoles).RequireAuthorization(Permissions.TenantRolesRead);
        tenant.MapPost("/roles", CreateRole)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .RequireAuthorization(Permissions.TenantRolesCreate)
            .Produces<RoleDetailResponse>(StatusCodes.Status201Created);
        tenant.MapPatch("/roles/{roleId:guid}", UpdateRole).RequireAuthorization(Permissions.TenantRolesUpdate);
        tenant.MapDelete("/roles/{roleId:guid}", DeleteRole).RequireAuthorization(Permissions.TenantRolesDelete);
    }

    // Every response that holds a role answers it in this one shape (N7k).
    internal static RoleResponse ToResponse(RoleView role) => new(role.Id, role.Name, role.BuiltIn);

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<RoleDetailResponse>>> ListRoles(
        int? page,
        int? pageSize,
        ListSort<ListRolesSort>? sort,
        string? q,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var roles = await bus.InvokeAsync<Result<ListPage<RoleDetailView>>>(new ListRolesQuery(list.Value), cancellationToken);
        return roles.Map(found => found.Map(ToResponse));
    }

    // The role is created inside the tenant of the route, which the tenant door has proved, under the request's idempotency key (I1). The
    // server chooses the role's id. The endpoint declares 201, so the host answers a new role with 201, and a replayed request maps its
    // saved result the same way (I5).
    private static async Task<Result<RoleDetailResponse>> CreateRole(
        Guid tenantId,
        CreateRoleRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<RoleDetailView>(
            new CreateRoleCommand(user.Id(), request.Name, request.Permissions), tenantId.ToString(), cancellationToken);
        return created.Map(ToResponse);
    }

    // Changing a role sets a state, so it takes no idempotency key (I1): the name and the permissions it has answer 200 again.
    private static async Task<Result<RoleDetailResponse>> UpdateRole(
        Guid roleId,
        UpdateRoleRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var updated = await bus.InvokeAsync<Result<RoleDetailView>>(
            new UpdateRoleCommand(user.Id(), roleId, request.Name, request.Permissions), cancellationToken);
        return updated.Map(ToResponse);
    }

    // Deleting takes no idempotency key (I1): GET and DELETE take none. A role deleted before is not found.
    private static async Task<Result> DeleteRole(Guid roleId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new DeleteRoleCommand(user.Id(), roleId), cancellationToken);

    private static RoleDetailResponse ToResponse(RoleDetailView role) => new(role.Id, role.Name, role.BuiltIn, role.Permissions);
}
