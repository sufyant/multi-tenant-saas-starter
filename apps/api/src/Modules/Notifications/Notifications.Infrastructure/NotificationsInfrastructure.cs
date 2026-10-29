using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Notifications.Application.Email;
using Notifications.Application.EmailPreferences;
using Notifications.Application.Invitations;
using Notifications.Application.Notifications;
using Notifications.Infrastructure.Email;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Persistence.EmailPreferences;
using Notifications.Infrastructure.Persistence.Notifications;
using SharedKernel;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace Notifications.Infrastructure;

/// <summary>
/// The module's infrastructure entry point: the host calls it to register the module's DbContext, adapters and ports. The host registers
/// the live channel (<see cref="ILiveChannel"/>), which the module signals after each commit that changed a notification (U6).
/// </summary>
public static class NotificationsInfrastructure
{
    /// <summary>The key of the pauses before each new try of an invitation email.</summary>
    public const string InvitationEmailRetryDelaysKey = $"{InvitationEmailOptions.Section}:{nameof(InvitationEmailOptions.RetryDelays)}";

    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<NotificationsDbContext>(
            (provider, options) => options
                .UseModuleDatabase(provider, NotificationsDbContext.Schema)
                .AddInterceptors(new LiveSignalInterceptor(provider.GetRequiredService<ILiveChannel>())),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<NotificationsDbContext>(NotificationsDbContext.Schema);

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext (W1).
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationQueries, NotificationQueries>();
        services.AddScoped<IEmailPreferenceRepository, EmailPreferenceRepository>();

        // Outside Development email goes only through Resend. Without its settings no email could leave, invitations included, so the
        // application does not start.
        services.AddOptions<ResendOptions>()
            .BindConfiguration(ResendOptions.Section)
            .Validate<IHostEnvironment>(
                (resend, environment) => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(resend.ApiKey),
                $"{ResendOptions.Section}:ApiKey must be set outside Development, or no email is sent.")
            .Validate<IHostEnvironment>(
                (resend, environment) => environment.IsDevelopment() || !string.IsNullOrWhiteSpace(resend.From),
                $"{ResendOptions.Section}:From must be set outside Development, or no email is sent.")
            .Validate(resend => resend.ApiUrl.IsAbsoluteUri, $"{ResendOptions.Section}:ApiUrl must be an absolute URL.")
            .Validate(resend => resend.Timeout > TimeSpan.Zero, $"{ResendOptions.Section}:Timeout must be positive.")
            .ValidateOnStart();

        services.AddHttpClient<ResendEmailChannel>((provider, http) =>
        {
            var resend = provider.GetRequiredService<IOptions<ResendOptions>>().Value;
            http.BaseAddress = resend.ApiUrl;
            http.DefaultRequestHeaders.Authorization = new("Bearer", resend.ApiKey);
            http.Timeout = resend.Timeout;
        });

        // Development without Resend writes email to the log; anywhere else email goes through Resend.
        services.AddTransient<IEmailChannel>(provider =>
            provider.GetRequiredService<IOptions<ResendOptions>>().Value.IsConfigured
            || !provider.GetRequiredService<IHostEnvironment>().IsDevelopment()
                ? provider.GetRequiredService<ResendEmailChannel>()
                : ActivatorUtilities.CreateInstance<LogEmailChannel>(provider));

        // Wolverine's generated code cannot build the email channel, which the container chooses by environment, so it resolves it
        // from the message's scope. It reaches no DbContext.
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IEmailChannel>());

        services.AddOptions<InvitationEmailOptions>()
            .BindConfiguration(InvitationEmailOptions.Section)
            .Validate(settings => settings.IsValid, $"{InvitationEmailRetryDelaysKey} must be positive pauses that grow.")
            .ValidateOnStart();
        services.AddSingleton<IWolverineExtension, InvitationEmailRetries>();

        return services;
    }
}
