using ControlPlane.Application.TermsVersions.GetTermsInForce;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Wolverine;

namespace ControlPlane.Api.Public.TermsVersions;

/// <summary>The terms version in force, for anyone before sign-in: the "Terms" link of the entry pages opens its link.</summary>
public static class PublicTermsVersionEndpoints
{
    /// <summary>
    /// How long a browser or a cache in between may keep the answer. A new version takes effect at the start of its day and a version in
    /// force never changes, so a link stays right for the whole period, but the first minutes of a new version's day.
    /// </summary>
    public static readonly TimeSpan CachePeriod = TimeSpan.FromMinutes(5);

    // A caller without a user is still under the rate limit, by their address (API4), so the read declares its 429. It never answers 401.
    public static void Map(RouteGroupBuilder publicRoutes) =>
        publicRoutes.MapGet("/terms", GetTermsInForce).ProducesProblem(StatusCodes.Status429TooManyRequests);

    // 204 when no version is in force yet: the entry pages then hide the link. The answer is the same for everyone, so any cache may keep it.
    private static async Task<Results<Ok<TermsInForceResponse>, NoContent>> GetTermsInForce(HttpContext context, IMessageBus bus, CancellationToken cancellationToken)
    {
        var version = await bus.InvokeAsync<TermsInForceView?>(new GetTermsInForceQuery(), cancellationToken);
        context.Response.Headers[HeaderNames.CacheControl] = $"public, max-age={(int)CachePeriod.TotalSeconds}";
        return version is null
            ? TypedResults.NoContent()
            : TypedResults.Ok(new TermsInForceResponse(version.Id, version.EffectiveDate, version.Url));
    }
}
