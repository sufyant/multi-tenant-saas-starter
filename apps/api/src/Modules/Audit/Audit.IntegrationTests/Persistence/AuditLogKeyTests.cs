using Audit.Application.AuditLogs;
using Audit.Application.Tenants.RecordTenantPurgeWarned;
using Audit.Application.Users.RecordUserSuspended;
using ControlPlane.Contracts.Tenants;
using ControlPlane.Contracts.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Persistence;

// R18, K3: each log is keyed by the entry's moment and then its id, so a later retention can partition by the moment. An event that
// arrives twice still gives one entry: each copy carries the same id and the same moment. PostgreSQL keeps a moment to the microsecond and
// .NET to 100 ns. Npgsql sends the moment to the microsecond, so the second copy's lookup finds the first copy's entry.
public sealed class AuditLogKeyTests(Database database)
{
    // A moment between two microseconds, as a clock gives one.
    private static readonly DateTimeOffset OccurredAt = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero).AddTicks(1_234_567);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RecordEntry_TheTenantsEventWithAMomentBetweenTwoMicrosecondsArrivesTwice_WritesOneEntry()
    {
        var warned = new TenantPurgeWarnedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), "Acme Ltd", OccurredAt.AddDays(7));

        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordTenantPurgeWarnedAsync(scope, warned));
        await InTenant.RunAsync(database.Services, warned.TenantId, scope => RecordTenantPurgeWarnedAsync(scope, warned));

        (await Logs.CountAsync(database.Services, Logs.Tenant, warned.TenantId, warned.EventId)).ShouldBe(1);
    }

    [Fact]
    public async Task RecordEntry_ThePlatformsEventWithAMomentBetweenTwoMicrosecondsArrivesTwice_WritesOneEntry()
    {
        var suspended = new UserSuspendedEvent(Guid.CreateVersion7(), OccurredAt, Guid.CreateVersion7(), Guid.CreateVersion7());

        await InTenant.RunAsync(database.Services, null, scope => RecordUserSuspendedAsync(scope, suspended));
        await InTenant.RunAsync(database.Services, null, scope => RecordUserSuspendedAsync(scope, suspended));

        (await Logs.CountAsync(database.Services, Logs.Platform, null, suspended.EventId)).ShouldBe(1);
    }

    private static Task RecordTenantPurgeWarnedAsync(IServiceProvider scope, TenantPurgeWarnedEvent warned) =>
        RecordTenantPurgeWarnedHandler.HandleAsync(warned, scope.GetRequiredService<IAuditLog>(), Cancellation);

    private static Task RecordUserSuspendedAsync(IServiceProvider scope, UserSuspendedEvent suspended) =>
        RecordUserSuspendedHandler.HandleAsync(suspended, scope.GetRequiredService<IAuditLog>(), Cancellation);
}
