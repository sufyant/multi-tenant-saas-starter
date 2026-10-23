using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Platform.Users;
using ControlPlane.Application.SystemAdmins.GrantSystemAdmin;
using ControlPlane.Application.SystemAdmins.RevokeSystemAdmin;
using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.SystemAdmins;

/// <summary>
/// The system admin list, which a system admin changes for an existing user: a grant and a revocation (section 6, "Root system admins").
/// The routes sit under the user they change, and the grant answers the user (UserResponse, N7g).
/// </summary>
public static class SystemAdminEndpoints
{
    private const string NoLocation =
        "Answers 201 with the user and without Location: the API has no GET for one grant, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapPost("/users/{userId:guid}/system-admin", GrantSystemAdmin)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .RequireAuthorization(Permissions.PlatformSystemAdminsCreate)
            .Produces<UserResponse>(StatusCodes.Status201Created);
        platform.MapDelete("/users/{userId:guid}/system-admin", RevokeSystemAdmin).RequireAuthorization(Permissions.PlatformSystemAdminsDelete);
    }

    // A grant creates the user's row on the system admin list, so it takes an idempotency key (I1): a retry with the same key replays the
    // first answer, and a new grant of a system admin answers 409. The system admin list belongs to no tenant, so the command runs without
    // one.
    private static async Task<Result<UserResponse>> GrantSystemAdmin(
        Guid userId,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken) =>
        (await idempotency.ExecuteAsync<UserView>(new GrantSystemAdminCommand(user.Id(), userId), null, cancellationToken)).Map(UserEndpoints.ToResponse);

    // A DELETE takes no idempotency key (I1). The user stays a user, with every membership.
    private static async Task<Result> RevokeSystemAdmin(Guid userId, ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        await bus.InvokeAsync<Result>(new RevokeSystemAdminCommand(user.Id(), userId), cancellationToken);
}
