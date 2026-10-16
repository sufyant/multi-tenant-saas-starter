using ControlPlane.Application.Invitations;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using ControlPlane.Infrastructure.Clerk;
using ControlPlane.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tenancy;
using Wolverine;
using Wolverine.EntityFrameworkCore;

namespace ControlPlane.Infrastructure;

/// <summary>
/// The module's infrastructure entry point: the host calls it to register the module's DbContext, adapters and contract
/// implementations.
/// </summary>
public static class ControlPlaneInfrastructure
{
    /// <param name="servesTheApi">Whether the process serves the API, so it receives the identity provider's webhook too.</param>
    public static IServiceCollection AddControlPlaneInfrastructure(this IServiceCollection services, bool servesTheApi)
    {
        services.AddDbContextWithWolverineIntegration<ControlPlaneDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ControlPlaneDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<ControlPlaneDbContext>(ControlPlaneDbContext.Schema);
        services.AddScoped<ITenantDirectory, TenantDirectory>();
        services.AddScoped<IUserDirectory, UserDirectory>();

        // Registered by type, so Wolverine builds it in Audit's handler that names a page of an audit log, on this module's
        // DbContext and in its transaction (W1).
        services.AddScoped<IUserNames, UserNames>();
        services.AddScoped<ISystemAdminDirectory, SystemAdminDirectory>();
        services.AddOptions<SystemAdminSettings>().BindConfiguration(SystemAdminSettings.Section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SystemAdminSettings>, SystemAdminSettings.Validation>();

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext, and sees that the
        // handler needs its transaction (W1).
        services.AddScoped<ITenantCatalog, TenantCatalog>();
        services.AddScoped<IUserTenants, UserTenants>();
        services.AddScoped<IUserCatalog, UserCatalog>();

        services.AddOptions<InvitationSettings>()
            .BindConfiguration(InvitationSettings.Section)
            .Validate(settings => settings.AcceptUrl is { IsAbsoluteUri: true }, $"{InvitationSettings.Section}:AcceptUrl must be the absolute URL of the page that accepts invitations.")
            .Validate(settings => settings.Lifetime > TimeSpan.Zero, $"{InvitationSettings.Section}:Lifetime must be positive.")
            .Validate(settings => settings.DailyLimitPerTenant > 0, $"{InvitationSettings.Section}:DailyLimitPerTenant must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<InvitationSettings>>().Value);

        services.AddOptions<TenantSettings>()
            .BindConfiguration(TenantSettings.Section)
            .Validate(settings => settings.ReopenPeriod > TimeSpan.Zero, $"{TenantSettings.Section}:ReopenPeriod must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantSettings>>().Value);

        services.AddOptions<TenantCreationSettings>()
            .BindConfiguration(TenantCreationSettings.Section)
            .Validate(settings => settings.ActivationTimeout > TimeSpan.Zero, $"{TenantCreationSettings.Section}:ActivationTimeout must be positive.")
            .Validate(settings => settings.InvitationEmailTimeout > TimeSpan.Zero, $"{TenantCreationSettings.Section}:InvitationEmailTimeout must be positive.")
            .Validate(settings => settings.CancellationTimeout > TimeSpan.Zero, $"{TenantCreationSettings.Section}:CancellationTimeout must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantCreationSettings>>().Value);
        services.AddSingleton<TenantCreationAlarm>();

        // Wolverine's generated code cannot build the identity provider, a typed HTTP client that only the container builds, so it
        // resolves it from the message's scope. It reaches no DbContext.
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IIdentityProvider>());

        services.AddOptions<ClerkOptions>()
            .BindConfiguration(ClerkOptions.Section)
            .Validate(clerk => !string.IsNullOrWhiteSpace(clerk.SecretKey), $"{ClerkOptions.Section}:SecretKey must be the secret key of the Clerk instance.")
            .Validate(clerk => clerk.BackendApiUrl.IsAbsoluteUri, $"{ClerkOptions.Section}:BackendApiUrl must be an absolute URL.")
            .Validate(clerk => clerk.Timeout > TimeSpan.Zero, $"{ClerkOptions.Section}:Timeout must be positive.")
            .Validate(
                clerk => !servesTheApi || ClerkIdentityProviderWebhook.IsSigningSecret(clerk.WebhookSigningSecret),
                $"{ClerkOptions.Section}:WebhookSigningSecret must be the signing secret of the Clerk webhook endpoint, which starts with whsec_.")
            .ValidateOnStart();
        services.AddSingleton<IIdentityProviderWebhook, ClerkIdentityProviderWebhook>();
        services.AddHttpClient<IIdentityProvider, ClerkIdentityProvider>((provider, http) =>
        {
            var clerk = provider.GetRequiredService<IOptions<ClerkOptions>>().Value;
            http.BaseAddress = clerk.BackendApiUrl;
            http.DefaultRequestHeaders.Authorization = new("Bearer", clerk.SecretKey);
            http.Timeout = clerk.Timeout;
        });

        return services;
    }
}
