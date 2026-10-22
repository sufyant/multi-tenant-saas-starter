namespace Api.IntegrationTests.Fixtures;

// The summary a tenant row keeps for the platform's list (R12), and the same values counted from the tenant's own rows, as the
// superuser, who row level security does not hold. A test compares the two after a command that changes a membership or an invitation:
// the summary must say what the rows say. Both read as one line: the members, the Owners, and the latest Owner invitation's email,
// expiry, status and language, which the summary keeps only while the tenant has no Owner.
internal sealed class TenantSummaries(Database database, string databaseName = Database.MainDatabase)
{
    public Task<string?> StoredAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', member_count, owner_count, coalesce(owner_invitation_email, '-'),
                coalesce(owner_invitation_expires_at::text, '-'), coalesce(owner_invitation_status, '-'), coalesce(owner_invitation_language, '-'))
            FROM control_plane.tenants WHERE id = '{tenantId}'
            """,
            databaseName);

    public Task<string?> CountedAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            WITH owner AS (SELECT id FROM control_plane.roles WHERE built_in = 'Owner'),
                members AS (SELECT * FROM control_plane.memberships WHERE tenant_id = '{tenantId}'),
                owners AS (SELECT * FROM members WHERE role_id = (SELECT id FROM owner)),
                latest AS (
                    SELECT * FROM control_plane.invitations
                    WHERE tenant_id = '{tenantId}' AND role_id = (SELECT id FROM owner) AND NOT EXISTS (SELECT FROM owners)
                    ORDER BY created_at DESC, id DESC LIMIT 1)
            SELECT concat_ws('|', (SELECT count(*) FROM members), (SELECT count(*) FROM owners),
                coalesce((SELECT email FROM latest), '-'), coalesce((SELECT expires_at::text FROM latest), '-'),
                coalesce((SELECT status FROM latest), '-'), coalesce((SELECT language FROM latest), '-'))
            """,
            databaseName);

    public async Task ShouldMatchTheRowsAsync(Guid tenantId) => (await StoredAsync(tenantId)).ShouldBe(await CountedAsync(tenantId));
}
