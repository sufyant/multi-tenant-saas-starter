using Api.ErrorHandling;
using SharedKernel;

namespace Api.Idempotency;

internal sealed class IdempotencyMiddleware(RequestDelegate next)
{
    // I2: the one name of the header and the one bound of its length. CORS, the OpenAPI document and the database column use them.
    public const string Header = "Idempotency-Key";
    public const int MaxLength = 255;
    public const string TraceItem = "IdempotencyTraceId";

    private static readonly Error KeyInvalid = Error.Validation(HostErrorCodes.IdempotencyKeyInvalid, $"The {Header} header must hold 1 to {MaxLength} visible ASCII characters.");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyKeyRequired>() is not null)
        {
            var values = context.Request.Headers[Header];
            if (values.Count != 1 || values[0] is not { Length: > 0 and <= MaxLength } key || !key.All(character => character is >= '!' and <= '~'))
            {
                await Problems.For(KeyInvalid).ExecuteAsync(context);
                return;
            }

            context.Request.EnableBuffering();
        }

        await next(context);
    }
}
