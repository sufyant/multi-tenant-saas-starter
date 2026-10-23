using System.Net.Mail;
using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Roles;
using ControlPlane.Application.SystemAdmins;
using ControlPlane.Application.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.RaiseTenantPurgeAlarm;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Infrastructure.Clerk;
using ControlPlane.Infrastructure.Persistence;
using ControlPlane.Infrastructure.Persistence.Invitations;
using ControlPlane.Infrastructure.Persistence.Roles;
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
    public static IServiceCollection AddControlPlaneInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<ControlPlaneDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ControlPlaneDbContext.Schema).AddInterceptors(TenantSummaryInterceptor.Instance),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<ControlPlaneDbContext>(ControlPlaneDbContext.Schema);
        services.AddScoped<ITenantDirectory, TenantDirectory>();

        // Registered by type, so Wolverine builds it in Audit's handler that names a page of an audit log, on this module's
        // DbContext and in its transaction (W1).
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<ISystemAdminDirectory, SystemAdminDirectory>();

        // Section 6, "Root system admins": the list must name at least one address, so the platform always has a way in. Each item is an
        // address in the form the validators accept, and the same text as .NET reads it, so no item is left out of the list's one form.
        services.AddOptions<SystemAdminOptions>()
            .BindConfiguration(SystemAdminOptions.Section)
            .Validate(settings => settings.RootEmails.Count > 0, $"{SystemAdminOptions.Section}:RootEmails must list the email address of at least one root system admin.")
            .Validate(settings => settings.RootEmails.All(IsEmailAddress), $"{SystemAdminOptions.Section}:RootEmails must list only email addresses.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SystemAdminOptions>>().Value);

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext, and sees that the
        // handler needs its transaction (W1).
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantQueries, TenantQueries>();
        services.AddScoped<IMemberQueries, MemberQueries>();
        services.AddScoped<IInvitationQueries, InvitationQueries>();
        services.AddScoped<IRoleQueries, RoleQueries>();
        services.AddScoped<IUserQueries, UserQueries>();

        services.AddOptions<RoleOptions>()
            .BindConfiguration(RoleOptions.Section)
            .Validate(settings => settings.MaxCustomRolesPerTenant >= 1, $"{RoleOptions.Section}:MaxCustomRolesPerTenant must be at least 1.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<RoleOptions>>().Value);

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
            .Validate(settings => settings.PurgeTimeout > TimeSpan.Zero, $"{TenantOptions.Section}:PurgeTimeout must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantOptions>>().Value);
        services.AddSingleton<TenantPurgeAlarm>();

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

    // An address that FluentValidation's EmailAddress accepts (EmailAddress.TryCreate), which .NET reads as the same text, so no
    // display name or comment hides in it.
    private static bool IsEmailAddress(string text) =>
        MailAddress.TryCreate(text, out var address) && address.Address == text && EmailAddress.TryCreate(text) is not null;
}
