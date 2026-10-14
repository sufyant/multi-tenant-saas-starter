namespace Api.Idempotency;

public sealed class IdempotencyKey
{
    public required string Subject { get; init; }
    public required string Key { get; init; }
    public required string Method { get; init; }
    public required string Route { get; init; }
    public required string RequestHash { get; init; }
    public Guid Lease { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public string? Result { get; set; }
    public DateTimeOffset CreatedAt { get; init; }
}
