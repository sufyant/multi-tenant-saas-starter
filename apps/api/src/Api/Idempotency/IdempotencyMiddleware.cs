using SharedKernel;

namespace Api.Idempotency;

internal sealed class IdempotencyMiddleware(RequestDelegate next)
{
    public const string Header = "Idempotency-Key";
    public const string TraceItem = "IdempotencyTraceId";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IdempotencyKeyRequired>() is not null)
        {
            var values = context.Request.Headers[Header];
            if (values.Count != 1 || values[0] is not { Length: > 0 and <= 255 } key || !key.All(character => character is >= '!' and <= '~'))
            {
                await Results.Problem(statusCode: StatusCodes.Status400BadRequest,
                    detail: $"The {Header} header must hold 1 to 255 visible ASCII characters.",
                    extensions: new Dictionary<string, object?> { ["code"] = "idempotency_key_invalid" }).ExecuteAsync(context);
                return;
            }

            context.Request.EnableBuffering();
        }

        await next(context);
    }
}
