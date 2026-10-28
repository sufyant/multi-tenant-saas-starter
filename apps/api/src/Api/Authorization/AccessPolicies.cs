using System.Reflection;
using Api.ErrorHandling;
using ControlPlane.Contracts.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace Api.Authorization;

/// <summary>Marks an endpoint any signed-in user may call, without a permission (A5).</summary>
public sealed class SignedInEndpoint;

// Authorization asks for permissions, never roles. Every endpoint states its access (A5): public with AllowAnonymous, signed-in
// only with RequireSignedIn, or the permission it needs as its policy. The route groups add who may reach them at all. A tenant endpoint
// may also name a feature of the tenant's plan as a policy, which the tenant's effective entitlements decide
// (docs/flows/plans-and-entitlements.md).
internal static class AccessPolicies
{
    public const string TenantMember = "tenant-member";

    public const string SystemAdmin = "system-admin";

    public const string SignedIn = "signed-in";

    public static IServiceCollection AddAccessAuthorization(this IServiceCollection services)
    {
        services.AddScoped<RequestAccess>();
        services.AddSingleton<IAuthorizationHandler, AccessHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, AccessDeniedHandler>();

        return services.AddAuthorizationBuilder()
            .AddPolicy(TenantMember, policy => policy.RequireAuthenticatedUser().AddRequirements(new TenantMemberRequirement()))
            .AddPolicy(SystemAdmin, policy => policy.RequireAuthenticatedUser().AddRequirements(new SystemAdminRequirement()))
            .AddPolicy(SignedIn, policy => policy.RequireAuthenticatedUser())

            // An endpoint that states no access is refused, never opened.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().AddRequirements(new AccessStatedRequirement()).Build())
            .Services;
    }

    /// <summary>The signed-in only state (A5): any signed-in user may call the endpoint, without a permission.</summary>
    public static TBuilder RequireSignedIn<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(SignedIn).WithMetadata(new SignedInEndpoint());

    // A policy name is a feature key of the plans' list, or else a permission. The two lists never share a name: a key has no audience.
    private sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : DefaultAuthorizationPolicyProvider(options)
    {
        private static readonly HashSet<string> FeatureKeys =
        [
            .. typeof(Features).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(field => field.IsLiteral)
                .Select(field => (string)field.GetRawConstantValue()!),
        ];

        public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName) =>
            await base.GetPolicyAsync(policyName)
            ?? new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(FeatureKeys.Contains(policyName) ? new FeatureRequirement(policyName) : new PermissionRequirement(policyName))
                .Build();
    }

    // A caller ControlPlane refused is told why, in the error shape of every other refusal and with the refusal's own status. A tenant
    // route the caller cannot enter answers ControlPlane's tenant.not_found, the handlers' own answer, so it looks the same as a
    // tenant that does not exist (T3). Any other refused caller gets the default 401 or 403, and Problems gives it its code.
    private sealed class AccessDeniedHandler : IAuthorizationMiddlewareResultHandler
    {
        private readonly AuthorizationMiddlewareResultHandler _default = new();

        public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
        {
            var access = context.RequestServices.GetRequiredService<RequestAccess>();
            if (authorizeResult.Forbidden && (access.Refusal ?? access.FeatureRefusal) is { } refusal)
            {
                await Problems.For(refusal).ExecuteAsync(context);
                return;
            }

            await _default.HandleAsync(next, context, policy, authorizeResult);
        }
    }
}

internal sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

internal sealed record FeatureRequirement(string Feature) : IAuthorizationRequirement;

internal sealed record TenantMemberRequirement : IAuthorizationRequirement;

internal sealed record SystemAdminRequirement : IAuthorizationRequirement;

// The fallback policy's only requirement. It reaches a request whose endpoint states no access; a request that matched no route
// (an unknown path, or a method the route does not take) has nothing to open and keeps its 404 or 405.
internal sealed record AccessStatedRequirement : IAuthorizationRequirement;

internal sealed class AccessHandler : IAuthorizationHandler
{
    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        if (context.Resource is not HttpContext http)
        {
            return;
        }

        // A refused caller meets no requirement, whatever the endpoint asks (A9).
        var access = http.RequestServices.GetRequiredService<RequestAccess>();
        if (access.Refusal is not null)
        {
            context.Fail();
            return;
        }

        foreach (var requirement in context.PendingRequirements.Where(requirement => requirement is not FeatureRequirement).ToList())
        {
            var met = requirement switch
            {
                PermissionRequirement permission => access.Has(permission.Permission),
                TenantMemberRequirement => access.Membership is not null,
                SystemAdminRequirement => access.PlatformPermissions is not null,
                AccessStatedRequirement => http.GetEndpoint() is not RouteEndpoint,
                _ => false,
            };

            if (met)
            {
                context.Succeed(requirement);
            }
        }

        // A feature is asked last, and only of a member who meets every other requirement: a caller without the permission is told
        // that, and costs no read of the plan. The tenant's effective entitlements decide, from the cache.
        var features = context.PendingRequirements.OfType<FeatureRequirement>().ToList();
        var unmet = context.PendingRequirements.Any(requirement => requirement is PermissionRequirement or TenantMemberRequirement or SystemAdminRequirement);
        if (features.Count == 0 || unmet || access.Membership is not { } membership)
        {
            return;
        }

        var entitlements = http.RequestServices.GetRequiredService<IEntitlementDirectory>();
        foreach (var feature in features)
        {
            var on = await entitlements.CheckFeatureAsync(membership.TenantId, feature.Feature, http.RequestAborted);
            if (!on.IsSuccess)
            {
                access.FeatureRefusal = on.Error;
                return;
            }

            context.Succeed(feature);
        }
    }
}
