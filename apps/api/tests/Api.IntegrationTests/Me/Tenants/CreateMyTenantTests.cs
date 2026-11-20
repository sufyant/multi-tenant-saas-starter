using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Tenants.EndTenantTrial;
using ControlPlane.Application.Tenants.WarnTenantTrialEnding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Me.Tenants;

// Step 40d: a user creates an organisation of their own while the product has sign-up on, and becomes its only Owner (1.28, 2.38). A
// person creates a limited number of organisations (2.39).
public sealed class CreateMyTenantTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), settings: SignUpSettings.OnWithOneTenant);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateMyTenant_AUser_CreatesAnActiveTenantOnTheDefaultPlanWithThemAsItsOnlyOwner()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var slug = NewSlug();

        var created = await _api.WaitingForMessagesAsync(() => CreateAsync(_api, person, slug));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tenant = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        (tenant.GetProperty("name").GetString(), tenant.GetProperty("slug").GetString(), tenant.GetProperty("status").GetString()).ShouldBe(("Kestrel Analytics", slug, "active"));
        RoleAnswers.Of(tenant).ShouldBe(RoleAnswers.BuiltIn("owner"));
        var tenantId = tenant.GetProperty("id").GetGuid();
        (await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT string_agg(roles.built_in || ':' || users.external_id, ',') FROM control_plane.memberships
            JOIN control_plane.roles ON roles.id = memberships.role_id JOIN control_plane.users ON users.id = memberships.user_id
            WHERE memberships.tenant_id = '{tenantId}'
            """)).ShouldBe($"Owner:{person}");
        (await database.ScalarAsSuperuserAsync<bool>(
            $"SELECT plan_id = (SELECT plan_id FROM control_plane.default_plans) FROM control_plane.tenants WHERE id = '{tenantId}'")).ShouldBeTrue();
        (await database.ScalarAsSuperuserAsync<int>($"SELECT membership_count FROM control_plane.users WHERE external_id = '{person}'")).ShouldBe(1);
        (await database.ScalarAsSuperuserAsync<string>($"SELECT member_count || ':' || owner_count FROM control_plane.tenant_summaries WHERE tenant_id = '{tenantId}'"))
            .ShouldBe("1:1");
    }

    // K3: the user acted, in both logs.
    [Fact]
    public async Task CreateMyTenant_AUser_IsRecordedInBothLogsByTheUser()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var userId = await _testData.UserIdOfAsync(person);

        var created = await _api.WaitingForMessagesAsync(() => CreateAsync(_api, person, NewSlug()));

        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        (await _audit.TenantLogAsync(tenantId)).ShouldBe($"{tenantId}:tenant.created|User|{userId}|Tenant|{tenantId}|Kestrel Analytics");
        (await _audit.PlatformLogAsync(tenantId)).ShouldBe($"tenant.created|User|{userId}|Tenant|{tenantId}|Kestrel Analytics");
    }

    // 13.25, 3.19: the person who created the organisation gets the welcome in its bell and by email, with the trial that started.
    [Fact]
    public async Task CreateMyTenant_BillingOn_WelcomesTheCreatorWithTheTrial()
    {
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));
        await using var billing = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            time: time,
            settings: new Dictionary<string, string?>(SignUpSettings.On.Concat(BillingRequests.On)));
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var userId = await _testData.UserIdOfAsync(person);
        var endsAt = (time.GetUtcNow() + TimeSpan.FromDays(14)).ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

        var created = await billing.WaitingForMessagesAsync(() => CreateAsync(billing, person, NewSlug()));

        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        var notifications = new NotificationRows(database);
        (await notifications.AboutAsync(tenantId, userId)).ShouldBe([NotificationRows.Row(userId, "tenant.created", tenantId, "Tenant")]);
        (await notifications.ValueListOfAsync(userId, "tenant.created", tenantId)).ShouldBe(
            $"endsAt={endsAt}, planName=Business, productName=Starter, tenantName=Kestrel Analytics, trialDays=14");
    }

    // 13.25, 3.19: while billing is off, the welcome names no trial.
    [Fact]
    public async Task CreateMyTenant_BillingOff_WelcomesTheCreatorWithoutATrial()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var userId = await _testData.UserIdOfAsync(person);

        var created = await _api.WaitingForMessagesAsync(() => CreateAsync(_api, person, NewSlug()));

        var tenantId = (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        (await new NotificationRows(database).ValueListOfAsync(userId, "tenant.created", tenantId))
            .ShouldBe("productName=Starter, tenantName=Kestrel Analytics");
    }

    // 2.39: the limit counts the tenants the person created; the next one is refused and nothing is created.
    [Fact]
    public async Task CreateMyTenant_AtTheLimit_IsAConflictAndCreatesNothing()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        (await _api.WaitingForMessagesAsync(() => CreateAsync(_api, person, NewSlug()))).EnsureSuccessStatusCode();
        var slug = NewSlug();

        var refused = await CreateAsync(_api, person, slug);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Conflict, "tenant.creation_limit_reached"));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    // K6f: two creations of one person at once run one after the other under the user row's lock, so the limit counts the first. A holder
    // locks the user row until both creations wait behind it.
    [Fact]
    public async Task CreateMyTenant_TwoAtOnceWithRoomForOne_CreatesOneAndRefusesTheOther()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        await using var holder = new NpgsqlConnection(database.SuperuserConnectionString);
        await holder.OpenAsync(Cancellation);
        await using var holding = await holder.BeginTransactionAsync(Cancellation);
        await using var hold = new NpgsqlCommand($"SELECT 1 FROM control_plane.users WHERE external_id = '{person}' FOR UPDATE", holder, holding);
        await hold.ExecuteScalarAsync(Cancellation);

        var creating = _api.WaitingForMessagesAsync(() => Task.WhenAll(CreateAsync(_api, person, NewSlug()), CreateAsync(_api, person, NewSlug())));
        await Waiting.UntilAsync(async () => creating.IsCompleted || await WaitingBehindAsync(holder.ProcessID) == 2);
        await holding.CommitAsync(Cancellation);
        var created = await creating;

        created.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.Created, HttpStatusCode.Conflict], ignoreOrder: true);
        (await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.tenants WHERE created_by = (SELECT id FROM control_plane.users WHERE external_id = '{person}')")).ShouldBe(1);
    }

    [Fact]
    public async Task CreateMyTenant_ATakenSlug_IsAConflictAndCreatesNothing()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var (_, slug) = await _testData.AddTenantAsync();

        var refused = await CreateAsync(_api, person, slug);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Conflict, "tenant.slug_taken"));
        (await database.ScalarAsSuperuserAsync<int>($"SELECT membership_count FROM control_plane.users WHERE external_id = '{person}'")).ShouldBe(0);
    }

    [Fact]
    public async Task CreateMyTenant_APersonWhoIsNotAUserYet_IsNotFound()
    {
        var person = $"user_{Guid.NewGuid():N}";
        var slug = NewSlug();

        var refused = await CreateAsync(_api, person, slug);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.NotFound, "user.not_found"));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    // The product has sign-up off by default: only a system admin creates a tenant.
    [Fact]
    public async Task CreateMyTenant_SignUpOff_IsForbiddenAndCreatesNothing()
    {
        await using var off = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        var slug = NewSlug();

        var refused = await CreateAsync(off, person, slug);

        (refused.StatusCode, await CodeOfAsync(refused)).ShouldBe((HttpStatusCode.Forbidden, "sign_up.disabled"));
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    // Step 40d: while billing is on, the tenant starts a 14-day trial of Business at once, keeps the default plan as the plan it returns
    // to, and schedules the trial's end and its reminder 3 days before (1.28, 10.08, 13.16).
    [Fact]
    public async Task CreateMyTenant_BillingOn_StartsATrialOfBusinessAndSchedulesItsEnd()
    {
        var time = new FakeTimeProvider(DateTimeOffset.FromUnixTimeSeconds(TimeProvider.System.GetUtcNow().ToUnixTimeSeconds()));
        await using var billing = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            time: time,
            settings: new Dictionary<string, string?>(SignUpSettings.On.Concat(BillingRequests.On)));
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));
        HttpResponseMessage? created = null;

        var session = await billing.TrackMessagesAsync(async () => created = await CreateAsync(billing, person, NewSlug()));

        var tenantId = (await created!.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        var endsAt = time.GetUtcNow() + TimeSpan.FromDays(14);
        (await database.ScalarAsSuperuserAsync<string>(
            $"""
            SELECT concat_ws('|', trial_plan_id, plan_id, plan_before_trial_id = (SELECT plan_id FROM control_plane.default_plans),
                trial_ends_at = '{endsAt:O}'::timestamptz, trial_ends_at - trial_started_at = interval '14 days')
            FROM control_plane.tenants WHERE id = '{tenantId}'
            """)).ShouldBe($"{SeedPlans.Business}|{SeedPlans.Business}|t|t|t");
        session.Scheduled.Envelopes().Select(envelope => (envelope.Message, envelope.ScheduledTime))
            .ShouldBe([(new EndTenantTrialCommand(tenantId, endsAt), endsAt), (new WarnTenantTrialEndingCommand(tenantId, endsAt), endsAt - TimeSpan.FromDays(3))]);
    }

    [Fact]
    public async Task CreateMyTenant_AReservedSlug_IsABadRequest()
    {
        var person = await _testData.AddUserAsync(email: TestEmails.Of($"{Guid.NewGuid():N}@example.com"));

        var refused = await CreateAsync(_api, person, "settings");

        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private static string NewSlug() => $"kestrel-{Guid.NewGuid():N}"[..20];

    // POST /v1/me/tenants creates a tenant, so it takes an idempotency key (I1).
    private static async Task<HttpResponseMessage> CreateAsync(ApiFactory api, string person, string slug)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/me/tenants")
        {
            Content = JsonContent.Create(new { name = "Kestrel Analytics", slug }),
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await api.CreateClient(person).SendAsync(request, Cancellation);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private Task<long> WaitingBehindAsync(int holder) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            WITH RECURSIVE behind (pid) AS (
                SELECT pid FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))
                UNION SELECT waiting.pid FROM pg_stat_activity AS waiting JOIN behind ON behind.pid = ANY(pg_blocking_pids(waiting.pid)))
            SELECT count(*) FROM behind
            """);
}
