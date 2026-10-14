using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Admin;

/// <summary>The catalog users a system admin works with. Users belong to no tenant, so every command runs without one.</summary>
public static class Users
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapGet("/users", ListUsers).RequireAuthorization(Permissions.AdminUsersRead);
        admin.MapPost("/users/{userId:guid}/suspend", SuspendUser).RequireAuthorization(Permissions.AdminUsersSuspend);
        admin.MapPost("/users/{userId:guid}/reactivate", ReactivateUser).RequireAuthorization(Permissions.AdminUsersReactivate);
    }

    private static async Task<Result<ListPage<UserResponse>>> ListUsers(
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

        var users = await bus.InvokeAsync<Result<ListPage<UserDetails>>>(new ListUsers(paging.Value), cancellationToken);
        return users.Map(list => list.Map(ToResponse));
    }

    // Suspension and reactivation set a state, so they take no idempotency key (I1).
    private static async Task<Result<UserResponse>> SuspendUser(Guid userId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetails>>(new SuspendUser(userId), cancellationToken)).Map(ToResponse);

    private static async Task<Result<UserResponse>> ReactivateUser(Guid userId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetails>>(new ReactivateUser(userId), cancellationToken)).Map(ToResponse);

    private static UserResponse ToResponse(UserDetails user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Status, user.IsSystemAdmin);
}

/// <summary>
/// A catalog user: the catalog's id, not the identity provider's, the copy of their profile, their status (active, suspended or
/// deleted) and whether they are a system admin.
/// </summary>
public sealed record UserResponse(Guid Id, string? Email, string? FirstName, string? LastName, string Status, bool IsSystemAdmin);
