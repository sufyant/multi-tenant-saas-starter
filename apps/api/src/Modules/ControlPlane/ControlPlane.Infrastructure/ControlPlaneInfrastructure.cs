using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using ControlPlane.Infrastructure.Clerk;
using ControlPlane.Infrastructure.Persistence;
using ControlPlane.Infrastructure.Persistence.Invitations;
using ControlPlane.Infrastructure.Persistence.Members;
using ControlPlane.Infrastructure.Persistence.SystemAdmins;
using ControlPlane.Infrastructure.Persistence.Tenants;
using ControlPlane.Infrastructure.Persistence.Users;
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
    /// <summary>The key of the wait for the invitation email after the tenant's activation.</summary>
    public const string InvitationEmailTimeoutKey = $"{TenantCreationOptions.Section}:{nameof(TenantCreationOptions.InvitationEmailTimeout)}";

    /// <summary>The key of the pauses before each new try of a call to the identity provider.</summary>
    public const string IdentityProviderRetryDelaysKey = $"{IdentityProviderOptions.Section}:{nameof(IdentityProviderOptions.RetryDelays)}";

    /// <summary>
    /// The wait for the invitation email, as configured. The host checks it against the retries of both calls before the email
    /// (section 6, "Tenant creation process").
    /// </summary>
    public static TimeSpan InvitationEmailTimeout(IServiceProvider services) =>
        services.GetRequiredService<IOptions<TenantCreationOptions>>().Value.InvitationEmailTimeout;

    /// <summary>The pauses before each new try of a call to the identity provider, as configured or by default.</summary>
    public static IReadOnlyList<TimeSpan> IdentityProviderRetryDelays(IServiceProvider services) =>
        services.GetRequiredService<IOptions<IdentityProviderOptions>>().Value.Delays;

    public static IServiceCollection AddControlPlaneInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<ControlPlaneDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ControlPlaneDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<ControlPlaneDbContext>(ControlPlaneDbContext.Schema);
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        // Registered by type, so Wolverine builds it in Audit's handler that names a page of an audit log, on this module's
        // DbContext and in its transaction (W1).
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ISystemAdminDirectory, SystemAdminDirectory>();
        services.AddOptions<SystemAdminOptions>().BindConfiguration(SystemAdminOptions.Section).ValidateOnStart();
        services.AddSingleton<IValidateOptions<SystemAdminOptions>, SystemAdminOptions.Validation>();

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext, and sees that the
        // handler needs its transaction (W1).
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantQueries, TenantQueries>();
        services.AddScoped<IMemberQueries, MemberQueries>();
        services.AddScoped<IInvitationQueries, InvitationQueries>();
        services.AddScoped<IUserQueries, UserQueries>();

        services.AddOptions<InvitationOptions>()
            .BindConfiguration(InvitationOptions.Section)
            .Validate(settings => settings.AcceptUrl is { IsAbsoluteUri: true }, $"{InvitationOptions.Section}:AcceptUrl must be the absolute URL of the page that accepts invitations.")
            .Validate(
                settings => settings.Lifetime >= TimeSpan.FromDays(1),
                $"{InvitationOptions.Section}:Lifetime must be at least one day: the invitation email says it in whole days.")
            .Validate(settings => settings.DailyLimitPerTenant > 0, $"{InvitationOptions.Section}:DailyLimitPerTenant must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<InvitationOptions>>().Value);

        services.AddOptions<TenantOptions>()
            .BindConfiguration(TenantOptions.Section)
            .Validate(settings => settings.ReopenPeriod > TimeSpan.Zero, $"{TenantOptions.Section}:ReopenPeriod must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantOptions>>().Value);

        services.AddOptions<TenantCreationOptions>()
            .BindConfiguration(TenantCreationOptions.Section)
            .Validate(settings => settings.ActivationTimeout > TimeSpan.Zero, $"{TenantCreationOptions.Section}:ActivationTimeout must be positive.")
            .Validate(settings => settings.InvitationEmailTimeout > TimeSpan.Zero, $"{TenantCreationOptions.Section}:InvitationEmailTimeout must be positive.")
            .Validate(settings => settings.CancellationTimeout > TimeSpan.Zero, $"{TenantCreationOptions.Section}:CancellationTimeout must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantCreationOptions>>().Value);
        services.AddSingleton<TenantCreationAlarm>();

        // Wolverine's generated code cannot build the identity provider, a typed HTTP client that only the container builds, so it
        // resolves it from the message's scope. It reaches no DbContext.
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IIdentityProvider>());

        services.AddOptions<ClerkOptions>()
            .BindConfiguration(ClerkOptions.Section)
            .Validate(clerk => !string.IsNullOrWhiteSpace(clerk.SecretKey), $"{ClerkOptions.Section}:SecretKey must be the secret key of the Clerk instance.")
            .Validate(clerk => clerk.BackendApiUrl.IsAbsoluteUri, $"{ClerkOptions.Section}:BackendApiUrl must be an absolute URL.")
            .Validate(clerk => clerk.Timeout > TimeSpan.Zero, $"{ClerkOptions.Section}:Timeout must be positive.")
            .ValidateOnStart();
        services.AddOptions<IdentityProviderOptions>()
            .BindConfiguration(IdentityProviderOptions.Section)
            .Validate(settings => settings.IsValid, $"{IdentityProviderOptions.Section}:RetryDelays must be positive pauses that grow.")
            .ValidateOnStart();
        services.AddSingleton<IWolverineExtension, IdentityProviderRetries>();
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
