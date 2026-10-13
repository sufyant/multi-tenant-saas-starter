using Api.Authorization;

namespace Api.Admin;

// The admin door (T5): a separate route group for system admins, with its own authorization policy. ControlPlane decides who
// passes, including the second factor (A6).
internal static class AdminRoutes
{
    public static RouteGroupBuilder MapAdmin(this RouteGroupBuilder v1) =>
        v1.MapGroup("/admin")
            .WithMetadata(new AdminEndpoint())
            .RequireAuthorization(AccessPolicies.SystemAdmin);
}

internal sealed class AdminEndpoint;
