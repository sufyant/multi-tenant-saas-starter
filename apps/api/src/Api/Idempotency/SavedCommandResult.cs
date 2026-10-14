using System.Text.Json;
using SharedKernel;

namespace Api.Idempotency;

internal sealed record SavedCommandResult<T>(T? Value, Error? Error, string TraceId)
{
    public static string Serialize(Result<T> result, string traceId) =>
        JsonSerializer.Serialize(result.IsSuccess ? new(result.Value, null, traceId) : new SavedCommandResult<T>(default, result.Error, traceId));

    public Result<T> ToResult() => Error is null ? Value! : Error;
}
