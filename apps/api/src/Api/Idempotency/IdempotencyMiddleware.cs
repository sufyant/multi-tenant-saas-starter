using Api.ErrorHandling;
using SharedKernel;

namespace Api.Idempotency;

internal sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string Header = "Idempotency-Key";
    public const string TraceItem = "IdempotencyTraceId";

    private static readonly Error KeyInvalid = Error.Validation(HostErrorCodes.IdempotencyKeyInvalid, $"The {Header} header must hold 1 to 255 visible ASCII characters.");

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyKeyRequired>() is not null)
        {
            var values = context.Request.Headers[Header];
            if (values.Count != 1 || values[0] is not { Length: > 0 and <= 255 } key || !key.All(character => character is >= '!' and <= '~'))
            {
                await Problems.For(KeyInvalid).ExecuteAsync(context);
                return;
            }

            context.Request.EnableBuffering();
        }

        await next(context);
    }
}
