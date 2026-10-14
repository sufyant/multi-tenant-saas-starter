namespace Api.Idempotency;

public sealed class IdempotencySettings
{
    public const string Section = "Api";
    public TimeSpan IdempotencyKeyRetention { get; set; } = TimeSpan.FromHours(24);
}
