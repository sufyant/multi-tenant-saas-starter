using Audit.Application.AuditLogs;
using Audit.Application.Invitations.RecordOwnerInvited;
using Audit.Application.Tenants.RecordTenantRenamed;
using Audit.Application.Tenants.RecordTenantReopened;
using ControlPlane.Contracts.Invitations;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Invitations;

// The Audit module records the platform's Owner invitation when ControlPlane announces it, in the tenant's transaction: in the
// platform's log by the system admin, and in the tenant's own log by the platform, with no person in it (K3).
public sealed class RecordOwnerInvitedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

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
}
