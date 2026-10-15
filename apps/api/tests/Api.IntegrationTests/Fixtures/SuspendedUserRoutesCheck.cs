using Api.Me;
using Api.Tenant;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Api.IntegrationTests.Fixtures;

// A9: a suspended user is refused in one place. TenantResolutionMiddleware asks for the user's state on an endpoint that carries the
// me or the tenant metadata, and authorization turns the user away. So an endpoint on a me or a tenant path must carry its
// audience's metadata, and must not let anonymous callers skip authorization; the me metadata on any other path is a mistake.
internal static class SuspendedUserRoutesCheck
{
    public const string MePathPrefix = "/v1/me/";

    public static List<string> Gaps(IEnumerable<Endpoint> endpoints) =>
        [.. endpoints.OfType<RouteEndpoint>().SelectMany(Gaps)];

    private static IEnumerable<string> Gaps(RouteEndpoint endpoint)
    {
        var path = endpoint.RoutePattern.RawText ?? string.Empty;
        var onMePath = path.StartsWith(MePathPrefix, StringComparison.Ordinal);
        var onTenantPath = path.StartsWith(TenantRoutesCheck.TenantPathPrefix, StringComparison.Ordinal);
        var meMetadata = endpoint.Metadata.GetMetadata<MeEndpoint>() is not null;
        var tenantMetadata = endpoint.Metadata.GetMetadata<TenantScopedEndpoint>() is not null;
        var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (onMePath && !meMetadata)
        {
            yield return $"{path}: on a me path, without the me metadata";
        }

        if (onTenantPath && !tenantMetadata)
        {
            yield return $"{path}: on a tenant path, without the tenant metadata";
        }

        if ((onMePath || onTenantPath) && anonymous)
        {
            yield return $"{path}: lets anonymous callers skip the check";
        }

        if (meMetadata && !onMePath)
        {
            yield return $"{path}: carries the me metadata, outside a me path";
        }
    }
}
