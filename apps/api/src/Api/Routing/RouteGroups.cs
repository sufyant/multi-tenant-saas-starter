using Api.Authorization;
using Api.ErrorHandling;
using Api.RateLimiting;

namespace Api.Routing;

// The doors of the API, in the order the URLs read (N5). Each audience has one route group here: its prefix, who may enter and its
// extra rules. The modules map their endpoints onto these groups.
internal static class RouteGroups
{
    public const string TenantIdParameter = "tenantId";

    public const string V1 = "/v1";

    // Every API route lives under a version segment; expected failures returned as results become Problem Details here.
    // The group grants no access: each endpoint states its own (A5).
    public static RouteGroupBuilder MapV1(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup(V1)
            .AddEndpointFilter<ResultEndpointFilter>()
            .DescribeResults()
            .RequireUserRateLimit();

    // The system door (T5): a separate route group for system admins, with its own authorization policy. ControlPlane decides who
    // passes, including the second factor (A6).
    public static RouteGroupBuilder MapPlatform(this RouteGroupBuilder v1) =>
        v1.MapGroup("/platform")
            .WithMetadata(new PlatformEndpoint())
            .RequireAuthorization(AccessPolicies.SystemAdmin);

    // The signed-in user, acting for themselves: any signed-in user may call these endpoints, without a permission, and reaches only
    // their own data (A5). A suspended user is refused on every one of them (A9).
    public static RouteGroupBuilder MapMe(this RouteGroupBuilder v1) => v1.MapGroup("/me").WithMetadata(new MeEndpoint()).RequireSignedIn();

    // Tenant routes are /v1/tenants/{tenantId}/... (T1); a value that is not a GUID matches no route. Every endpoint in the group
    // runs only for a verified member (T7).
    public static RouteGroupBuilder MapTenant(this RouteGroupBuilder v1) =>
        v1.MapGroup($"/tenants/{{{TenantIdParameter}:guid}}")
            .WithMetadata(new TenantScopedEndpoint())
            .RequireAuthorization(AccessPolicies.TenantMember);
}

internal sealed class PlatformEndpoint;

internal sealed class MeEndpoint;

internal sealed class TenantScopedEndpoint;
