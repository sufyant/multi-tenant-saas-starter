using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me;

/// <summary>The signed-in user's own profile.</summary>
public static class MyProfile
{
    public static void Map(RouteGroupBuilder me) => me.MapGet(string.Empty, GetMe);

    // Read without a tenant. A signed-in person the catalog does not know is not found: the web shows "no access yet".
    private static async Task<Result<MeResponse>> GetMe(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MeDetails>>(new GetMe(user.Id()), cancellationToken))
            .Map(me => new MeResponse(me.Id, me.Email, me.FirstName, me.LastName, me.IsSystemAdmin));
}

/// <summary>
/// The signed-in user: the catalog's id, not the identity provider's, the copy of their profile, and whether they are a system admin.
/// It has no status: a suspended user is refused on every me endpoint (A9), and a deleted user cannot sign in.
/// </summary>
public sealed record MeResponse(Guid Id, string? Email, string? FirstName, string? LastName, bool IsSystemAdmin);
