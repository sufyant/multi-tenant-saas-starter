namespace Api.IntegrationTests.Fixtures;

// What is really in the notifications table: the superuser is bound by no policy, so it sees every user's rows (U3).
internal sealed class NotificationRows(Database database)
{
    public static string Row(Guid userId, string type, Guid? tenantId, string bell) =>
        string.Join('|', userId, type, tenantId?.ToString() ?? "-", bell);

    // Each notification of the users, as "user|type|tenant|bell", in the order of the users, then the types; the tenant is "-" for none.
    public async Task<string[]> OfAsync(params Guid[] userIds)
    {
        var rows = await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(concat_ws('|', user_id, type, coalesce(tenant_id::text, '-'), bell), E'\n' ORDER BY user_id, type, created_at)
            FROM notifications.notifications WHERE user_id IN ({string.Join(", ", userIds.Select(id => $"'{id}'"))})
            """);
        return rows?.Split('\n') ?? [];
    }

    // The values a notification of the user keeps, as stored.
    public Task<string?> ValuesOfAsync(Guid userId, string type) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT \"values\"::text FROM notifications.notifications WHERE user_id = '{userId}' AND type = '{type}'");
}
