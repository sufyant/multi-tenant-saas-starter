using System.Text.Json;
using Audit.Application.AuditLogs;
using Audit.Application.Invitations.RecordOwnerInvited;
using Audit.Application.Tenants.RecordTenantClosed;
using Audit.Application.Tenants.RecordTenantRenamed;
using Audit.Application.Tenants.RecordTenantReopened;
using ControlPlane.Contracts.Invitations;
using ControlPlane.Contracts.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Tenants;

// The Audit module records a tenant's new name when ControlPlane announces it, in the tenant's transaction: in the platform's log by
// the system admin, and in the tenant's own log by the platform, with no person in it (K3). Both entries hold the old and the new name.
public sealed class RecordTenantRenamedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    // The target keeps the name of that moment, the new one; the details hold both names in each log.
    [Fact]
    public async Task RecordTenantRenamed_TenantRenamed_WritesBothEntriesWithTheOldAndTheNewName()
    {
        var renamed = new TenantRenamedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme", "Globex");

        await InTenant.RunAsync(database.Services, renamed.TenantId, scope =>
            RecordTenantRenamedHandler.HandleAsync(renamed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, renamed.EventId))
            .ShouldBe($"tenant.renamed|User|{renamed.RenamedBy}|Tenant|{renamed.TenantId}|Globex");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, renamed.TenantId, renamed.EventId))
            .ShouldBe($"tenant.renamed|Platform|-|Tenant|{renamed.TenantId}|Globex");
        (await NamesInAsync(Logs.Platform, null, renamed.EventId)).ShouldBe(("Acme", "Globex"));
        (await NamesInAsync(Logs.Tenant, renamed.TenantId, renamed.EventId)).ShouldBe(("Acme", "Globex"));
    }

    private async Task<(string? Old, string? New)> NamesInAsync(string log, Guid? tenantId, Guid entryId)
    {
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, log, tenantId, entryId))!).RootElement;
        return (details.GetProperty("oldName").GetString(), details.GetProperty("newName").GetString());
    }
}
