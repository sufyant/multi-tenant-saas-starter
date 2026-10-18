using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.DeleteUser;
using ControlPlane.Application.Users.GetLastOwnership;
using ControlPlane.Application.Users.GetUser;
using ControlPlane.Application.Users.ListUserMemberships;
using ControlPlane.Application.Users.ListUsers;
using ControlPlane.Application.Users.ReactivateUser;
using ControlPlane.Application.Users.RemoveUserMembership;
using ControlPlane.Application.Users.RenameUser;
using ControlPlane.Application.Users.SuspendUser;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.Users;

/// <summary>The catalog users a system admin works with. Users belong to no tenant, so every command runs without one.</summary>
public static class UserEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/users", ListUsers).RequireAuthorization(Permissions.PlatformUsersRead);
        platform.MapGet("/users/{userId:guid}", GetUser).RequireAuthorization(Permissions.PlatformUsersRead);
        platform.MapPost("/users/{userId:guid}/suspend", SuspendUser).RequireAuthorization(Permissions.PlatformUsersSuspend);
        platform.MapPost("/users/{userId:guid}/reactivate", ReactivateUser).RequireAuthorization(Permissions.PlatformUsersReactivate);
        platform.MapPatch("/users/{userId:guid}", RenameUser).RequireAuthorization(Permissions.PlatformUsersUpdate);
        platform.MapDelete("/users/{userId:guid}", DeleteUser).RequireAuthorization(Permissions.PlatformUsersDelete);
    }

    private static async Task<Result<ListPage<UserResponse>>> ListUsers(
        int? page,
        int? pageSize,
        ListSort<ListUsersSort>? sort,
        string? q,
        ListFilter<UserResponseStatus>? status,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var filter = ListFilter.Read(status, nameof(status));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        var users = await bus.InvokeAsync<Result<ListPage<UserView>>>(
            new ListUsersQuery(list.Value, filter.Value is { } chosen ? ResponseStatus.OfUser(chosen) : null),
            cancellationToken);
        return users.Map(found => found.Map(ToResponse));
    }

    // The user's memberships are under row level security; the query declares the user (R11), as a user's deletion does.
    private static async Task<Result<UserDetailResponse>> GetUser(Guid userId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetailView>>(new GetUserQuery(userId), cancellationToken)).Map(ToResponse);

    // Suspension and reactivation set a state, so they take no idempotency key (I1).
    private static async Task<Result<UserResponse>> SuspendUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserView>>(new SuspendUserCommand(user.Id(), userId), cancellationToken)).Map(ToResponse);

    private static async Task<Result<UserResponse>> ReactivateUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserView>>(new ReactivateUserCommand(user.Id(), userId), cancellationToken)).Map(ToResponse);

    // Renaming sets a state, so it takes no idempotency key (I1).
    private static async Task<Result<UserResponse>> RenameUser(
        Guid userId,
        RenameUserRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserView>>(new RenameUserCommand(user.Id(), userId, request.FirstName, request.LastName), cancellationToken)).Map(ToResponse);

    // A user's memberships are under row level security, and a transaction declares one tenant (R4), so the deletion runs in steps, each
    // in a transaction of its own. First the check: a tenant always has at least one Owner (K1), so the user's tenants where they are the
    // last Owner answer 409 and nothing changes. Then the user leaves each tenant, under that tenant's lock, which checks again; a tenant
    // whose last Owner the user became meanwhile stops the deletion with the same 409, and the tenants the user already left stay left.
    // Last, with no membership left, the catalog erases the user, and the identity provider deletes its user after that commit. A
    // deletion sets a state, so it takes no idempotency key (I1); a second one answers 204 again.
    private static async Task<Result> DeleteUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken)
    {
        var memberships = await bus.InvokeAsync<Result<UserMembership[]>>(new ListUserMembershipsQuery(userId), cancellationToken);
        if (!memberships.IsSuccess)
        {
            return memberships.Error;
        }

        var lastOwner = new List<Guid>();
        foreach (var owned in memberships.Value.Where(membership => membership.OwnsATenantPastCreation))
        {
            if (await bus.InvokeForTenantAsync<bool>(owned.TenantId.ToString(), new GetLastOwnershipQuery(userId), cancellationToken))
            {
                lastOwner.Add(owned.TenantId);
            }
        }

        if (lastOwner.Count > 0)
        {
            return Errors.UserLastOwner(lastOwner);
        }

        foreach (var membership in memberships.Value)
        {
            var removed = await bus.InvokeForTenantAsync<Result>(membership.TenantId.ToString(), new RemoveUserMembershipCommand(userId), cancellationToken);
            if (!removed.IsSuccess)
            {
                return removed;
            }
        }

        return await bus.InvokeAsync<Result>(new DeleteUserCommand(user.Id(), userId), cancellationToken);
    }

    private static UserResponse ToResponse(UserView user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        ResponseStatus.OfUser(user.Status),
        user.IsSystemAdmin,
        user.MembershipCount,
        user.CreatedAt);

    private static UserDetailResponse ToResponse(UserDetailView user) => new(
        user.User.Id,
        user.User.Email,
        user.User.FirstName,
        user.User.LastName,
        ResponseStatus.OfUser(user.User.Status),
        user.User.IsSystemAdmin,
        user.User.MembershipCount,
        user.User.CreatedAt,
        user.SuspendedAt,
        user.SuspendedBy is { } suspendedBy ? InvitationEndpoints.ToResponse(suspendedBy) : null,
        [.. user.Memberships.Select(ToResponse)]);

    private static UserMembershipResponse ToResponse(UserMembershipView membership) => new(
        membership.TenantId,
        membership.TenantName,
        membership.TenantSlug,
        ResponseStatus.OfUserMembership(membership.TenantStatus),
        MemberEndpoints.ToResponse(membership.Role),
        membership.JoinedAt,
        membership.LastOwner);
}
