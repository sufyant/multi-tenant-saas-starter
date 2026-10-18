using Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;

namespace Audit.IntegrationTests.Fixtures;

// Runs work the way Wolverine runs a handler: the message carries the tenant, the module's DbContext begins the transaction, which
// declares that tenant, and the work is saved and committed unless it throws. A message without a tenant (null) declares nothing, as
// for an event about a catalog user.
internal static class InTenant
{
    public static async Task RunAsync(IServiceProvider services, Guid? tenantId, Func<IServiceProvider, Task> handler)
    {
        await using var scope = services.CreateAsyncScope();
        var audit = Audit(scope, tenantId);
        await using var transaction = await audit.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await handler(scope.ServiceProvider);

        await audit.SaveChangesAsync(TestContext.Current.CancellationToken);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    // Runs SQL as the application role inside the tenant's transaction, or one that declares nothing, so row level security applies.
    public static async Task<T> SqlAsync<T>(IServiceProvider services, Guid? tenantId, Func<NpgsqlCommand, Task<T>> run, string sql)
    {
        await using var scope = services.CreateAsyncScope();
        var audit = Audit(scope, tenantId);
        await using var transaction = await audit.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using var command = new NpgsqlCommand(
            sql, (NpgsqlConnection)audit.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction());

        return await run(command);
    }

    private static AuditDbContext Audit(AsyncServiceScope scope, Guid? tenantId)
    {
        scope.ServiceProvider.GetRequiredService<IMessageContext>().TenantId = tenantId?.ToString();
        return scope.ServiceProvider.GetRequiredService<AuditDbContext>();
    }
}
