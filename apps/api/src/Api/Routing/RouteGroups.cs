using Api.Authorization;
using Api.ErrorHandling;
using Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;

namespace Api.Routing;

// The doors of the API, in the order the URLs read (N5). Each audience has one route group here: its prefix, who may enter and its
// extra rules. The modules map their endpoints onto these groups.
internal static class RouteGroups
{
    public const string TenantIdParameter = "tenantId";

    public const long MaxBodySize = 1024 * 1024;

    // Every API route lives under a version segment; expected failures returned as results become Problem Details here.
    // The group grants no access: each endpoint states its own (A5).
    public static RouteGroupBuilder MapV1(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup("/v1")
            .AddEndpointFilter<ResultEndpointFilter>()
            .DescribeResults()
            .RequireUserRateLimit();

    // The system door (T5): a separate route group for system admins, with its own authorization policy. ControlPlane decides who
    // passes, including the second factor (A6).
    public static RouteGroupBuilder MapAdmin(this RouteGroupBuilder v1) =>
        v1.MapGroup("/admin")
            .WithMetadata(new AdminEndpoint())
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

    // An external system that calls the API, proven by a signature (N5). A webhook delivery carries no user, so the group asks for none;
    // each endpoint checks its sender's signature and declares it (A5). The version group's limit applies, and without a user it is kept
    // for each client address. The body is read whole before its signature is checked, so the group takes at most 1 MB (API4). The
    // OpenAPI document is the contract for our own clients, and the sender of a webhook does not read it, so the group is not in it (N5).
    public static RouteGroupBuilder MapWebhooks(this RouteGroupBuilder v1) =>
        v1.MapGroup("/webhooks").AllowAnonymous().WithMetadata(new RequestSizeLimitAttribute(MaxBodySize)).ExcludeFromDescription();
}

internal sealed class AdminEndpoint;

internal sealed class MeEndpoint;

internal sealed class TenantScopedEndpoint;
