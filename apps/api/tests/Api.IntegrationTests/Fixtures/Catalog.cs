using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// Writes catalog rows with plain SQL: the host's tests may not see ControlPlane's internals. Built-in roles are found by
// name, the way the catalog stores them. A membership belongs to a tenant, so it is written with its tenant declared. The rows go
// into the main database unless the test names a database of its own.
internal sealed class Catalog(Database database, string databaseName = Database.MainDatabase)
{
    public async Task<(Guid Id, string Slug)> AddTenantAsync(
        string status = "Active",
        string? slug = null,
        string name = "Acme Ltd",
        Guid? tenantId = null,
        string? suspensionReason = null,
        DateTimeOffset? closedAt = null)
    {
        var id = tenantId ?? Guid.NewGuid();
        slug ??= $"tenant-{id:N}"[..20];
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.tenants (id, name, slug, status, suspension_reason, closed_at)
            VALUES ('{id}', '{name}', '{slug}', '{status}', {Text(suspensionReason)}, {Text(closedAt?.ToString("O"))})
            """,
            database: databaseName);

        return (id, slug);
    }

    public async Task<string> AddMemberAsync(Guid tenantId, string? externalId = null, string role = "Member")
    {
        externalId ??= await AddUserAsync();
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.memberships (tenant_id, user_id, role_id)
            SELECT '{tenantId}', users.id, roles.id FROM control_plane.users, control_plane.roles
            WHERE users.external_id = '{externalId}' AND roles.built_in = '{role}'
            """,
            database: databaseName);

        return externalId;
    }

    // A user with an email address and names, each of which may be missing. Without a language the column's default applies: English.
    public async Task<string> AddUserAsync(
        Guid? id = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? language = null,
        string? timeZone = null)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.users (id, external_id, email, first_name, last_name) VALUES ('{id ?? Guid.NewGuid()}', '{externalId}', {Text(email)}, {Text(firstName)}, {Text(lastName)})",
            database: databaseName);
        if (language is not null || timeZone is not null)
        {
            await database.ScalarAsync<object>(
                $"UPDATE control_plane.users SET language = '{language ?? "en"}', time_zone = {Text(timeZone)} WHERE external_id = '{externalId}'",
                database: databaseName);
        }

        return externalId;
    }

    // The catalog's own id of a user, which the API answers with; the identity provider's id is what a token carries.
    public Task<Guid> UserIdOfAsync(string externalId) =>
        database.ScalarAsync<Guid>($"SELECT id FROM control_plane.users WHERE external_id = '{externalId}'", database: databaseName);

    public async Task<string> AddSystemAdminAsync(string? email = null, string? language = null)
    {
        var externalId = await AddUserAsync(email: email, language: language);
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{externalId}'",
            database: databaseName);

        return externalId;
    }

    // Marks the user the way a system admin's deletion does: the email address and the names erased, the time of the deletion set.
    public Task DeleteUserAsync(string externalId) =>
        database.ScalarAsync<object>(
            $"UPDATE control_plane.users SET email = NULL, first_name = NULL, last_name = NULL, deleted_at = now() WHERE external_id = '{externalId}'",
            database: databaseName);

    public Task SuspendUserAsync(string externalId) =>
        database.ScalarAsync<object>($"UPDATE control_plane.users SET suspended_at = now() WHERE external_id = '{externalId}'", database: databaseName);

    // An invitation of the tenant, from a new inviter, with its tenant declared. Its token hash is new, so no link names it.
    public async Task<Guid> AddInvitationAsync(
        Guid tenantId,
        string? email = null,
        string role = "Member",
        string status = "Pending",
        DateTimeOffset? expiresAt = null,
        Guid? id = null)
    {
        var invitationId = id ?? Guid.NewGuid();
        var inviter = await UserIdOfAsync(await AddUserAsync());
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.invitations (id, tenant_id, email, role_id, invited_by, created_at, expires_at, token_hash, status)
            SELECT '{invitationId}', '{tenantId}', '{email ?? $"{Guid.NewGuid():N}@example.com"}', roles.id, '{inviter}', now(),
                '{(expiresAt ?? DateTimeOffset.UtcNow.AddDays(7)):O}', '{Guid.NewGuid():N}{Guid.NewGuid():N}', '{status}'
            FROM control_plane.roles WHERE roles.built_in = '{role}'
            """,
            database: databaseName);

        return invitationId;
    }

    public Task<string?> InvitationStatusOfAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT status FROM control_plane.invitations WHERE id = '{invitationId}'", databaseName);

    public Task<string?> TenantStatusOfAsync(Guid tenantId) =>
        database.ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenantId}'", database: databaseName);

    public Task SetTenantStatusAsync(Guid tenantId, string status) =>
        database.ScalarAsync<object>($"UPDATE control_plane.tenants SET status = '{status}' WHERE id = '{tenantId}'", database: databaseName);

    public Task<long> CountAsync(string sql) => database.ScalarAsync<long>(sql);

    // The language of the invitation to the address; each test invites an address of its own.
    public Task<string?> InvitationLanguageOfAsync(string email) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT language FROM control_plane.invitations WHERE email = '{email}'", databaseName);

    // The row's version: PostgreSQL gives a row a new one on every write, so the same version means nothing wrote the row.
    public Task<string?> UserRowVersionOfAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT xmin::text FROM control_plane.users WHERE external_id = '{externalId}'", databaseName);

    // The catalog's email address, names and preferences of a user, and whether the user is marked deleted.
    public async Task<StoredUser> UserOfAsync(string externalId, string? databaseName = null)
    {
        var json = await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT json_build_object(
                'email', email, 'firstName', first_name, 'lastName', last_name, 'deleted', deleted_at IS NOT NULL, 'language', language,
                'timeZone', time_zone)::text
            FROM control_plane.users WHERE external_id = '{externalId}'
            """,
            databaseName ?? Database.MainDatabase);

        return JsonSerializer.Deserialize<StoredUser>(json!, JsonSerializerOptions.Web)!;
    }

    private static string Text(string? value) => value is null ? "NULL" : $"'{value}'";
}

internal sealed record StoredUser(string? Email, string? FirstName, string? LastName, bool Deleted, string Language = "en", string? TimeZone = null);
