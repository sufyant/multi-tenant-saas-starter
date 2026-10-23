using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// Writes ControlPlane's rows with plain SQL: the host's tests may not see ControlPlane's internals. Built-in roles are found by
// name, the way ControlPlane stores them. A membership belongs to a tenant, so it is written with its tenant declared. The rows go
// into the main database unless the test names a database of its own.
internal sealed class TestData(Database database, string databaseName = Database.MainDatabase)
{
    // The invitation code of a link: the tenant id, a dot and the secret (section 6).
    public static string InvitationCodeOf(Guid tenantId, string secret) => $"{tenantId}.{secret}";

    // Every tenant has its summary from its creation on, empty until a command writes it (R12), and its own Owner, Admin and Member roles
    // (A2), written with its tenant declared. A database before the migration GiveEachTenantItsRoles has one role of each kind, which
    // every tenant shares, so a tenant there gets no roles of its own.
    public async Task<(Guid Id, string Slug)> AddTenantAsync(
        string status = "Active",
        string? slug = null,
        string name = "Acme Ltd",
        Guid? tenantId = null,
        string? suspensionReason = null,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? createdAt = null)
    {
        var tenant = await AddTenantRowAsync(status, slug, name, tenantId, suspensionReason, closedAt, createdAt);
        await database.ScalarAsync<object>($"INSERT INTO control_plane.tenant_summaries (tenant_id) VALUES ('{tenant.Id}')", database: databaseName);
        if (await HasRolesOfEachTenantAsync())
        {
            await database.ExecuteInTenantAsync(
                tenant.Id,
                $"""
                INSERT INTO control_plane.roles (id, tenant_id, name, built_in)
                SELECT gen_random_uuid(), '{tenant.Id}', lower(built_in), built_in FROM unnest(ARRAY['Owner', 'Admin', 'Member']) AS built_in
                """,
                database: databaseName);
        }

        return tenant;
    }

    // A custom role of the tenant with these permissions (A3), written with its tenant declared, as CreateRole writes it.
    public async Task<Guid> AddCustomRoleAsync(Guid tenantId, string name, params string[] permissions)
    {
        var roleId = Guid.NewGuid();
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.roles (id, tenant_id, name) VALUES ('{roleId}', '{tenantId}', '{name}');
            INSERT INTO control_plane.role_permissions (tenant_id, role_id, permission)
            SELECT '{tenantId}', '{roleId}', permission FROM unnest(ARRAY[{string.Join(", ", permissions.Select(permission => $"'{permission}'"))}]::text[]) AS permission;
            """,
            database: databaseName);
        return roleId;
    }

    // The id of the tenant's built-in role of this kind (Owner, Admin, Member).
    public Task<Guid> RoleIdOfAsync(Guid tenantId, string builtIn) =>
        database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.roles WHERE tenant_id = '{tenantId}' AND built_in = '{builtIn}'", databaseName);

    // A member of the tenant with the role of this id, a custom role included; its user's count follows, as AddMemberAsync keeps it.
    public async Task<string> AddMemberWithRoleAsync(Guid tenantId, Guid roleId, string? externalId = null)
    {
        externalId ??= await AddUserAsync();
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.memberships (tenant_id, user_id, role_id, joined_at)
            SELECT '{tenantId}', users.id, '{roleId}', now() FROM control_plane.users WHERE users.external_id = '{externalId}';
            UPDATE control_plane.users SET membership_count = membership_count + 1 WHERE external_id = '{externalId}';
            """,
            database: databaseName);

