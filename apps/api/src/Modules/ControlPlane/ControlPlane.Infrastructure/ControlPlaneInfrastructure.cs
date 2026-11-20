using System.Net.Mail;
using ControlPlane.Application.DataExports;
using ControlPlane.Application.FeatureFlags;
using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Plans;
using ControlPlane.Application.Roles;
using ControlPlane.Application.SystemAdmins;
using ControlPlane.Application.TermsVersions;
using ControlPlane.Application.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantPurge;
using ControlPlane.Application.Usage;
using ControlPlane.Application.Users;
using ControlPlane.Contracts.FeatureFlags;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Members;
using ControlPlane.Contracts.Plans;
using ControlPlane.Contracts.SystemAdmins;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.TermsVersions;
using ControlPlane.Contracts.Usage;
using ControlPlane.Contracts.Users;
using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Infrastructure.Clerk;
using ControlPlane.Infrastructure.FeatureFlags;
using ControlPlane.Infrastructure.Persistence;
using ControlPlane.Infrastructure.Persistence.DataExports;
using ControlPlane.Infrastructure.Persistence.FeatureFlags;
using ControlPlane.Infrastructure.Persistence.Invitations;
using ControlPlane.Infrastructure.Persistence.Members;
using ControlPlane.Infrastructure.Persistence.Plans;
using ControlPlane.Infrastructure.Persistence.Roles;
using ControlPlane.Infrastructure.Persistence.SystemAdmins;
using ControlPlane.Infrastructure.Persistence.Tenants;
using ControlPlane.Infrastructure.Persistence.TermsVersions;
using ControlPlane.Infrastructure.Persistence.Usage;
using ControlPlane.Infrastructure.Persistence.Users;
using ControlPlane.Infrastructure.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SharedKernel;
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
        services.AddScoped<IEntitlementDirectory, EntitlementDirectory>();
        services.AddScoped<IUsageMeter, UsageMeter>();

        // Registered by type, so Wolverine builds it in Audit's handler that names a page of an audit log, on this module's
        // DbContext and in its transaction (W1).
        services.AddScoped<IUserDirectory, UserDirectory>();
        services.AddScoped<IInvitationDirectory, InvitationDirectory>();
        services.AddScoped<IMemberDirectory, MemberDirectory>();
        services.AddScoped<IPlanDirectory, PlanDirectory>();
        services.AddScoped<ITenantNames, TenantNames>();
        services.AddScoped<ISystemAdminDirectory, SystemAdminDirectory>();
        services.AddScoped<IAuditRetentionDirectory, AuditRetentionDirectory>();
        services.AddScoped<ITermsVersionDirectory, TermsVersionDirectory>();
        services.AddScoped<ITermsGate, TermsGateCheck>();

        // docs/flows/system-admins.md: the list must name at least one address, so the platform always has a way in. Each item is an
        // address in the form the validators accept, and the same text as .NET reads it, so no item is left out of the list's one form.
        services.AddOptions<SystemAdminOptions>()
            .BindConfiguration(SystemAdminOptions.Section)
            .Validate(settings => settings.RootEmails.Count > 0, $"{SystemAdminOptions.Section}:RootEmails must list the email address of at least one root system admin.")
            .Validate(settings => settings.RootEmails.All(IsEmailAddress), $"{SystemAdminOptions.Section}:RootEmails must list only email addresses.")
            .Validate(
                settings => settings.ReverificationWindow >= SystemAdminOptions.MinReverificationWindow && settings.ReverificationWindow <= SystemAdminOptions.MaxReverificationWindow,
                $"{SystemAdminOptions.Section}:ReverificationWindow must be from {SystemAdminOptions.MinReverificationWindow} to {SystemAdminOptions.MaxReverificationWindow}.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SystemAdminOptions>>().Value);

        // Registered by type, so Wolverine builds them in its generated code on the handler's own DbContext, and sees that the
        // handler needs its transaction (W1).
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPlanRepository, PlanRepository>();
        services.AddScoped<IMeterUsageRepository, MeterUsageRepository>();
        services.AddScoped<ISpendingCapRepository, SpendingCapRepository>();
        services.AddScoped<IUsageQueries, UsageQueries>();
        services.AddScoped<ITenantQueries, TenantQueries>();
        services.AddScoped<IMemberQueries, MemberQueries>();
        services.AddScoped<IInvitationQueries, InvitationQueries>();
        services.AddScoped<IRoleQueries, RoleQueries>();
        services.AddScoped<IPlanQueries, PlanQueries>();
        services.AddScoped<IUserQueries, UserQueries>();
        services.AddScoped<ITermsVersionRepository, TermsVersionRepository>();
        services.AddScoped<ITermsVersionQueries, TermsVersionQueries>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();
        services.AddScoped<IFeatureFlagRepository, FeatureFlagRepository>();
        services.AddScoped<IFeatureFlagQueries, FeatureFlagQueries>();

        // The one evaluation of a flag (docs/flows/feature-flags.md). A handler of any module may ask it, so Wolverine takes it from the
        // message's scope and builds no ControlPlane DbContext into another module's handler (W1). The catalog is the code's.
        services.AddScoped<IFeatureFlags, FeatureFlagEvaluation>();
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IFeatureFlags>());
        services.AddSingleton(FeatureFlagCode.Template);
        services.AddSingleton(MeterCode.Template);
        services.AddOptions<BillingOptions>()
            .BindConfiguration(BillingOptions.Section)
            .Validate(
                settings => settings.Currency is { Length: 3 } currency && currency.All(char.IsAsciiLetterUpper),
                $"{BillingOptions.Section}:Currency must be an ISO 4217 code in capitals, such as EUR.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<BillingOptions>>().Value);
        services.AddOptions<FeatureFlagOptions>().BindConfiguration(FeatureFlagOptions.Section).ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<FeatureFlagOptions>>().Value);

        services.AddScoped<IDataExportQueries, DataExportQueries>();

        // The module's part of a person's data export (docs/flows/personal-data-export.md). The export takes every exporter from the
        // message's scope, so Wolverine's generated code builds none of them on a DbContext of the handler, which has no transaction.
        services.AddScoped<IPersonalDataExporter, UserDataExporter>();
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IEnumerable<IPersonalDataExporter>>());

        // The invitation email asks before each try whether its invitation is still pending (W1). Wolverine takes the check from the
        // message's scope, so its generated code builds no DbContext for the email handler, which has no transaction.
        services.AddScoped<IPendingInvitationCheck, PendingInvitationCheck>();
        services.ConfigureWolverine(options => options.CodeGeneration.AlwaysUseServiceLocationFor<IPendingInvitationCheck>());


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
            .Validate(settings => settings.PurgeWarningLead > TimeSpan.Zero, $"{TenantOptions.Section}:PurgeWarningLead must be positive.")
            .Validate(
                settings => settings.PurgeWarningLead < settings.ReopenPeriod,
                $"{TenantOptions.Section}:PurgeWarningLead must be shorter than {TenantOptions.Section}:ReopenPeriod: the warning comes after the closure.")
            .Validate(settings => settings.PurgeTimeout > TimeSpan.Zero, $"{TenantOptions.Section}:PurgeTimeout must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<TenantOptions>>().Value);

        services.AddOptions<SignUpOptions>()
            .BindConfiguration(SignUpOptions.Section)
            .Validate(settings => settings.TenantLimit > 0, $"{SignUpOptions.Section}:TenantLimit must be positive.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<SignUpOptions>>().Value);
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

    // The sync and the weekly cleanup of the feature flags, which the host adds where the background work runs (W5).
    public static IServiceCollection AddFeatureFlagJob(this IServiceCollection services) =>
        services.AddHostedService<FeatureFlagJob>();

    // An address that FluentValidation's EmailAddress accepts (EmailAddress.TryCreate), which .NET reads as the same text, so no
    // display name or comment hides in it.
    private static bool IsEmailAddress(string text) =>
        MailAddress.TryCreate(text, out var address) && address.Address == text && EmailAddress.TryCreate(text) is not null;
}
