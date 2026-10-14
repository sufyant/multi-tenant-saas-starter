namespace SharedKernel;

/// <summary>The host runs an opted-in endpoint's command under its request key.</summary>
public interface IIdempotencyExecutor
{
    Task<Result<T>> ExecuteAsync<T>(object command, string? tenantId, CancellationToken cancellationToken);
}

/// <summary>Endpoint metadata: this operation requires an Idempotency-Key header.</summary>
public sealed class IdempotencyKeyRequired;
