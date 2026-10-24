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
            ["id", "name", "slug", "status", "suspensionReason", "closedAt", "purgedAt", "reopenableUntil", "purgeWarningAt", "memberCount", "ownerCount", "owners", "ownerInvitation"],
            ignoreOrder: true);
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
        Owners(found).ShouldBe([$"{await _testData.UserIdOfAsync(owner)}|Ada|Lovelace|{email}"]);
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
        var (second, _) = await _tenants.AcceptAsync(graces, grace, "Grace", "Hopper");
        var alans = await PlatformTenants.InvitationIdOfAsync(
            await _api.WaitingForMessagesAsync(() => _api.CreateClient(first).InviteMemberAsync(tenant, alan, role: "member")));
        await _tenants.AcceptAsync(alans, alan, "Alan", "Turing");

        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);

        (found.GetProperty("memberCount").GetInt32(), found.GetProperty("ownerCount").GetInt32()).ShouldBe((3, 2));
        Owners(found).ShouldBe([$"{await _testData.UserIdOfAsync(first)}|Ada|Lovelace|{ada}", $"{await _testData.UserIdOfAsync(second)}|Grace|Hopper|{grace}"]);
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

    [Fact]
    public async Task GetTenant_AnIdNoTenantHas_IsNotFound()
    {
        var response = await (await SystemAdminAsync()).GetAsync($"/v1/platform/tenants/{Guid.NewGuid()}", Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.not_found");
    }

    // Each Owner as "userId|firstName|lastName|email".
    private static IEnumerable<string> Owners(JsonElement tenant) =>
        tenant.GetProperty("owners").EnumerateArray().Select(owner =>
            $"{owner.GetProperty("userId").GetGuid()}|{owner.GetProperty("firstName").GetString()}|{owner.GetProperty("lastName").GetString()}|{owner.GetProperty("email").GetString()}");

    // The Owner invitation as "email|expiresAt|state".
    private static string Invitation(JsonElement tenant)
    {
        var invitation = tenant.GetProperty("ownerInvitation");
        return $"{invitation.GetProperty("email").GetString()}|{invitation.GetProperty("expiresAt").GetDateTimeOffset():O}|{invitation.GetProperty("state").GetString()}";
    }

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);
}
