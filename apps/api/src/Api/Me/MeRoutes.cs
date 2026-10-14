using Api.Authorization;

namespace Api.Me;

// The signed-in user, acting for themselves: any signed-in user may call these endpoints, without a permission, and reaches only
// their own data (A5).
internal static class MeRoutes
{
    public static RouteGroupBuilder MapMe(this RouteGroupBuilder v1) => v1.MapGroup("/me").RequireSignedIn();
}

internal sealed class MeEndpoint;
