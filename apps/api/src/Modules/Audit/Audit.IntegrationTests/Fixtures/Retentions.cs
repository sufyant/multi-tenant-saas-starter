using System.Globalization;
using Audit.Application.AuditLogs;
using Audit.Application.Plans.RecordTenantPlanChanged;
using ControlPlane.Contracts.Plans;
using Microsoft.Extensions.DependencyInjection;

namespace Audit.IntegrationTests.Fixtures;

// The tenant's row of the retention (K14) for the tests of the events that start a hold: written and read as the superuser, whom no
// policy binds. A row reads "days|held_from|held_until", with "-" for an empty value, to the minute.
internal static class Retentions
{
    public static readonly DateTimeOffset At = new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    public static string Text(DateTimeOffset moment) => moment.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    public static AuditRetentionShortening Shortening(int? fromDays, int toDays, DateTimeOffset at) => new(fromDays, toDays, at.AddDays(AuditRetention.Grace));

    // A plan change at this moment; with a new period, from 365 days to it.
    public static TenantPlanChangedEvent PlanChanged(Guid tenantId, DateTimeOffset at, int? toDays) =>
        new(
            Guid.CreateVersion7(), at, tenantId, "Acme", Guid.CreateVersion7(), Guid.CreateVersion7(), "Pro", Guid.CreateVersion7(), "Free",
            toDays is { } days ? Shortening(365, days, at) : null);

    public static Task PlanChangedAsync(Database database, Guid tenantId, DateTimeOffset at, int? toDays) =>
        RecordAsync(database, PlanChanged(tenantId, at, toDays));

    public static Task RecordAsync(Database database, TenantPlanChangedEvent changed) =>
        InTenant.RunAsync(database.Services, changed.TenantId, scope =>
            RecordTenantPlanChangedHandler.HandleAsync(
                changed, scope.GetRequiredService<IAuditLog>(), scope.GetRequiredService<IRetentionStore>(), TestContext.Current.CancellationToken));

    public static async Task<Guid> TenantWithRowAsync(Database database, int? days)
    {
        var tenantId = Guid.CreateVersion7();
        await database.ScalarAsSuperuserAsync<object>(
            $"INSERT INTO audit.tenant_retentions (tenant_id, days) VALUES ('{tenantId}', {days?.ToString(CultureInfo.InvariantCulture) ?? "NULL"})");
        return tenantId;
    }

    public static Task<string?> RowAsync(Database database, Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', coalesce(days::text, '-'), coalesce(to_char(held_from AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'),
                coalesce(to_char(held_until AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI'), '-'))
            FROM audit.tenant_retentions WHERE tenant_id = '{tenantId}'
            """);
}
