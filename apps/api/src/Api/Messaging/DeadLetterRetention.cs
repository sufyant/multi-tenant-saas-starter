using Wolverine;

namespace Api.Messaging;

internal static class DeadLetterRetention
{
    public static IServiceCollection AddDeadLetterOptions(this IServiceCollection services)
    {
        services.AddOptions<DeadLetterOptions>()
            .BindConfiguration(DeadLetterOptions.Section)
            .Validate(options => options.Retention > TimeSpan.Zero, $"{DeadLetterOptions.Section}:Retention must be positive.")
            .ValidateOnStart();

        return services;
    }

    // Wolverine keeps a dead letter forever unless expiration is on, which MessageStorage turns on (W13). A worker's durability agent
    // deletes the expired ones. The setting is read while Wolverine is configured and checked on start like every other setting.
    public static void Configure(WolverineOptions options, IConfiguration configuration)
    {
        var settings = configuration.GetSection(DeadLetterOptions.Section).Get<DeadLetterOptions>() ?? new DeadLetterOptions();
        options.Durability.DeadLetterQueueExpiration = settings.Retention;
    }
}

// How long Wolverine keeps a dead letter (Messaging:DeadLetters, W13).
internal sealed class DeadLetterOptions
{
    public const string Section = "Messaging:DeadLetters";

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(14);
}
