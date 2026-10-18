using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ControlPlane.Application.Ports;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// A system admin creates a tenant through the system door, and the tenant creation saga runs through the outbox. TenantCreationTests
// follows the saga itself.
public sealed class CreateTenantTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateTenant_Valid_AnswersProvisioningAndBecomesActiveWithItsOwnerInvited()
    {
        var admin = await AdminAsync();
        var slug = Slug();
        var owner = $"{Guid.NewGuid():N}@example.com";

        var created = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = owner }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("provisioning");
        (await StatusOfAsync(slug)).ShouldBe("Active");
        _api.Email.Sent.ShouldContain(sent => sent.To == owner);
    }

    // Settings › General shows when the tenant was created: the moment of the request, by the module's clock.
    [Fact]
    public async Task CreateTenant_Valid_StoresWhenTheTenantWasCreated()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        await _api.DisposeAsync();
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: new FakeTimeProvider(now));
        var slug = Slug();
        var admin = await AdminAsync();

        var created = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = $"{Guid.NewGuid():N}@example.com" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await database.ScalarAsync<DateTime>($"SELECT created_at FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(now.UtcDateTime);
    }

    // Decision 5 of step 20j: without a language, the owner's invitation speaks the system admin's own, and its email does too.
    [Fact]
    public async Task CreateTenant_WithoutALanguage_InvitesTheOwnerInTheSystemAdminsLanguage()
    {
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(language: "ar"), secondFactor: true);
        var owner = $"{Guid.NewGuid():N}@example.com";

        var created = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = owner }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await _catalog.InvitationLanguageOfAsync(owner)).ShouldBe("ar");
        _api.Email.Sent.Single(sent => sent.To == owner).Subject.ShouldBe("دعوة للانضمام إلى Acme Ltd");
    }

    [Fact]
    public async Task CreateTenant_WithALanguage_InvitesTheOwnerInIt()
    {
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(language: "ar"), secondFactor: true);
        var owner = $"{Guid.NewGuid():N}@example.com";

        var created = await _api.WaitingForMessagesAsync(
            () => admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = owner, language = "tr" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await _catalog.InvitationLanguageOfAsync(owner)).ShouldBe("tr");
        _api.Email.Sent.Single(sent => sent.To == owner).Subject.ShouldBe("Acme Ltd adlı kuruluşa davet edildiniz");
    }

    [Fact]
    public async Task CreateTenant_WithALanguageNotOfTheList_IsABadRequestAndCreatesNothing()
    {
        var admin = await AdminAsync();
        var slug = Slug();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = "owner@example.com", language = "de" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    [Fact]
    public async Task CreateTenant_SlugTaken_IsAConflict()
    {
        var admin = await AdminAsync();
        var existing = await _catalog.AddTenantAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = existing.Slug, ownerEmail = "owner@example.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.slug_taken");
    }

    [Theory]
    [InlineData("Not A Slug", "owner@example.com")]
    [InlineData("valid-slug", "not an email")]
    public async Task CreateTenant_InvalidSlugOrOwnerEmail_IsABadRequest(string slug, string ownerEmail)
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenant_WithNameSlugAndOwnerEmail_ReturnsTheTenant()
    {
        var admin = await AdminAsync();
        var slug = Slug();

        var created = await _api.WaitingForMessagesAsync(() =>
            admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var tenant = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        tenant.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "suspensionReason", "closedAt"], ignoreOrder: true);
        tenant.GetProperty("id").GetGuid().ShouldBe(await database.ScalarAsync<Guid>($"SELECT id FROM control_plane.tenants WHERE slug = '{slug}'"));
        tenant.GetProperty("name").GetString().ShouldBe("Acme Ltd");
        tenant.GetProperty("slug").GetString().ShouldBe(slug);
        tenant.GetProperty("status").GetString().ShouldBe("provisioning");
        (await database.ScalarAsync<string>($"SELECT name FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe("Acme Ltd");
    }

    // T5: the system door is /v1/platform; the old path is gone.
    [Fact]
    public async Task CreateTenant_OnTheOldPath_IsNotFound()
    {
        var admin = await AdminAsync();

        var created = await admin.PostAsJsonAsync("/v1/system/tenants", new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    public static TheoryData<string> InvalidNames => ["", "   ", new string('n', 101)];

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task CreateTenant_InvalidName_IsRejected(string name)
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new { name, slug = Slug(), ownerEmail = "ali@acme.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.name_invalid");
    }

    [Fact]
    public async Task CreateTenant_WithoutAName_IsRejected()
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new { slug = Slug(), ownerEmail = "ali@acme.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // OWASP API8: a field the request leaves out, or sends as null, is a validation problem, never a server error.
    [Theory]
    [InlineData("""{"name":"Acme Ltd","ownerEmail":"ali@acme.com"}""", "slug")]
    [InlineData("""{"name":"Acme Ltd","slug":null,"ownerEmail":"ali@acme.com"}""", "slug")]
    [InlineData("""{"name":"Acme Ltd","slug":"acme-ltd"}""", "ownerEmail")]
    [InlineData("""{"name":"Acme Ltd","slug":"acme-ltd","ownerEmail":null}""", "ownerEmail")]
    [InlineData("""{"slug":"acme-ltd","ownerEmail":"ali@acme.com"}""", "name")]
    [InlineData("""{"name":null,"slug":"acme-ltd","ownerEmail":"ali@acme.com"}""", "name")]
    public async Task CreateTenant_WithoutARequiredField_IsAValidationProblem(string body, string field)
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new StringContent(body, Encoding.UTF8, "application/json"));

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetRawText().ShouldBe($$"""{"{{field}}":["required"]}""");
    }

    public static TheoryData<string, string> OwnerEmailsThatBreakARule => new()
    {
        { "not an email", "email_invalid" },
        { new string('x', 320) + "@acme.com", "too_long" },
    };

    // A screen writes its own text for each rule a field broke, so the answer names the rule's code, never a message.
    [Theory]
    [MemberData(nameof(OwnerEmailsThatBreakARule))]
    public async Task CreateTenant_AnOwnerEmailThatBreaksARule_ListsTheRulesCode(string ownerEmail, string code)
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("errors").GetRawText()
            .ShouldBe($$"""{"ownerEmail":["{{code}}"]}""");
    }

    // A body the endpoint cannot read at all is a bad request in every environment, never a server error.
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task CreateTenant_WithoutAReadableBody_IsABadRequest(string body)
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new StringContent(body, Encoding.UTF8, "application/json"));

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        created.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateTenant_NameOfOneHundredCharacters_IsAccepted()
    {
        var admin = await AdminAsync();
        var name = new string('n', 100);

        var created = await _api.WaitingForMessagesAsync(() =>
            admin.CreateTenantAsync(new { name, slug = Slug(), ownerEmail = "ali@acme.com" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("name").GetString().ShouldBe(name);
    }

    [Fact]
    public async Task CreateTenant_AsATenantMember_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var owner = await _catalog.AddMemberAsync(tenant.Id, role: "Owner");

        var created = await _api.CreateClient(owner, secondFactor: true)
            .CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "owner@example.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Section 8: tenant creation carries an idempotency key, so a request sent twice does not create a second tenant.
    [Fact]
    public async Task CreateTenant_WithoutAnIdempotencyKey_IsABadRequest()
    {
        var admin = await AdminAsync();
        var slug = Slug();

        var created = await admin.PostAsJsonAsync(TenantRequests.Path, new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" }, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("idempotency.key_invalid");
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    public static TheoryData<string> InvalidIdempotencyKeys => ["", "has space", "tab\there", "ключ", new string('k', 256)];

    [Theory]
    [MemberData(nameof(InvalidIdempotencyKeys))]
    public async Task CreateTenant_WithAnInvalidIdempotencyKey_IsABadRequest(string key)
    {
        var admin = await AdminAsync();
        using var request = new HttpRequestMessage(HttpMethod.Post, TenantRequests.Path)
        {
            Content = JsonContent.Create(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }),
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key).ShouldBeTrue();

        var created = await admin.SendAsync(request, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("idempotency.key_invalid");
    }

    [Fact]
    public async Task CreateTenant_AKeyOfTwoHundredFiftyFiveVisibleCharacters_IsAccepted()
    {
        var admin = await AdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }, new string('~', 255));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTenant_TheSameKeyAndBodyAgain_ReturnsTheFirstTenantAndCreatesNothingNew()
    {
        var admin = await AdminAsync();
        var slug = Slug();
        var body = new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" };
        var key = Guid.NewGuid().ToString();
        var first = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(body, key));

        var again = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(body, key));

        var tenantId = await IdOfAsync(first);
        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await IdOfAsync(again)).ShouldBe(tenantId);
        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await again.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(1);
        (await CountAsync($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}'")).ShouldBe(1);
    }

    // RFC 9110, section 10.2.2: a 201 names the new resource in Location, and GetTenant reads it there. A replay names the same tenant (I5).
    [Fact]
    public async Task CreateTenant_Valid_AnswersTheAddressOfTheTenantInLocation()
    {
        var admin = await AdminAsync();
        var body = new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" };
        var key = Guid.NewGuid().ToString();
        var created = await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(body, key));

        var again = await admin.CreateTenantAsync(body, key);
        var read = await admin.GetAsync(created.Headers.Location, Cancellation);

        created.Headers.Location.ShouldBe(new Uri($"{TenantRequests.Path}/{await IdOfAsync(created)}", UriKind.Relative));
        again.Headers.Location.ShouldBe(created.Headers.Location);
        read.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The saved result holds the status as ControlPlane names it. The endpoint maps it to the API's lowercase value when it builds the
    // answer, so the first answer and its replay are the same.
    [Fact]
    public async Task CreateTenant_ReplayedAnswer_HasTheLowercaseStatus()
    {
        var admin = await AdminAsync();
        var body = new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" };
        var key = Guid.NewGuid().ToString();
        await _api.WaitingForMessagesAsync(() => admin.CreateTenantAsync(body, key));

        var again = await admin.CreateTenantAsync(body, key);

        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await again.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("status").GetString().ShouldBe("provisioning");
        (await CountAsync($"SELECT count(*) FROM api.idempotency_keys WHERE key = '{key}' AND result::text LIKE '%Provisioning%'")).ShouldBe(1);
    }

    [Fact]
    public async Task CreateTenant_TheSameKeyWithAnotherBody_IsUnprocessable()
    {
        var admin = await AdminAsync();
        var key = Guid.NewGuid().ToString();
        await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }, key);
        var otherSlug = Slug();

        var reused = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = otherSlug, ownerEmail = "ali@acme.com" }, key);

        reused.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await reused.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("idempotency.key_reused");
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{otherSlug}'")).ShouldBe(0);
    }

    [Fact]
    public async Task CreateTenant_TwoRequestsWithTheSameKeyAtOnce_CreateOneTenant()
    {
        var admin = await AdminAsync();
        var slug = Slug();
        var body = new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" };
        var key = Guid.NewGuid().ToString();

        var created = await Task.WhenAll(admin.CreateTenantAsync(body, key), admin.CreateTenantAsync(body, key));

        created.Select(response => response.StatusCode).ShouldContain(HttpStatusCode.Created);
        created.Select(response => response.StatusCode).ShouldAllBe(status => status == HttpStatusCode.Created || status == HttpStatusCode.Conflict);
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(1);
    }

    // A key belongs to the system admin who sent it.
    [Fact]
    public async Task CreateTenant_TwoSystemAdminsWithTheSameKey_EachCreateTheirOwnTenant()
    {
        var first = await AdminAsync();
        var second = await AdminAsync();
        var key = Guid.NewGuid().ToString();
        var firstSlug = Slug();
        var secondSlug = Slug();

        var byFirst = await first.CreateTenantAsync(new { name = "Acme Ltd", slug = firstSlug, ownerEmail = "ali@acme.com" }, key);
        var bySecond = await second.CreateTenantAsync(new { name = "Globex", slug = secondSlug, ownerEmail = "hank@globex.com" }, key);

        byFirst.StatusCode.ShouldBe(HttpStatusCode.Created);
        bySecond.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await IdOfAsync(bySecond)).ShouldNotBe(await IdOfAsync(byFirst));
    }

    // A7: the first system admin's first visit writes their catalog user with the configured address, which the identity provider
    // verified, and no names: the identity provider keeps none, and a system admin gives them. No invitation chose their language, so
    // they start with English (decision 6 of step 20j). Their system admin list is empty, so the test has a database of its own.
    [Fact]
    public async Task CreateTenant_FirstSystemAdminsFirstVisit_StartsTheirUserWithTheConfiguredAddressNoNamesAndEnglish()
    {
        var name = await database.CreateMigratedDatabaseAsync();
        await using var api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application, name));
        var admin = $"user_{Guid.NewGuid():N}";
        api.Identity.AddAccount(admin, ApiFactory.FirstSystemAdminEmail);

        var created = await api.WaitingForMessagesAsync(
            () => api.CreateClient(admin, secondFactor: true).CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "owner@example.com" }));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await _catalog.UserOfAsync(admin, name)).ShouldBe(new StoredUser(ApiFactory.FirstSystemAdminEmail, null, null, Deleted: false, "en", null));
    }

    private async Task<HttpClient> AdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private static async Task<Guid> IdOfAsync(HttpResponseMessage created) =>
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    private Task<long> CountAsync(string sql) => database.ScalarAsSuperuserAsync<long>(sql);

    private static string Slug() => $"tenant-{Guid.NewGuid():N}"[..20];

    private Task<string?> StatusOfAsync(string slug) => database.ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE slug = '{slug}'");
}
