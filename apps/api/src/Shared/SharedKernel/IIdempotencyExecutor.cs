namespace SharedKernel;

/// <summary>The host runs an opted-in endpoint's command under its request key.</summary>
public interface IIdempotencyExecutor
{
    Task<Result<T>> ExecuteAsync<T>(object command, string? tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// The same, with a check that runs under the claim, before the command and outside its transaction, such as a question to an
    /// external service (I4). A refusal of the check is saved as the request's result.
    /// </summary>
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Error?>> check, object command, string? tenantId, CancellationToken cancellationToken);
}

/// <summary>Endpoint metadata: this operation requires an Idempotency-Key header.</summary>
public sealed class IdempotencyKeyRequired;
