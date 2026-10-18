using Audit.Application.AuditLogs;
using Audit.Application.Invitations.RecordInvitationRevoked;
using ControlPlane.Contracts.Invitations;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Invitations;

// The Audit module records an invitation's revocation in the tenant's own log: the member who revoked it, and the invitation by its id
// (K3).
public sealed class RecordInvitationRevokedTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RecordInvitationRevoked_InvitationRevoked_WritesTheTenantEntryByTheRevoker()
    {
        var revoked = new InvitationRevokedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, revoked.TenantId, scope =>
            RecordInvitationRevokedHandler.HandleAsync(revoked, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Tenant, revoked.TenantId, revoked.EventId))
            .ShouldBe($"invitation.revoked|User|{revoked.RevokedBy}|Invitation|{revoked.InvitationId}|-");
        (await Logs.CountAsync(database.Services, Logs.Platform, null, revoked.EventId)).ShouldBe(0);
    }
}
