using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Tenant;
using ControlPlane.Application;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.UserDeletion;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform;

/// <summary>The catalog users a system admin works with. Users belong to no tenant, so every command runs without one.</summary>
public static class Users
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

        var users = await bus.InvokeAsync<Result<ListPage<UserDetails>>>(
            new ListUsers(list.Value, filter.Value is { } chosen ? ResponseStatus.OfUser(chosen) : null),
            cancellationToken);
        return users.Map(found => found.Map(ToResponse));
    }

    // The user's memberships are under row level security; the query declares the user (R11), as a user's deletion does.
    private static async Task<Result<UserDetailResponse>> GetUser(Guid userId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserWithMemberships>>(new GetUser(userId), cancellationToken)).Map(found => new UserDetailResponse(
            found.User.Id,
            found.User.Email,
            found.User.FirstName,
            found.User.LastName,
            ResponseStatus.OfUser(found.User.Status),
            found.User.IsSystemAdmin,
            found.User.MembershipCount,
            found.User.CreatedAt,
            found.SuspendedAt,
            found.SuspendedBy is { } admin ? Tenant.Invitations.ToResponse(admin) : null,
            [
                .. found.Memberships.Select(membership => new UserMembershipResponse(
                    membership.TenantId,
                    membership.TenantName,
                    membership.TenantSlug,
                    ResponseStatus.OfUserMembership(membership.TenantStatus),
                    Tenant.Members.ToResponse(membership.Role),
                    membership.JoinedAt,
                    membership.LastOwner)),
            ]));

    // Suspension and reactivation set a state, so they take no idempotency key (I1).
    private static async Task<Result<UserResponse>> SuspendUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetails>>(new SuspendUser(userId, user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<UserResponse>> ReactivateUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetails>>(new ReactivateUser(userId, user.Id()), cancellationToken)).Map(ToResponse);

    // Renaming sets a state, so it takes no idempotency key (I1).
    private static async Task<Result<UserResponse>> RenameUser(
        Guid userId,
        RenameUserRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<UserDetails>>(new RenameUser(userId, user.Id(), request.FirstName, request.LastName), cancellationToken)).Map(ToResponse);

    // A user's memberships are under row level security, and a transaction declares one tenant (R4), so the deletion runs in steps, each
    // in a transaction of its own. First the check: a tenant always has at least one Owner (K1), so the user's tenants where they are the
    // last Owner answer 409 and nothing changes. Then the user leaves each tenant, under that tenant's lock, which checks again; a tenant
    // whose last Owner the user became meanwhile stops the deletion with the same 409, and the tenants the user already left stay left.
    // Last, with no membership left, the catalog erases the user, and the identity provider deletes its user after that commit. A
    // deletion sets a state, so it takes no idempotency key (I1); a second one answers 204 again.
    private static async Task<Result> DeleteUser(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken)
    {
        var memberships = await bus.InvokeAsync<Result<UserMembership[]>>(new ListUserMemberships(userId), cancellationToken);
        if (!memberships.IsSuccess)
        {
            return memberships.Error;
        }

        var lastOwner = new List<Guid>();
        foreach (var owned in memberships.Value.Where(membership => membership.OwnsATenantPastCreation))
        {
            if (await bus.InvokeForTenantAsync<bool>(owned.TenantId.ToString(), new GetLastOwnership(userId), cancellationToken))
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
            var removed = await bus.InvokeForTenantAsync<Result>(membership.TenantId.ToString(), new RemoveUserMembership(userId), cancellationToken);
            if (!removed.IsSuccess)
            {
                return removed;
            }
        }

        return await bus.InvokeAsync<Result>(new DeleteUser(userId, user.Id()), cancellationToken);
    }

    private static UserResponse ToResponse(UserDetails user) => new(
        user.Id,
        user.Email,
        user.FirstName,
        user.LastName,
        ResponseStatus.OfUser(user.Status),
        user.IsSystemAdmin,
        user.MembershipCount,
        user.CreatedAt);
}

/// <summary>
/// A catalog user: the catalog's id, not the identity provider's, their email address and names, their status (active, suspended or
/// deleted), whether they are a system admin, how many tenants they are a member of, and when they joined the catalog.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// One catalog user for the user detail screen: as in the list, with when a system admin suspended them and that system admin's names
/// (null when the user is not suspended, or was suspended before step 20l), and every membership by the tenant's name.
/// </summary>
public sealed record UserDetailResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    UserResponseStatus Status,
    bool IsSystemAdmin,
    int MembershipCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SuspendedAt,
    UserNameResponse? SuspendedBy,
    IReadOnlyList<UserMembershipResponse> Memberships);

/// <summary>
/// A tenant the user is a member of: its id, name, slug and status, the user's role there, when they joined, and whether they are
/// its last Owner. The suspend dialog warns with <c>lastOwner</c>, and the delete refusal names those tenants.
/// </summary>
public sealed record UserMembershipResponse(
    Guid TenantId,
    string TenantName,
    string TenantSlug,
    UserMembershipResponseTenantStatus TenantStatus,
    RoleResponse Role,
    DateTimeOffset JoinedAt,
    bool LastOwner);

/// <summary>The status of a tenant a user is a member of: active, or suspended or closed when its members cannot enter it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserMembershipResponseTenantStatus>))]
public enum UserMembershipResponseTenantStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("closed")]
    Closed,
}

/// <summary>A user's new names: 1 to 100 characters each, trimmed.</summary>
public sealed record RenameUserRequest(string FirstName, string LastName);

/// <summary>The status of a catalog user. A system admin deleted a deleted user, and the catalog keeps no email address or name of theirs.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<UserResponseStatus>))]
public enum UserResponseStatus
{
    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("deleted")]
    Deleted,
}
