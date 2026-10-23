using ControlPlane.Application.Tenants.StartTenantPurge;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Platform.Tenants;

// Section 6, "Tenant purge": when a closed tenant's reopen period is over, the purge deletes every module's data of the tenant, keeps the
// tenant row as its tombstone and the platform's log, writes tenant.purged there, and deletes the users it left without a tenant. Each
// test runs on a database of its own: the purge schedules its timeout, and a host of another test would run it (section 9, rule 10).
public sealed class TenantPurgeTests(Database database) : IAsyncLifetime
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    private string _databaseName = null!;

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, _databaseName));
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task PurgeTenant_AClosedTenantAtItsClosure_DeletesEveryModulesDataAndKeepsTheTombstoneAndThePlatformsLog()
    {
        var tenant = await ClosedTenantWithDataAsync("Gone Ltd");
        var kept = await ClosedTenantWithDataAsync("Kept Ltd");

        await PurgeAsync(tenant.Id, ClosedAt);

        (await RowsOfAsync(tenant.Id)).ShouldBe("memberships 0, invitations 0, roles 0, role_permissions 0, tenant_summaries 0, entries 0");
        (await RowsOfAsync(kept.Id)).ShouldBe("memberships 2, invitations 1, roles 4, role_permissions 2, tenant_summaries 1, entries 1");
        (await TombstoneOfAsync(tenant.Id)).ShouldBe($"Gone Ltd|{tenant.Slug}|Purged|-|-|t|2026-08-01");
        (await PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.closed|User, tenant.purged|Platform|-|Tenant|{tenant.Id}|Gone Ltd");
        (await SagaOfAsync(tenant.Id)).ShouldBe("Completed|");
    }

    // A user whose only membership was in the purged tenant is deleted through the user deletion path, by the platform: masked, deleted
    // at the identity provider, recorded. A user with another membership stays, and so does a system admin.
    [Fact]
    public async Task PurgeTenant_ItsMembers_DeletesOnlyTheUsersLeftWithoutATenant()
    {
        var tenant = await ClosedTenantWithDataAsync("Gone Ltd");
        var alone = await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        var elsewhere = await _testData.AddMemberAsync(tenant.Id);
        await _testData.AddMemberAsync((await _testData.AddTenantAsync()).Id, elsewhere);
        var admin = await _testData.AddSystemAdminAsync();
        await _testData.AddMemberAsync(tenant.Id, admin);
        var aloneId = await _testData.UserIdOfAsync(alone);

        await PurgeAsync(tenant.Id, ClosedAt);
        await Waiting.UntilAsync(async () => await PlatformEntriesAsync(aloneId, "user.deleted") > 0);

        (await _testData.UserOfAsync(alone, _databaseName)).ShouldSatisfyAllConditions(
            user => user.Deleted.ShouldBeTrue(),
            user => user.Email.ShouldBeNull());
        (await _testData.UserOfAsync(elsewhere, _databaseName)).Deleted.ShouldBeFalse();
        (await _testData.UserOfAsync(admin, _databaseName)).Deleted.ShouldBeFalse();
        (await _testData.StoredMembershipCountOfAsync(elsewhere), await _testData.StoredMembershipCountOfAsync(admin)).ShouldBe((1L, 0L));
        _api.Identity.Deleted.ShouldContain(alone);
        _api.Identity.Deleted.ShouldNotContain(elsewhere);
        _api.Identity.Deleted.ShouldNotContain(admin);
        (await PlatformLogAsync(aloneId)).ShouldBe($"user.deleted|Platform|-|User|{aloneId}|-");
    }

    // The scheduled purge of an earlier closure, or of a reopened tenant, does nothing (K4).
    [Fact]
    public async Task PurgeTenant_ClosedAgainAtAnotherMoment_DoesNothing()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");

        await PurgeAsync(tenant.Id, ClosedAt - TimeSpan.FromDays(3));

        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
        (await RowsOfAsync(tenant.Id)).ShouldBe("memberships 2, invitations 1, roles 4, role_permissions 2, tenant_summaries 1, entries 1");
        (await SagaOfAsync(tenant.Id)).ShouldBeNull();
    }

    // The purge's start may arrive twice (O4): the second finds the tenant purging or purged and does nothing.
    [Fact]
    public async Task PurgeTenant_TheStartArrivesTwice_PurgesOnce()
    {
        var tenant = await ClosedTenantWithDataAsync("Gone Ltd");

        await PurgeAsync(tenant.Id, ClosedAt);
        await PurgeAsync(tenant.Id, ClosedAt);

        (await PlatformEntriesAsync(tenant.Id, "tenant.purged")).ShouldBe(1L);
        (await SagaOfAsync(tenant.Id)).ShouldBe("Completed|");
    }

    // The purge's start, as the closure scheduled it, and every message it causes.
    private Task<Guid> PurgeAsync(Guid tenantId, DateTimeOffset closedAt) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new StartTenantPurgeCommand(tenantId, closedAt), Cancellation);
            return tenantId;
        });

    // A closed tenant with one of each kind of data: two members, a custom role with two permissions, a cancelled invitation, an entry of
    // its own log and one of the platform's.
    private async Task<(Guid Id, string Slug)> ClosedTenantWithDataAsync(string name)
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", name: name, closedAt: ClosedAt, createdAt: new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read", "tenant.members.read");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberWithRoleAsync(tenant.Id, role);
        await _testData.AddInvitationAsync(tenant.Id, status: "Cancelled");
        await _testData.SummarizeAsync(tenant.Id);
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            UPDATE control_plane.tenants SET suspension_reason = 'Unpaid invoices' WHERE id = '{tenant.Id}';
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id, target_name)
            VALUES (gen_random_uuid(), '{tenant.Id}', '{ClosedAt:O}', 'Platform', 'Command', 'tenant.closed', 'Tenant', '{tenant.Id}', '{name}');
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name)
            VALUES ('{Guid.CreateVersion7(ClosedAt)}', '{ClosedAt:O}', 'User', gen_random_uuid(), 'Command', 'tenant.closed', 'Tenant', '{tenant.Id}', '{name}');
            """,
            _databaseName);
        return tenant;
    }

    // Every row of the tenant in each table that holds its data, as the superuser sees them: no policy binds it.
    private Task<string?> RowsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws(', ',
                'memberships ' || (SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'),
                'invitations ' || (SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}'),
                'roles ' || (SELECT count(*) FROM control_plane.roles WHERE tenant_id = '{tenantId}'),
                'role_permissions ' || (SELECT count(*) FROM control_plane.role_permissions WHERE tenant_id = '{tenantId}'),
                'tenant_summaries ' || (SELECT count(*) FROM control_plane.tenant_summaries WHERE tenant_id = '{tenantId}'),
                'entries ' || (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}'))
            """,
            _databaseName);

    // "name|slug|status|suspension reason|closed at|purged at is set|created at", with "-" for an empty value.
    private Task<string?> TombstoneOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', name, slug, status, coalesce(suspension_reason, '-'), coalesce(closed_at::text, '-'), purged_at IS NOT NULL,
                to_char(created_at AT TIME ZONE 'UTC', 'YYYY-MM-DD'))
            FROM control_plane.tenants WHERE id = '{tenantId}'
            """,
            _databaseName);

    // The platform's entries about the target, oldest first: the closure only by its operation and actor kind, the rest in full.
    private Task<string?> PlatformLogAsync(Guid targetId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(
                CASE WHEN operation = 'tenant.closed' THEN operation || '|' || actor_kind
                ELSE concat_ws('|', operation, actor_kind, coalesce(actor_id::text, '-'), target_kind, target_id, coalesce(target_name, '-')) END,
                ', ' ORDER BY occurred_at, id)
            FROM audit.platform_entries WHERE target_id = '{targetId}'
            """,
            _databaseName);

    private Task<long> PlatformEntriesAsync(Guid targetId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.platform_entries WHERE target_id = '{targetId}' AND operation = '{operation}'", _databaseName);

    // "state|the steps it waits for", or null when no purge started.
    private Task<string?> SagaOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT state || '|' || array_to_string(waiting, ',') FROM control_plane.tenant_purge_sagas WHERE id = '{tenantId}'", _databaseName);
}
