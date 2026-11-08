using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform.Tenants;

// GET /v1/platform/tenants/{tenantId}: one tenant, in every status, for the system admin, with its members and Owners counted, every
// Owner in the order they joined, and, while it has no Owner, its latest Owner invitation. The clock starts now, so the messages a tenant
// creation schedules come after the test.
public sealed class GetTenantTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly FakeTimeProvider _time = new(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));

    private ApiFactory _api = null!;

    private PlatformTenants _tenants = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        _tenants = new PlatformTenants(database, _api);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Theory]
    [InlineData("Active", "active")]
    [InlineData("Suspended", "suspended")]
    [InlineData("Closed", "closed")]
    [InlineData("Purging", "purging")]
    [InlineData("Purged", "purged")]
    public async Task GetTenant_ATenantInAnyStatus_ReturnsItWithItsStatus(string stored, string answered)
    {
        var tenant = await _testData.AddTenantAsync(status: stored, name: "Admin View Ltd");

        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("id").GetGuid(), found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString())
            .ShouldBe((tenant.Id, "Admin View Ltd", tenant.Slug, answered));
        found.EnumerateObject().Select(field => field.Name).ShouldBe(
            [
                "id", "name", "slug", "status", "suspensionReason", "closedAt", "purgedAt", "reopenableUntil", "purgeWarningAt", "memberCount", "ownerCount",
                "owners", "ownerInvitation", "createdAt", "createdBy", "suspendedAt", "suspendedBy", "closedBy", "purgeStartedAt", "pendingInvitationCount",
            ],
            ignoreOrder: true);
    }

    // An organisation's page has a Created row (step r13): the tenant's created_at, which every status keeps.
    [Fact]
    public async Task GetTenant_AnActiveTenant_AnswersWhenItWasCreated()
    {
        var created = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(createdAt: created);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(created);
    }

    [Fact]
    public async Task GetTenant_AClosedTenant_AnswersWhenItWasCreated()
    {
        var created = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: DateTimeOffset.UtcNow, createdAt: created);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(created);
    }

    // K4: the tombstone keeps the moment of the tenant's creation.
    [Fact]
    public async Task GetTenant_APurgedTenant_AnswersWhenItWasCreated()
    {
        var created = new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);
        var tenant = await _testData.AddTenantRowAsync(status: "Purged", createdAt: created);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("createdAt").GetDateTimeOffset().ShouldBe(created);
    }

    // K4: the purge leaves a tombstone, the tenant row with its id, name, slug, status and the moments of its creation and its purge, and
    // no summary: no member, no Owner, no invitation.
    [Fact]
    public async Task GetTenant_APurgedTenant_AnswersItsTombstone()
    {
        var purgedAt = new DateTimeOffset(2026, 10, 20, 9, 30, 0, TimeSpan.Zero);
        var tenant = await _testData.AddTenantRowAsync(status: "Purged", name: "Gone Ltd");
        await database.ScalarAsync<object>($"UPDATE control_plane.tenants SET purged_at = '{purgedAt:O}' WHERE id = '{tenant.Id}'");

        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (found.GetProperty("name").GetString(), found.GetProperty("slug").GetString(), found.GetProperty("status").GetString(), found.GetProperty("purgedAt").GetDateTimeOffset())
            .ShouldBe(("Gone Ltd", tenant.Slug, "purged", purgedAt));
        (found.GetProperty("suspensionReason").ValueKind, found.GetProperty("closedAt").ValueKind, found.GetProperty("reopenableUntil").ValueKind, found.GetProperty("ownerInvitation").ValueKind)
            .ShouldBe((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null));
        (found.GetProperty("memberCount").GetInt32(), found.GetProperty("ownerCount").GetInt32(), found.GetProperty("owners").GetArrayLength()).ShouldBe((0, 0, 0));
    }


    [Fact]
    public async Task GetTenant_ClosedAfterASuspension_ShowsTheReasonAndTheMomentOfTheClosure()
    {
        var closedAt = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(status: "Closed", suspensionReason: "Unpaid invoices", closedAt: closedAt);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        (found.GetProperty("suspensionReason").GetString(), found.GetProperty("closedAt").GetDateTimeOffset()).ShouldBe(("Unpaid invoices", closedAt));
        found.GetProperty("reopenableUntil").GetDateTimeOffset().ShouldBe(closedAt.AddDays(30));
    }

    // The platform shows when the Owners of a closed tenant are or were warned (decision A of 2026-10-24): the closure plus ReopenPeriod
    // less PurgeWarningLead, computed on each read, the moment the warning's entries carry. A purging tenant keeps its closure, so it
    // answers the moment too.
    [Theory]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public async Task GetTenant_AClosedOrAPurgingTenant_AnswersWhenItsOwnersAreWarned(string status)
    {
        var closedAt = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(status: status, closedAt: closedAt);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeWarningAt").GetDateTimeOffset().ShouldBe(closedAt.AddDays(23));
    }

    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    public async Task GetTenant_AnActiveOrASuspendedTenant_AnswersNoWarning(string status)
    {
        var tenant = await _testData.AddTenantAsync(status: status);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeWarningAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // A purged tenant's tombstone keeps no closure (K4), so it answers no warning.
    [Fact]
    public async Task GetTenant_APurgedTenant_AnswersNoWarning()
    {
        var tenant = await _testData.AddTenantRowAsync(status: "Purged");
        await database.ScalarAsync<object>($"UPDATE control_plane.tenants SET purged_at = now() WHERE id = '{tenant.Id}'");

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeWarningAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // The moment follows the same settings as the purge.
    [Fact]
    public async Task GetTenant_OtherPurgeSettings_AnswersTheWarningFromThem()
    {
        var closedAt = new DateTimeOffset(2026, 10, 1, 9, 30, 0, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(status: "Closed", closedAt: closedAt);
        await using var api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            settings: new Dictionary<string, string?> { ["ControlPlane:Tenants:ReopenPeriod"] = "10.00:00:00", ["ControlPlane:Tenants:PurgeWarningLead"] = "2.00:00:00" });

        var found = await api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true)
            .GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeWarningAt").GetDateTimeOffset().ShouldBe(closedAt.AddDays(8));
    }

    [Fact]
    public async Task GetTenant_ItsFirstOwnerJoined_AnswersTheOwnerAndNoInvitation()
    {
        var admin = await SystemAdminAsync();
        var email = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, email);
        var (owner, _) = await _tenants.AcceptAsync(await _tenants.LatestInvitationAsync(tenant), email);

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        (found.GetProperty("memberCount").GetInt32(), found.GetProperty("ownerCount").GetInt32()).ShouldBe((1, 1));
        Owners(found).ShouldBe([$"{await _testData.UserIdOfAsync(owner)}|Ada Lovelace|{email}"]);
        found.GetProperty("ownerInvitation").ValueKind.ShouldBe(JsonValueKind.Null);
        found.GetProperty("reopenableUntil").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    // Every Owner, in the order they joined; a member who is no Owner is counted, and not listed.
    [Fact]
    public async Task GetTenant_SeveralOwners_AnswersEveryOwnerInTheOrderTheyJoined()
    {
        var admin = await SystemAdminAsync();
        var (ada, grace, alan) = (PlatformTenants.Email(), PlatformTenants.Email(), PlatformTenants.Email());
        var tenant = await _tenants.CreateAsync(admin, ada);
        var (first, _) = await _tenants.AcceptAsync(await _tenants.LatestInvitationAsync(tenant), ada);
        var graces = await PlatformTenants.InvitationIdOfAsync(
            await _api.WaitingForMessagesAsync(() => _api.CreateClient(first).InviteMemberAsync(tenant, grace, role: "owner")));
        _time.Advance(TimeSpan.FromMinutes(1));
        var (second, _) = await _tenants.AcceptAsync(graces, grace, "Grace Hopper");
        var alans = await PlatformTenants.InvitationIdOfAsync(
            await _api.WaitingForMessagesAsync(() => _api.CreateClient(first).InviteMemberAsync(tenant, alan, role: "member")));
        await _tenants.AcceptAsync(alans, alan, "Alan Turing");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        (found.GetProperty("memberCount").GetInt32(), found.GetProperty("ownerCount").GetInt32()).ShouldBe((3, 2));
        Owners(found).ShouldBe([$"{await _testData.UserIdOfAsync(first)}|Ada Lovelace|{ada}", $"{await _testData.UserIdOfAsync(second)}|Grace Hopper|{grace}"]);
        found.GetProperty("ownerInvitation").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetTenant_ItsOwnerInvitationIsPending_AnswersTheInvitation()
    {
        var admin = await SystemAdminAsync();
        var email = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, email);

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        (found.GetProperty("memberCount").GetInt32(), found.GetProperty("ownerCount").GetInt32()).ShouldBe((0, 0));
        Owners(found).ShouldBeEmpty();
        Invitation(found).ShouldBe($"{email}|{_time.GetUtcNow().AddDays(7):O}|pending");
    }

    // The state "expired" is read from the expiry at the moment of the request, not stored.
    [Fact]
    public async Task GetTenant_ItsOwnerInvitationExpired_AnswersItAsExpired()
    {
        var admin = await SystemAdminAsync();
        var email = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, email);
        var expiresAt = _time.GetUtcNow().AddDays(7);
        _time.Advance(TimeSpan.FromDays(7));

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        Invitation(found).ShouldBe($"{email}|{expiresAt:O}|expired");
    }

    // Closing cancels the pending invitation (K4): a closed tenant without an Owner shows it cancelled, and when it can be reopened.
    [Fact]
    public async Task GetTenant_ClosedWithoutAnOwner_AnswersTheCancelledInvitationAndWhenItCanBeReopened()
    {
        var admin = await SystemAdminAsync();
        var email = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, email);
        var expiresAt = _time.GetUtcNow().AddDays(7);
        await _api.WaitingForMessagesAsync(() => admin.CloseTenantAsync(tenant));

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        found.GetProperty("status").GetString().ShouldBe("closed");
        found.GetProperty("reopenableUntil").GetDateTimeOffset().ShouldBe(_time.GetUtcNow().AddDays(30));
        Invitation(found).ShouldBe($"{email}|{expiresAt:O}|cancelled");
    }

    // The organisation's page says who created it, as they are named now, and when its Owner invitation was sent (the maintainer's
    // decision of 2026-11-08).
    [Fact]
    public async Task GetTenant_ACreatedTenant_AnswersWhoCreatedItAndWhenTheOwnerInvitationWasSent()
    {
        var (admin, _) = await NamedSystemAdminAsync("Deniz Kaya");
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        found.GetProperty("createdBy").ToString().ShouldBe("""{"name":"Deniz Kaya","deleted":false}""");
        found.GetProperty("ownerInvitation").GetProperty("sentAt").GetDateTimeOffset().ShouldBe(_time.GetUtcNow());
    }

    // "Invite Owner again" sends a new invitation, so the sending is the latest invitation's.
    [Fact]
    public async Task GetTenant_ItsOwnerInvitedAgain_AnswersWhenTheLatestInvitationWasSent()
    {
        var admin = await SystemAdminAsync();
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        _time.Advance(TimeSpan.FromHours(3));
        (await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, PlatformTenants.Email()))).EnsureSuccessStatusCode();

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        found.GetProperty("ownerInvitation").GetProperty("sentAt").GetDateTimeOffset().ShouldBe(_time.GetUtcNow());
    }

    // A deleted system admin has no name: the screen says "Deleted user".
    [Fact]
    public async Task GetTenant_ItsCreatorWasDeleted_AnswersTheCreatorDeleted()
    {
        var (admin, creator) = await NamedSystemAdminAsync("Deniz Kaya");
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        await _testData.DeleteUserAsync(creator);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        found.GetProperty("createdBy").ToString().ShouldBe("""{"name":null,"deleted":true}""");
    }

    // A seed, and every tenant from before the columns, has no "by" and no "at": nothing is backfilled (the maintainer's decision of
    // 2026-11-08).
    [Theory]
    [InlineData("Active")]
    [InlineData("Suspended")]
    [InlineData("Closed")]
    [InlineData("Purging")]
    public async Task GetTenant_ASeed_AnswersNoCreatorAndNoMomentOfItsEvents(string status)
    {
        var tenant = await SeedAsync(status);

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        Moments(found).ShouldBe("-|-|-|-|-");
    }

    // The Suspended banner: when and by whom. A reactivation forgets both, as it forgets the reason.
    [Fact]
    public async Task GetTenant_SuspendedThenReactivated_AnswersTheSuspensionThenNone()
    {
        var (admin, _) = await NamedSystemAdminAsync("Deniz Kaya");
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        _time.Advance(TimeSpan.FromDays(1));
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant));
        var suspendedAt = _time.GetUtcNow();

        var suspended = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);
        await _api.WaitingForMessagesAsync(() => admin.PostAsync($"/v1/platform/tenants/{tenant}/reactivate", null, Cancellation));
        var reactivated = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        Moments(suspended).ShouldBe($"Deniz Kaya|{suspendedAt:O}|Deniz Kaya|-|-");
        Moments(reactivated).ShouldBe("Deniz Kaya|-|-|-|-");
    }

    // A second suspension keeps the first, as it keeps the first reason.
    [Fact]
    public async Task GetTenant_SuspendedTwice_KeepsTheFirstSuspension()
    {
        var (admin, _) = await NamedSystemAdminAsync("Deniz Kaya");
        var (other, _) = await NamedSystemAdminAsync("Selin Arslan");
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant));
        var suspendedAt = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromHours(1));
        await _api.WaitingForMessagesAsync(() => other.SuspendTenantAsync(tenant));

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        Moments(found).ShouldBe($"Deniz Kaya|{suspendedAt:O}|Deniz Kaya|-|-");
    }

    // The Closed banner says who closed it. A closure after a suspension keeps the suspension, as it keeps the reason; a reopening forgets
    // both.
    [Fact]
    public async Task GetTenant_SuspendedClosedThenReopened_AnswersWhoClosedItThenNoClosureAndNoSuspension()
    {
        var (admin, _) = await NamedSystemAdminAsync("Deniz Kaya");
        var (closer, _) = await NamedSystemAdminAsync("Selin Arslan");
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        await _api.WaitingForMessagesAsync(() => admin.SuspendTenantAsync(tenant));
        var suspendedAt = _time.GetUtcNow();
        await _api.WaitingForMessagesAsync(() => closer.CloseTenantAsync(tenant));

        var closed = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);
        await _api.WaitingForMessagesAsync(() => admin.ReopenTenantAsync(tenant));
        var reopened = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        Moments(closed).ShouldBe($"Deniz Kaya|{suspendedAt:O}|Deniz Kaya|Selin Arslan|-");
        Moments(reopened).ShouldBe("Deniz Kaya|-|-|-|-");
    }

    // The "Being deleted since" row: the moment the purge started, which the tenant row keeps while it is purging.
    [Fact]
    public async Task GetTenant_APurgingTenant_AnswersWhenItsPurgeStarted()
    {
        var startedAt = new DateTimeOffset(2026, 11, 1, 9, 30, 0, TimeSpan.Zero);
        var tenant = await _testData.AddTenantAsync(status: "Purging", closedAt: startedAt.AddDays(-30));
        await database.ScalarAsync<object>($"UPDATE control_plane.tenants SET purge_started_at = '{startedAt:O}' WHERE id = '{tenant.Id}'");

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("purgeStartedAt").GetDateTimeOffset().ShouldBe(startedAt);
    }

    // The members line of the page counts the pending invitations that have not expired at the read (K10, R12).
    [Fact]
    public async Task GetTenant_PendingInvitations_CountsThoseNotExpiredAtTheRead()
    {
        var tenant = await _testData.AddTenantAsync();
        var now = _time.GetUtcNow();
        await _testData.AddInvitationAsync(tenant.Id, expiresAt: now.AddDays(1));
        await _testData.AddInvitationAsync(tenant.Id, expiresAt: now.AddDays(4));
        await _testData.AddInvitationAsync(tenant.Id, expiresAt: now.AddDays(6));
        await _testData.AddInvitationAsync(tenant.Id, status: "Accepted");
        await _testData.SummarizeAsync(tenant.Id);
        _time.Advance(TimeSpan.FromDays(2));

        var found = await (await SystemAdminAsync()).GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant.Id}", Cancellation);

        found.GetProperty("pendingInvitationCount").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task GetTenant_AnIdNoTenantHas_IsNotFound()
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.not_found");
    }

    // Each Owner as "userId|name|email".
    private static IEnumerable<string> Owners(JsonElement tenant) =>
        tenant.GetProperty("owners").EnumerateArray().Select(owner =>
            $"{owner.GetProperty("userId").GetGuid()}|{owner.GetProperty("name").GetString()}|{owner.GetProperty("email").GetString()}");

    // The Owner invitation as "email|expiresAt|state".
    private static string Invitation(JsonElement tenant)
    {
        var invitation = tenant.GetProperty("ownerInvitation");
        return $"{invitation.GetProperty("email").GetString()}|{invitation.GetProperty("expiresAt").GetDateTimeOffset():O}|{invitation.GetProperty("state").GetString()}";
    }

    // "createdBy|suspendedAt|suspendedBy|closedBy|purgeStartedAt", each person by their name, "-" for null.
    private static string Moments(JsonElement tenant) => string.Join('|',
        NameOf(tenant.GetProperty("createdBy")),
        tenant.GetProperty("suspendedAt") is { ValueKind: JsonValueKind.String } suspendedAt ? suspendedAt.GetDateTimeOffset().ToString("O") : "-",
        NameOf(tenant.GetProperty("suspendedBy")),
        NameOf(tenant.GetProperty("closedBy")),
        tenant.GetProperty("purgeStartedAt") is { ValueKind: JsonValueKind.String } startedAt ? startedAt.GetDateTimeOffset().ToString("O") : "-");

    private static string NameOf(JsonElement person) => person.ValueKind == JsonValueKind.Object ? person.GetProperty("name").GetString()! : "-";

    // A tenant written as a seed writes it, in the status, with the reason a suspension has and the closure a closed tenant has.
    private async Task<Guid> SeedAsync(string status) =>
        (await _testData.AddTenantAsync(
            status: status,
            suspensionReason: status == "Suspended" ? "Unpaid invoices" : null,
            closedAt: status is "Closed" or "Purging" ? DateTimeOffset.UtcNow : null)).Id;

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    // A system admin with a name, as the screens name them.
    private async Task<(HttpClient Client, string ExternalId)> NamedSystemAdminAsync(string name)
    {
        var admin = await _testData.AddUserAsync(name: name);
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{admin}'");
        return (_api.CreateClient(admin, secondFactor: true), admin);
    }
}
