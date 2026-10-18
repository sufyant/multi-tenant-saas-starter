using Api.Idempotency;
using Api.RateLimiting;
using Microsoft.Net.Http.Headers;
using SharedKernel;

namespace Api.Hosting;

// The one map from an endpoint's metadata to the response headers that it makes the host send. The CORS policy exposes them to a
// browser, and the OpenAPI document declares them, so the two cannot drift (CorsExposedHeadersTests checks the result). The document
// declares them in this order.
internal static class ResponseHeaders
{
    public static IEnumerable<string> SentBy(IEnumerable<object> metadata)
    {
        var items = metadata as IReadOnlyCollection<object> ?? [.. metadata];

        // I5: a replayed answer says so.
        if (items.OfType<IdempotencyKeyRequired>().Any())
        {
            yield return IdempotencyExecutor.ReplayedHeader;
        }

        // N11: a 201 names the resource where a GET reads it.
        if (items.OfType<CreatedLocation>().Any())
        {
            yield return HeaderNames.Location;
        }

        // RFC 9110, section 10.2.3: the rate limiter's 429 says how long to wait.
        if (UserRateLimiting.Covers(items))
        {
            yield return HeaderNames.RetryAfter;
        }
    }
}
