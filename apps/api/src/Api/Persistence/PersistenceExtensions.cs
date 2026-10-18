using Tenancy;
using Wolverine.EntityFrameworkCore;

namespace Api.Persistence;

internal static class PersistenceExtensions
{
    public static IServiceCollection AddApiPersistence(this IServiceCollection services)
    {
        services.AddDbContextWithWolverineIntegration<ApiDbContext>(
            (provider, options) => options.UseModuleDatabase(provider, ApiDbContext.Schema), TenancyServiceCollectionExtensions.MessageSchema);
        return services.AddModuleMigrations<ApiDbContext>(ApiDbContext.Schema);
    }

    public const string DatabaseConnection = "Database";

    public const string MessagingConnection = "Messaging";

    public static WebApplicationBuilder AddPersistence(this WebApplicationBuilder builder)
    {
        builder.Services.AddOptions<ConnectionStringOptions>()
            .BindConfiguration(ConnectionStringOptions.Section)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Database), $"{ConnectionStringOptions.Section}:{DatabaseConnection} must name the application role's connection.")
            .ValidateOnStart();

        // Both connections are checked on start (DatabaseAccountCheck), which runs before Wolverine starts. A host composed without the
        // setting, as for the migration step, registers no database; it does not start.
        builder.Services.AddHostedService<DatabaseAccountCheck>();
        if (builder.Configuration.GetConnectionString(DatabaseConnection) is { Length: > 0 } database)
        {
            builder.Services.AddTenancy(_ => database);
        }

        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return builder;
    }

    // Wolverine's message store holds session-level advisory locks, so it needs a connection of its own behind a pooler in transaction
    // mode; without one it uses the database connection. The key names the setting the connection came from.
    public static (string Key, string ConnectionString)? MessagingConnectionOf(IConfiguration configuration) =>
        configuration.GetConnectionString(MessagingConnection) is { Length: > 0 } messaging ? ($"{ConnectionStringOptions.Section}:{MessagingConnection}", messaging)
        : configuration.GetConnectionString(DatabaseConnection) is { Length: > 0 } database ? ($"{ConnectionStringOptions.Section}:{DatabaseConnection}", database)
        : null;

    public static string DatabaseKey => $"{ConnectionStringOptions.Section}:{DatabaseConnection}";

    private sealed class ConnectionStringOptions
    {
        public const string Section = "ConnectionStrings";

        public string? Database { get; set; }
    }
}
