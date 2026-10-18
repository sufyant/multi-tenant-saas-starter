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

// The Audit module records a tenant's new name, the platform's Owner invitation, its closure and its reopening when ControlPlane
// announces them, in the tenant's transaction: in the platform's log by the system admin, and in the tenant's own log by the platform,
// with no person in it (K3).
public sealed class RecordTenantLifecycleTests(Database database)
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

    // The target is the tenant, so the platform's Activity tab of the tenant shows it; the details hold the invitations' ids, no address.
    [Fact]
    public async Task RecordOwnerInvited_OwnerInvited_WritesBothEntriesWithTheInvitationAndTheOneItReplaced()
    {
        var invited = new OwnerInvitedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, invited.TenantId, scope =>
            RecordOwnerInvitedHandler.HandleAsync(invited, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, invited.EventId))
            .ShouldBe($"tenant.owner_invited|User|{invited.InvitedBy}|Tenant|{invited.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId))
            .ShouldBe($"tenant.owner_invited|Platform|-|Tenant|{invited.TenantId}|Acme");
        var details = $$"""{"invitationId": "{{invited.InvitationId}}", "replacedInvitationId": "{{invited.ReplacedInvitationId}}"}""";
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, invited.EventId)).ShouldBe(details);
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, invited.TenantId, invited.EventId)).ShouldBe(details);
    }

    [Fact]
    public async Task RecordTenantClosed_TenantClosed_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var closed = new TenantClosedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, closed.EventId))
            .ShouldBe($"tenant.closed|User|{closed.ClosedBy}|Tenant|{closed.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, closed.TenantId, closed.EventId))
            .ShouldBe($"tenant.closed|Platform|-|Tenant|{closed.TenantId}|Acme");
    }

    [Fact]
    public async Task RecordTenantReopened_TenantReopened_WritesThePlatformEntryByTheAdminAndTheTenantEntryByThePlatform()
    {
        var reopened = new TenantReopenedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, reopened.TenantId, scope =>
            RecordTenantReopenedHandler.HandleAsync(reopened, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, reopened.EventId))
            .ShouldBe($"tenant.reopened|User|{reopened.ReopenedBy}|Tenant|{reopened.TenantId}|Acme");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, reopened.TenantId, reopened.EventId))
            .ShouldBe($"tenant.reopened|Platform|-|Tenant|{reopened.TenantId}|Acme");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each log holds it once.
    [Fact]
    public async Task RecordTenantClosed_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var closed = new TenantClosedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), "Acme");

        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, closed.TenantId, scope =>
            RecordTenantClosedHandler.HandleAsync(closed, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.CountAsync(database.Services, Logs.Tenant, closed.TenantId, closed.EventId)).ShouldBe(1);
        (await Logs.CountAsync(database.Services, Logs.Platform, null, closed.EventId)).ShouldBe(1);
    }

    private async Task<(string? Old, string? New)> NamesInAsync(string log, Guid? tenantId, Guid entryId)
    {
        var details = JsonDocument.Parse((await Logs.DetailsAsync(database.Services, log, tenantId, entryId))!).RootElement;
        return (details.GetProperty("oldName").GetString(), details.GetProperty("newName").GetString());
    }
}