        return externalId;
    }

    // The tenant row alone, as before the summary had its own table: for a test of the migrations before MoveTenantSummary.
    public async Task<(Guid Id, string Slug)> AddTenantRowAsync(
        string status = "Active",
        string? slug = null,
        string name = "Acme Ltd",
        Guid? tenantId = null,
        string? suspensionReason = null,
        DateTimeOffset? closedAt = null,
        DateTimeOffset? createdAt = null)
    {
        var id = tenantId ?? Guid.NewGuid();
        // Not from the id: two version 7 ids of one millisecond start with the same 13 digits, and so would their slugs.
        slug ??= $"tenant-{Guid.NewGuid():N}"[..20];
        // A purged tenant is a tombstone with the moment of its purge (R15).
        DateTimeOffset? purgedAt = status == "Purged" ? DateTimeOffset.UtcNow : null;
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.tenants (id, name, slug, status, suspension_reason, closed_at, created_at{Column("purged_at", purgedAt)})
            VALUES ('{id}', '{name}', '{slug}', '{status}', {Text(suspensionReason)}, {Text(closedAt?.ToString("O"))}, {Moment(createdAt)}{Value(purgedAt)})
            """,
            database: databaseName);

        return (id, slug);
    }

    // Without a moment, the member joins at the insert. The user's membership count follows, as the commands keep it, so a command that
    // removes the membership later counts down from a true value.
    public async Task<string> AddMemberAsync(Guid tenantId, string? externalId = null, string role = "Member", DateTimeOffset? joinedAt = null)
    {
        externalId ??= await AddUserAsync();
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.memberships (tenant_id, user_id, role_id, joined_at)
            SELECT '{tenantId}', users.id, roles.id, {Moment(joinedAt)}
            FROM control_plane.users, control_plane.roles
            WHERE users.external_id = '{externalId}' AND roles.built_in = '{role}';
            UPDATE control_plane.users SET membership_count = membership_count + 1 WHERE external_id = '{externalId}';
            """,
            database: databaseName);

        return externalId;
    }

    // Gives the tenant the summary of its own rows (R12), as a command writes it, for a test that adds its members with plain SQL. The
    // superuser counts every membership of the tenant.
    public Task SummarizeAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<object>(
            $"""
            WITH owners AS (
                SELECT memberships.* FROM control_plane.memberships JOIN control_plane.roles ON roles.id = memberships.role_id
                WHERE memberships.tenant_id = '{tenantId}' AND roles.built_in = 'Owner')
            UPDATE control_plane.tenant_summaries SET
                member_count = (SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'),
                owner_count = (SELECT count(*) FROM owners)
            WHERE tenant_id = '{tenantId}'
            """,
            databaseName);

    // A user with an email address and names, each of which may be missing. Without a language, English.
    public async Task<string> AddUserAsync(
        Guid? id = null,
        string? email = null,
        string? firstName = null,
        string? lastName = null,
        string? language = null,
        string? timeZone = null,
        DateTimeOffset? createdAt = null)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        await database.ScalarAsync<object>(
            $"""
            INSERT INTO control_plane.users (id, external_id, email, first_name, last_name, language{Column("created_at", createdAt)})
            VALUES ('{id ?? Guid.NewGuid()}', '{externalId}', {Text(email)}, {Text(firstName)}, {Text(lastName)}, 'en'{Value(createdAt)})
            """,
            database: databaseName);
        if (language is not null || timeZone is not null)
        {
            await database.ScalarAsync<object>(
                $"UPDATE control_plane.users SET language = '{language ?? "en"}', time_zone = {Text(timeZone)} WHERE external_id = '{externalId}'",
                database: databaseName);
        }

        return externalId;
    }

    // Our own id of a user, which the API answers with; the identity provider's id is what a token carries.
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

    // Marks the user the way a system admin's deletion does: the email address, the names and the time zone erased, the time of the
    // deletion set.
    public Task DeleteUserAsync(string externalId) =>
        database.ScalarAsync<object>(
            $"UPDATE control_plane.users SET email = NULL, first_name = NULL, last_name = NULL, time_zone = NULL, deleted_at = now() WHERE external_id = '{externalId}'",
            database: databaseName);

    // A suspension names the system admin who suspended the user (R15). The user stands in for them: the key needs only a user.
    public Task SuspendUserAsync(string externalId) =>
        database.ScalarAsync<object>(
            $"UPDATE control_plane.users SET suspended_at = now(), suspended_by = id WHERE external_id = '{externalId}'", database: databaseName);

    // A user's membership count as the user row keeps it (users.membership_count), and the user's memberships counted as the superuser,
    // whom row level security does not hold. After a command that adds or removes a membership, the two must be the same.
    public Task<long> StoredMembershipCountOfAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT membership_count::bigint FROM control_plane.users WHERE external_id = '{externalId}'", databaseName);

    public Task<long> CountedMembershipsOfAsync(string externalId) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.memberships JOIN control_plane.users ON users.id = memberships.user_id WHERE users.external_id = '{externalId}'",
            databaseName);

    // An invitation of the tenant, with its tenant declared, from the inviter or a new one. Without a secret its token hash is new, so no
    // link names it; with one, the link's code is the tenant id and the secret (InvitationCodeOf). With an accepting user, it was accepted
    // by them at the insert. An accepted invitation always has the user who accepted it (R15): a new one, unless the test names them.
    public async Task<Guid> AddInvitationAsync(
        Guid tenantId,
        string? email = null,
        string role = "Member",
        string status = "Pending",
        DateTimeOffset? expiresAt = null,
        Guid? id = null,
        string? invitedBy = null,
        string? secret = null,
        string language = "en",
        Guid? roleId = null,
        string? acceptedBy = null)
    {
        var invitationId = id ?? Guid.NewGuid();
        var inviter = await UserIdOfAsync(invitedBy ?? await AddUserAsync());
        acceptedBy ??= status == "Accepted" ? await AddUserAsync() : null;
        var accepter = acceptedBy is null ? "NULL" : $"'{await UserIdOfAsync(acceptedBy)}'";
        var tokenHash = secret is null ? $"{Guid.NewGuid():N}{Guid.NewGuid():N}" : Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));
        await database.ExecuteInTenantAsync(
            tenantId,
            $"""
            INSERT INTO control_plane.invitations (id, tenant_id, email, role_id, invited_by, created_at, expires_at, secret_hash, status, language, accepted_by, accepted_at)
            SELECT '{invitationId}', '{tenantId}', '{email ?? $"{Guid.NewGuid():N}@example.com"}', roles.id, '{inviter}', now(),
                '{(expiresAt ?? DateTimeOffset.UtcNow.AddDays(7)):O}', '{tokenHash}', '{status}', '{language}', {accepter},
                {(acceptedBy is null ? "NULL" : "now()")}
            FROM control_plane.roles WHERE {(roleId is { } custom ? $"roles.id = '{custom}'" : $"roles.built_in = '{role}'")}
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

    // Our copy of a user's email address, names and preferences, and whether the user is marked deleted.
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

    // A column the test gives a moment for, and its value; without one the column's default applies.
    private static string Column(string name, DateTimeOffset? moment) => moment is null ? string.Empty : $", {name}";

    private static string Value(DateTimeOffset? moment) => moment is { } given ? $", '{given:O}'" : string.Empty;

    // The moment the test gives, else the moment of the insert.
    private static string Moment(DateTimeOffset? moment) => moment is { } given ? $"'{given:O}'" : "now()";

    private async Task<bool> HasRolesOfEachTenantAsync() =>
        await database.ScalarAsync<bool>(
            """
            SELECT EXISTS (SELECT FROM information_schema.columns
                WHERE table_schema = 'control_plane' AND table_name = 'roles' AND column_name = 'tenant_id')
            """,
            database: databaseName);
}

internal sealed record StoredUser(string? Email, string? FirstName, string? LastName, bool Deleted, string Language = "en", string? TimeZone = null);
