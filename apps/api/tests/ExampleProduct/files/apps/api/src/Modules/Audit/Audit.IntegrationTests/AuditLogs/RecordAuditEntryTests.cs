using Audit.Application.AuditLogs;
using Audit.Application.AuditLogs.RecordAuditEntry;
using Audit.Contracts.AuditLogs;
using Audit.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.AuditLogs;

// A product module records its own entry through this command, because Audit references no product contracts (X8). The command takes
// only an operation of the product: a template's operation comes from the template's own events (K3). The example product's copy of
// this file keeps the template's test and adds the entry of its operation project.created (X4).
public sealed class RecordAuditEntryTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 11, 25, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordAuditEntry_ATemplatesOperation_IsRefusedAndWritesNoEntry()
    {
        var command = new RecordAuditEntryCommand(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), AuditOperations.TenantRenamed);

        var record = () => InTenant.RunAsync(database.Services, command.TenantId, scope =>
            RecordAuditEntryHandler.HandleAsync(command, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await record.ShouldThrowAsync<ArgumentException>()).ParamName.ShouldBe("command");
        (await Logs.CountAsync(database.Services, Logs.Tenant, command.TenantId, command.EventId)).ShouldBe(0);
    }

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
