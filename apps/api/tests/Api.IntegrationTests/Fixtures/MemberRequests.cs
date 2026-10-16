using System.Net.Http.Json;
using Npgsql;

namespace Api.IntegrationTests.Fixtures;

internal static class MemberRequests
{
    public static Task<HttpResponseMessage> ChangeMemberRoleAsync(this HttpClient client, Guid tenantId, Guid userId, string role) =>
        client.PatchAsJsonAsync($"/v1/tenants/{tenantId}/members/{userId}", new { role }, TestContext.Current.CancellationToken);

    public static Task<HttpResponseMessage> RemoveMemberAsync(this HttpClient client, Guid tenantId, Guid userId) =>
        client.DeleteAsync($"/v1/tenants/{tenantId}/members/{userId}", TestContext.Current.CancellationToken);

    // Two requests at the same moment (K6). A holder takes the tenant's row and every membership row of the tenant, then sends both
    // requests and lets go only when both wait behind it. With the tenant lock in the handlers, both wait for the tenant row and then run
    // one after the other. Without it, both read the memberships and count the Owners, then wait to write a membership row; both then
    // write on what they read. The answers come in the order of the arguments.
    public static Task<HttpResponseMessage[]> AtOnceAsync(
        this Database database,
        Guid tenantId,
        Func<Task<HttpResponseMessage>> one,
        Func<Task<HttpResponseMessage>> other) =>
        database.WhileHeldAsync(tenantId, one, other, inOrder: false);

    // As AtOnceAsync, but the second request is sent only when the first waits behind the holder. A waiter for a row keeps its place in
    // the queue, so the first request takes the tenant's lock first.
    public static Task<HttpResponseMessage[]> OneAfterTheOtherAsync(
        this Database database,
        Guid tenantId,
        Func<Task<HttpResponseMessage>> first,
        Func<Task<HttpResponseMessage>> second) =>
        database.WhileHeldAsync(tenantId, first, second, inOrder: true);

    private static async Task<HttpResponseMessage[]> WhileHeldAsync(
        this Database database,
        Guid tenantId,
        Func<Task<HttpResponseMessage>> one,
        Func<Task<HttpResponseMessage>> other,
        bool inOrder)
    {
        var cancellation = TestContext.Current.CancellationToken;
        await using var holder = new NpgsqlConnection(database.SuperuserConnectionString);
        await holder.OpenAsync(cancellation);
        await using var holding = await holder.BeginTransactionAsync(cancellation);
        await using var hold = new NpgsqlCommand(
            $"""
            SELECT id FROM control_plane.tenants WHERE id = '{tenantId}' FOR UPDATE;
            SELECT user_id FROM control_plane.memberships WHERE tenant_id = '{tenantId}' FOR UPDATE;
            """,
            holder,
            holding);
        await hold.ExecuteNonQueryAsync(cancellation);

        var first = one();
        if (inOrder)
        {
            await Waiting.UntilAsync(async () => first.IsCompleted || await database.WaitingBehindAsync(holder.ProcessID) == 1);
        }

        var requests = new[] { first, other() };
        await Waiting.UntilAsync(async () => requests.All(request => request.IsCompleted) || await database.WaitingBehindAsync(holder.ProcessID) == 2);
        await holding.CommitAsync(cancellation);

        return await Task.WhenAll(requests);
    }

    // How many sessions wait behind the holder's session: for its lock, or behind a session that waits for it.
    private static Task<long> WaitingBehindAsync(this Database database, int holder) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            WITH RECURSIVE waiting AS (
                SELECT pid FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))
                UNION
                SELECT activity.pid FROM pg_stat_activity AS activity JOIN waiting ON waiting.pid = ANY(pg_blocking_pids(activity.pid))
            )
            SELECT count(*) FROM waiting
            """);
}
