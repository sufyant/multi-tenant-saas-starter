using Audit.Application.AuditLogs;
using Audit.Application.Members.RecordOwnerMade;
using ControlPlane.Contracts.Members;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Members;

// The Audit module records the platform's new Owner (Owner recovery) when ControlPlane announces it, in the tenant's transaction: in the
// platform's log as tenant.owner_made by the system admin, about the tenant, and in the tenant's own log as the member's role change by
// the platform, with no person's name in either (K3).
public sealed class RecordOwnerMadeTests(Database database)
{
    private static readonly DateTimeOffset OccurredAt = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordOwnerMade_OwnerMade_WritesTenantOwnerMadeForThePlatformAndTheRoleChangeForTheTenant()
    {
        var made = OwnerMade();

        await InTenant.RunAsync(database.Services, made.TenantId, scope =>
            RecordOwnerMadeHandler.HandleAsync(made, scope.GetRequiredService<IAuditLog>(), Cancellation));

        (await Logs.DescribeAsync(database.Services, Logs.Platform, null, made.EventId))
            .ShouldBe($"tenant.owner_made|User|{made.MadeBy}|Tenant|{made.TenantId}|Acme");
        (await Logs.DetailsAsync(database.Services, Logs.Platform, null, made.EventId)).ShouldBe(
            $$"""{"userId": "{{made.UserId}}", "oldRole": "member", "oldRoleId": "{{made.OldRoleId}}", "oldRoleBuiltIn": true}""");
        (await Logs.DescribeAsync(database.Services, Logs.Tenant, made.TenantId, made.EventId))
            .ShouldBe($"member.role_changed|Platform|-|User|{made.UserId}|-");
        (await Logs.DetailsAsync(database.Services, Logs.Tenant, made.TenantId, made.EventId)).ShouldBe(
            $$"""{"newRole": "owner", "oldRole": "member", "newRoleId": "{{made.NewRoleId}}", "oldRoleId": "{{made.OldRoleId}}", "newRoleBuiltIn": true, "oldRoleBuiltIn": true}""");
    }

    // An event may arrive twice; its id, chosen by its publisher, is the entry's id in each log, so each is written once.
    [Fact]
    public async Task RecordOwnerMade_EventArrivesTwice_WritesOneEntryInEachLog()
    {
        var made = OwnerMade();

        await InTenant.RunAsync(database.Services, made.TenantId, scope =>
            RecordOwnerMadeHandler.HandleAsync(made, scope.GetRequiredService<IAuditLog>(), Cancellation));
        await InTenant.RunAsync(database.Services, made.TenantId, scope =>
            RecordOwnerMadeHandler.HandleAsync(made, scope.GetRequiredService<IAuditLog>(), Cancellation));

        ((await Logs.CountAsync(database.Services, Logs.Platform, null, made.EventId)), (await Logs.CountAsync(database.Services, Logs.Tenant, made.TenantId, made.EventId)))
            .ShouldBe((1, 1));
    }

    private static OwnerMadeEvent OwnerMade() =>
        new(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme", Guid.CreateVersion7(), Guid.CreateVersion7(), "member", Guid.CreateVersion7(), true, "owner", Guid.CreateVersion7());
}
