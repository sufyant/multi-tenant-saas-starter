using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.StartTenantPurge;
using ControlPlane.Application.Tenants.WarnTenantPurge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Tenancy;
using Wolverine;

namespace Api.IntegrationTests.Platform.Tenants;

// docs/flows/tenant-purge.md: when a closed tenant's reopen period is over, the purge deletes every module's data of the tenant, keeps the
// tenant row as its tombstone and the platform's log, writes tenant.purged there, and deletes the users it left without a tenant. Each
// test runs on a database of its own: the purge schedules its timeout, and a host of another test would run it (section 9, rule 10).
[FixedClock("Each test runs on a database of its own, so no host of another test runs what its clock schedules.")]
public sealed class TenantPurgeTests(Database database) : IAsyncLifetime
{
    private static readonly DateTimeOffset ClosedAt = new(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

    // The module's clock at the warning: the closure plus the reopen period, 30 days, less the warning's lead, 7 days.
    private readonly FakeTimeProvider _time = new(ClosedAt + TimeSpan.FromDays(23));

    private string _databaseName = null!;

    private TestData _testData = null!;

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _databaseName = await database.CreateMigratedDatabaseAsync();
        _testData = new TestData(database, _databaseName);
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, _databaseName), time: _time);
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task PurgeTenant_AClosedTenantAtItsClosure_DeletesEveryModulesDataAndKeepsTheTombstoneAndThePlatformsLog()
    {
        var tenant = await ClosedTenantWithDataAsync("Gone Ltd");
        var kept = await ClosedTenantWithDataAsync("Kept Ltd");

        await PurgeAsync(tenant.Id, ClosedAt);

        (await RowsOfAsync(tenant.Id)).ShouldBe("memberships 0, invitations 0, roles 0, role_permissions 0, tenant_summaries 0, entitlement_overrides 0, entries 0, notifications 0");
        (await RowsOfAsync(kept.Id)).ShouldBe("memberships 2, invitations 1, roles 4, role_permissions 2, tenant_summaries 1, entitlement_overrides 1, entries 1, notifications 2");
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

    // A8: a purge deletes a user through the user deletion path, so a tenant the user left before loses the user's address too.
    [Fact]
    public async Task PurgeTenant_AMemberLeftWithoutATenantWhoLeftAnotherTenantBefore_ErasesTheirAddressThere()
    {
        var tenant = await ClosedTenantWithDataAsync("Gone Ltd");
        var left = await _testData.AddTenantAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        var alone = await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: email));
        await _testData.AddInvitationAsync(left.Id, email, status: "Accepted", acceptedBy: alone);
        await _testData.AddInvitationAsync(left.Id, email);
        await _testData.SummarizeAsync(left.Id);

        await PurgeAsync(tenant.Id, ClosedAt);
        await Waiting.UntilAsync(async () => await InvitationsOfAsync(left.Id) == "Accepted:-, Cancelled:-");

        (await InvitationsOfAsync(left.Id)).ShouldBe("Accepted:-, Cancelled:-");
        (await _testData.UserOfAsync(alone, _databaseName)).Deleted.ShouldBeTrue();
    }

    // The scheduled purge of an earlier closure, or of a reopened tenant, does nothing (K4).
    [Fact]
    public async Task PurgeTenant_ClosedAgainAtAnotherMoment_DoesNothing()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");

        await PurgeAsync(tenant.Id, ClosedAt - TimeSpan.FromDays(3));

        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
        (await RowsOfAsync(tenant.Id)).ShouldBe("memberships 2, invitations 1, roles 4, role_permissions 2, tenant_summaries 1, entitlement_overrides 1, entries 1, notifications 2");
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

    // The warning reaches every member whose role holds tenant.owners.manage, each in their own language, with the moment of the purge:
    // the closure plus the reopen period, in the Owner's own time zone, or in UTC without one. A member who is no Owner gets none. The
    // tenant's log records it, by the platform.
    [Fact]
    public async Task WarnTenantPurge_AClosedTenantAtItsClosure_EmailsEachOwnerInTheirLanguageAndRecordsItInTheTenantsLog()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");
        var turkish = await _testData.AddUserAsync(email: "ayse@acme.test", language: "tr", timeZone: "Europe/Istanbul");
        var arabic = await _testData.AddUserAsync(email: "layla@acme.test", language: "ar");
        await _testData.AddMemberAsync(tenant.Id, turkish, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, arabic, role: "Owner");
        await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: "member@acme.test"));

        await WarnAsync(tenant.Id, ClosedAt);

        var emails = _api.Email.Sent.Where(email => email.To.EndsWith("@acme.test", StringComparison.Ordinal)).OrderBy(email => email.To).ToList();
        emails.Select(email => (email.To, email.Subject)).ShouldBe(
        [
            ("ayse@acme.test", "Acme Ltd 1 Eki 2026 tarihinde silinecek"),
            ("layla@acme.test", "ستُحذف Acme Ltd في 1 أكتوبر 2026"),
        ]);
        emails[0].Text.ShouldContain("1 Eki 2026, saat 12:00 Istanbul (UTC+3) itibarıyla");
        emails[1].Text.ShouldContain("في 1 أكتوبر 2026 الساعة 09:00 بالتوقيت العالمي (UTC)");
        (await TenantEntriesAsync(tenant.Id, "tenant.purge_warned")).ShouldBe(1L);
        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Closed");
    }

    // A warning that arrives twice records one entry and sends each email under the same key (O4).
    [Fact]
    public async Task WarnTenantPurge_ArrivesTwice_RecordsOnceAndEmailsEachOwnerOnce()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");
        await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: "ada@acme.test"), role: "Owner");

        await WarnAsync(tenant.Id, ClosedAt);
        await WarnAsync(tenant.Id, ClosedAt);

        _api.Email.Sent.Count(email => email.To == "ada@acme.test").ShouldBe(1);
        (await TenantEntriesAsync(tenant.Id, "tenant.purge_warned")).ShouldBe(1L);
        (await PlatformEntriesAsync(tenant.Id, "tenant.purge_warned")).ShouldBe(1L);
    }

    // The platform's Activity of the tenant shows the warning too (step r12): Audit writes it in both logs from one event, by the platform,
    // with the tenant as the target, so the platform's log filtered by the tenant finds it. Both entries name the moment of the deletion.
    [Fact]
    public async Task WarnTenantPurge_AClosedTenantAtItsClosure_RecordsItInBothLogsWithTheMomentOfThePurge()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");

        await WarnAsync(tenant.Id, ClosedAt);

        (await PlatformLogAsync(tenant.Id)).ShouldBe($"tenant.closed|User, tenant.purge_warned|Platform|-|Tenant|{tenant.Id}|Acme Ltd");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT details::text FROM audit.platform_entries WHERE target_id = '{tenant.Id}' AND operation = 'tenant.purge_warned'", _databaseName))
            .ShouldBe("""{"purgesAt": "2026-10-01T09:00:00+00:00"}""");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT details::text FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.purge_warned'", _databaseName))
            .ShouldBe("""{"purgesAt": "2026-10-01T09:00:00+00:00"}""");
    }

    // GetTenant's purgeWarningAt is computed, not stored: the same moment the warning's entries carry (R18), from the same settings.
    [Fact]
    public async Task WarnTenantPurge_AClosedTenant_IsRecordedAtThePurgeWarningAtOfGetTenant()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        await WarnAsync(tenant.Id, ClosedAt);
        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeWarningAt").GetDateTimeOffset().UtcDateTime.ShouldBe(
            await database.ScalarAsSuperuserAsync<DateTime>(
                $"SELECT occurred_at FROM audit.platform_entries WHERE target_id = '{tenant.Id}' AND operation = 'tenant.purge_warned'", _databaseName));
    }

    // R18: the audit logs are keyed by the moment and the id, so every copy of a warning carries the same moment: the moment the closure
    // scheduled it, not the moment a copy runs. A copy that runs later still records no second entry.
    [Fact]
    public async Task WarnTenantPurge_ArrivesTwiceAtTwoMoments_RecordsOnceAtTheScheduledMoment()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");

        await WarnAsync(tenant.Id, ClosedAt);
        _time.Advance(TimeSpan.FromMinutes(5));
        await WarnAsync(tenant.Id, ClosedAt);

        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT string_agg(to_char(occurred_at AT TIME ZONE 'UTC', 'YYYY-MM-DD HH24:MI:SS.US'), ', ') FROM audit.entries WHERE tenant_id = '{tenant.Id}' AND operation = 'tenant.purge_warned'",
            _databaseName)).ShouldBe("2026-09-24 09:00:00.000000");
        (await PlatformEntriesAsync(tenant.Id, "tenant.purge_warned")).ShouldBe(1L);
    }

    // A reopening cancels no scheduled message: the warning of a reopened tenant, or of an earlier closure, does nothing (K4).
    [Theory]
    [InlineData("Active", 0)]
    [InlineData("Closed", 3)]
    public async Task WarnTenantPurge_ReopenedOrClosedAgainAtAnotherMoment_EmailsNobodyAndRecordsNothing(string status, int daysEarlier)
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");
        await _testData.AddMemberAsync(tenant.Id, await _testData.AddUserAsync(email: "ada@acme.test"), role: "Owner");
        await _testData.SetTenantStatusAsync(tenant.Id, status);

        await WarnAsync(tenant.Id, ClosedAt - TimeSpan.FromDays(daysEarlier));

        _api.Email.Sent.ShouldNotContain(email => email.To == "ada@acme.test");
        (await TenantEntriesAsync(tenant.Id, "tenant.purge_warned")).ShouldBe(0L);
    }

    [Fact]
    public async Task PurgeTenant_ReopenedBeforeThePurge_DoesNothing()
    {
        var tenant = await ClosedTenantWithDataAsync("Acme Ltd");
        await _testData.SetTenantStatusAsync(tenant.Id, "Active");

        await PurgeAsync(tenant.Id, ClosedAt);

        (await _testData.TenantStatusOfAsync(tenant.Id)).ShouldBe("Active");
        (await SagaOfAsync(tenant.Id)).ShouldBeNull();
    }

    private Task<Guid> WarnAsync(Guid tenantId, DateTimeOffset closedAt) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new WarnTenantPurgeCommand(tenantId, closedAt), Cancellation);
            return tenantId;
        });

    private Task<long> TenantEntriesAsync(Guid tenantId, string operation) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}' AND operation = '{operation}'", _databaseName);

    // The purge's start, as the closure scheduled it, and every message it causes.
    private Task<Guid> PurgeAsync(Guid tenantId, DateTimeOffset closedAt) =>
        _api.WaitingForMessagesAsync(async () =>
        {
            using var scope = _api.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().InvokeForTenantAsync(tenantId.ToString(), new StartTenantPurgeCommand(tenantId, closedAt), Cancellation);
            return tenantId;
        });

    // A closed tenant with one of each kind of data: two members, a custom role with two permissions, a cancelled invitation, an override,
    // an entry of its own log and one of the platform's, and two notifications about it, of two users in two bells (U8).
    private async Task<(Guid Id, string Slug)> ClosedTenantWithDataAsync(string name)
    {
        var tenant = await _testData.AddTenantAsync(status: "Closed", name: name, closedAt: ClosedAt, createdAt: new DateTimeOffset(2026, 8, 1, 8, 0, 0, TimeSpan.Zero));
        var role = await _testData.AddCustomRoleAsync(tenant.Id, "Billing", "tenant.profile.read", "tenant.members.read");
        await _testData.AddMemberAsync(tenant.Id, role: "Owner");
        await _testData.AddMemberWithRoleAsync(tenant.Id, role);
        await _testData.AddInvitationAsync(tenant.Id, status: "Cancelled");
        await _testData.SummarizeAsync(tenant.Id);
        var admin = await _testData.UserIdOfAsync(await _testData.AddUserAsync());
        await database.ScalarAsSuperuserAsync<object>(
            $"""
            INSERT INTO control_plane.entitlement_overrides (tenant_id, key, enabled, maximum, unlimited, reason, set_by, set_at)
            VALUES ('{tenant.Id}', 'members.max', NULL, 50, false, 'Seats for the pilot', '{admin}', '{ClosedAt:O}');
            UPDATE control_plane.tenants SET suspension_reason = 'Unpaid invoices' WHERE id = '{tenant.Id}';
            INSERT INTO audit.entries (id, tenant_id, occurred_at, actor_kind, kind, operation, target_kind, target_id, target_name)
            VALUES (gen_random_uuid(), '{tenant.Id}', '{ClosedAt:O}', 'Platform', 'Command', 'tenant.closed', 'Tenant', '{tenant.Id}', '{name}');
            INSERT INTO audit.platform_entries (id, occurred_at, actor_kind, actor_id, kind, operation, target_kind, target_id, target_name)
            VALUES ('{Guid.CreateVersion7(ClosedAt)}', '{ClosedAt:O}', 'User', gen_random_uuid(), 'Command', 'tenant.closed', 'Tenant', '{tenant.Id}', '{name}');
            INSERT INTO notifications.notifications (id, user_id, tenant_id, type, bell, "values", created_at)
            VALUES (gen_random_uuid(), gen_random_uuid(), '{tenant.Id}', 'tenant.plan_changed', 'Tenant', jsonb_build_object('tenantName', '{name}', 'oldPlanName', 'Free', 'newPlanName', 'Pro'), '{ClosedAt:O}'),
                (gen_random_uuid(), '{admin}', '{tenant.Id}', 'tenant.closed', 'Platform', jsonb_build_object('tenantName', '{name}', 'actorName', '{admin}', 'reopenableUntil', '{ClosedAt.AddDays(30):O}'), '{ClosedAt:O}');
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
                'entitlement_overrides ' || (SELECT count(*) FROM control_plane.entitlement_overrides WHERE tenant_id = '{tenantId}'),
                'entries ' || (SELECT count(*) FROM audit.entries WHERE tenant_id = '{tenantId}'),
                'notifications ' || (SELECT count(*) FROM notifications.notifications WHERE tenant_id = '{tenantId}'))
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

    // The tenant's invitations as the superuser reads them, by status and address: "-" for an erased address.
    private Task<string?> InvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(status || ':' || coalesce(email, '-'), ', ' ORDER BY status, email)
            FROM control_plane.invitations WHERE tenant_id = '{tenantId}'
            """,
            _databaseName);

    // "state|the steps it waits for", or null when no purge started.
    private Task<string?> SagaOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT state || '|' || array_to_string(waiting, ',') FROM control_plane.tenant_purge_sagas WHERE id = '{tenantId}'", _databaseName);
}
