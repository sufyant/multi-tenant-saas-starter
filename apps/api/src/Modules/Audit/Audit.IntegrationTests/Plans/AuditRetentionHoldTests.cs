using Npgsql;

namespace Audit.IntegrationTests.Plans;

// K14, K6o: the tenant's row of the retention holds what a shorter period leaves out. Each event's own hold is in the test class of its
// handler (N9); here, two writers at once and the row's checks.
public sealed class AuditRetentionHoldTests(Database database)
{
    // K6o: each writer locks the row before it reads the hold, so two shortenings at once both count, and the shorter period stays.
    [Fact]
    public async Task RecordTwoShorteningsAtOnce_InOneTenant_KeepsBoth()
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);

        await Task.WhenAll(
            Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 90),
            Retentions.PlanChangedAsync(database, tenantId, Retentions.At, toDays: 60));

        (await Retentions.RowAsync(database, tenantId)).ShouldBe($"60|{Retentions.Text(Retentions.At.AddDays(-365))}|{Retentions.Text(Retentions.At.AddDays(30))}");
    }

    // R15: a period under the floor and a hold's start without its end are refused by the database too.
    [Theory]
    [InlineData("days = 29", "ck_tenant_retentions_days")]
    [InlineData("held_from = now()", "ck_tenant_retentions_hold")]
    public async Task WriteRow_BreakingARetentionCheck_IsRefusedByThatCheck(string change, string check)
    {
        var tenantId = await Retentions.TenantWithRowAsync(database, days: 365);

        var write = () => database.ScalarAsSuperuserAsync<object>($"UPDATE audit.tenant_retentions SET {change} WHERE tenant_id = '{tenantId}'");

        (await write.ShouldThrowAsync<PostgresException>()).ConstraintName.ShouldBe(check);
    }
}
