using Audit.Application.AuditLogs;
using Audit.Infrastructure.Persistence;
using Audit.Infrastructure.Persistence.AuditLogs;
using Audit.Infrastructure.Retention;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;
using Wolverine.EntityFrameworkCore;

namespace Audit.Infrastructure;

/// <summary>
/// The module's infrastructure entry point: the host calls it to register the module's DbContext, adapters and contract
/// implementations.
/// </summary>
public static class AuditInfrastructure
{
    public static IServiceCollection AddAuditInfrastructure(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<AuditDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, AuditDbContext.Schema),
            TenancyServiceCollectionExtensions.MessageSchema);
        services.AddModuleMigrations<AuditDbContext>(AuditDbContext.Schema);

        // Registered by type, so Wolverine builds it in its generated code on the handler's own DbContext (W1).
        services.AddScoped<IAuditLog, AuditLog>();
        services.AddScoped<IRetentionStore, RetentionStore>();

        // K15: a period under the floor stops the start, as the deletion would refuse it (R19).
        services.AddOptions<AuditRetentionOptions>()
            .BindConfiguration(AuditRetentionOptions.Section)
            .Validate(retention => retention.IsValid, $"{AuditRetentionOptions.Section}:Days must be at least {ControlPlane.Contracts.Plans.AuditRetention.Floor}.")
            .ValidateOnStart();

        // The module's part of a person's data export (docs/flows/personal-data-export.md).
        services.AddScoped<IPersonalDataExporter, AuditDataExporter>();

        return services;
    }

    // K14, K15: the daily audit retention, which the host adds where the background work runs (W5).
    public static IServiceCollection AddAuditRetention(this IServiceCollection services) =>
        services.AddHostedService<AuditRetentionJob>();
}
