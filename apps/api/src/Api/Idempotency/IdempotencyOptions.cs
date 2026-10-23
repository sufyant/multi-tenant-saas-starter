namespace Api.Idempotency;

public sealed class IdempotencyOptions
{
    public const string Section = "Host:Idempotency";
    public TimeSpan KeyRetention { get; set; } = TimeSpan.FromHours(24);
}

internal static class IdempotencyOptionsExtensions
{
    public static IServiceCollection AddIdempotencyOptions(this IServiceCollection services)
    {
        services.AddOptions<IdempotencyOptions>()
            .BindConfiguration(IdempotencyOptions.Section)
            .Validate(options => options.KeyRetention >= TimeSpan.FromHours(24), $"{IdempotencyOptions.Section}:KeyRetention must be at least 24 hours.")
            .ValidateOnStart();

        return services;
    }
}
