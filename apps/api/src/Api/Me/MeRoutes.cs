using Api.Authorization;

namespace Api.Me;

// The signed-in user, acting for themselves: any signed-in user may call these endpoints, without a permission, and reaches only
// their own data (A5). A suspended user is refused on every one of them (A9).
internal static class MeRoutes
{
    public static RouteGroupBuilder MapMe(this RouteGroupBuilder v1) => v1.MapGroup("/me").WithMetadata(new MeEndpoint()).RequireSignedIn();
}

internal sealed class MeEndpoint;
