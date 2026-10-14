using System.Text.Json;

namespace Api.IntegrationTests.Fixtures;

// Writes catalog rows with plain SQL: the host's tests may not see ControlPlane's internals. Built-in roles are found by
// name, the way the catalog stores them. A membership belongs to a tenant, so it is written with its tenant declared.
internal sealed class Catalog(Database database)
{
    public async Task<(Guid Id, string Slug)> AddTenantAsync(string status = "Active", string? slug = null, string name = "Acme Ltd", Guid? tenantId = null)
    {
        var id = tenantId ?? Guid.NewGuid();
        slug ??= $"tenant-{id:N}"[..20];
        await database.ScalarAsync<object>($"INSERT INTO control_plane.tenants (id, name, slug, status) VALUES ('{id}', '{name}', '{slug}', '{status}')");

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
            """);

        return externalId;
    }

    public async Task<string> AddUserAsync(Guid? id = null)
    {
        var externalId = $"user_{Guid.NewGuid():N}";
        await database.ScalarAsync<object>($"INSERT INTO control_plane.users (id, external_id) VALUES ('{id ?? Guid.NewGuid()}', '{externalId}')");

        return externalId;
    }

    // The catalog's own id of a user, which the API answers with; the identity provider's id is what a token carries.
    public Task<Guid> UserIdOfAsync(string externalId) =>
        database.ScalarAsync<Guid>($"SELECT id FROM control_plane.users WHERE external_id = '{externalId}'");

    public async Task<string> AddSystemAdminAsync()
    {
        var externalId = await AddUserAsync();
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{externalId}'");

        return externalId;
    }

    public Task<long> CountAsync(string sql) => database.ScalarAsync<long>(sql);

    // The catalog's copy of a user's profile, and whether the user is marked deleted.
    public async Task<StoredProfile> ProfileOfAsync(string externalId, string? databaseName = null)
    {
        var json = await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT json_build_object(
                'email', email, 'firstName', first_name, 'lastName', last_name, 'updatedAt', profile_updated_at, 'deletedAt', deleted_at)::text
            FROM control_plane.users WHERE external_id = '{externalId}'
            """,
            databaseName ?? Database.MainDatabase);

        return JsonSerializer.Deserialize<StoredProfile>(json!, JsonSerializerOptions.Web)!;
    }
}

internal sealed record StoredProfile(string? Email, string? FirstName, string? LastName, DateTimeOffset? UpdatedAt, DateTimeOffset? DeletedAt);
