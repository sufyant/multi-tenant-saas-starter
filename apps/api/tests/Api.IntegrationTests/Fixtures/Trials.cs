using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// A running trial of a tenant, as a system admin's trial leaves it (step 40d), written as the superuser, and the trial as the API answers it.
internal static class Trials
{
    public static readonly DateTimeOffset StartedAt = new(2026, 10, 22, 9, 0, 0, TimeSpan.Zero);

    public static readonly DateTimeOffset EndsAt = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    // Puts the tenant in a trial of Business from StartedAt to EndsAt that returns it to the plan.
    public static Task StartAsync(Database database, Guid tenantId, Guid planBefore) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenants SET plan_id = '{SeedPlans.Business}', trial_plan_id = '{SeedPlans.Business}', plan_before_trial_id = '{planBefore}',
                trial_started_at = '{StartedAt:O}', trial_ends_at = '{EndsAt:O}'
            WHERE id = '{tenantId}'
            """);

    public static Task<bool> InTrialAsync(Database database, Guid tenantId) =>
        database.ScalarAsSuperuserAsync<bool>($"SELECT plan_before_trial_id IS NOT NULL FROM control_plane.tenants WHERE id = '{tenantId}'");

    // "plan id|start|end|plan it returns to", the moments in the round-trip format.
    public static string Of(JsonElement trial) =>
        string.Join(
            '|',
            trial.GetProperty("plan").GetProperty("id").GetGuid(),
            trial.GetProperty("startedAt").GetDateTimeOffset().ToString("O"),
            trial.GetProperty("endsAt").GetDateTimeOffset().ToString("O"),
            trial.GetProperty("returnsTo").GetProperty("id").GetGuid());
}
