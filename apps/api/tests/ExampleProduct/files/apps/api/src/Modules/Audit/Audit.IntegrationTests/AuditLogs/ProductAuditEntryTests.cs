using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.RecordAuditEntry;
using Audit.Contracts.AuditLogs;
using Audit.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.AuditLogs;

// The example product records project.created through RecordAuditEntryCommand (X8): the tenant's own log keeps one entry by the member,
// with the tenant as its target and no details (X6).
public sealed class ProductAuditEntryTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordAuditEntry_AProductsOperation_WritesTheTenantEntryByTheMember()
    {
        var command = new RecordAuditEntryCommand(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), ProductAuditOperations.ProjectCreated);

        await InTenant.RunAsync(database.Services, command.TenantId, scope =>
            RecordAuditEntryHandler.HandleAsync(command, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, command.TenantId, command.EventId))
            .ShouldBe($"project.created|User|{command.ActorId}|Tenant|{command.TenantId}|-");
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, command.TenantId, command.EventId)).ShouldBeNull();
    }

    // A command may arrive twice; its id, chosen by its sender, is the entry's id, so the log holds it once.
    [Fact]
    public async Task RecordAuditEntry_CommandArrivesTwice_WritesOneEntry()
    {
        var command = new RecordAuditEntryCommand(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), ProductAuditOperations.ProjectCreated);

        await InTenant.RunAsync(database.Services, command.TenantId, scope =>
            RecordAuditEntryHandler.HandleAsync(command, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, command.TenantId, scope =>
            RecordAuditEntryHandler.HandleAsync(command, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, command.TenantId, command.EventId)).ShouldBe(1);
    }
}
