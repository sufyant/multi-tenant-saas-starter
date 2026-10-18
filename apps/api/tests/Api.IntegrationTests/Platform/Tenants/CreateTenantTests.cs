using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ControlPlane.Application.Invitations.OpenMemberSignUp;
using ControlPlane.Application.Invitations.OpenOwnerSignUp;
using ControlPlane.Application.Tenants.ActivateTenant;
using ControlPlane.Contracts.Invitations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using SharedKernel;
using Tenancy;
using Wolverine.Tracking;

namespace Api.IntegrationTests.Platform.Tenants;

// A system admin creates a tenant through the system door, and the tenant creation saga runs through the outbox, from the first step to
// the Owner's invitation email, or to its compensation. TenantCreationTests follows what the saga does with its own record.
public sealed class CreateTenantTests(Database database) : IAsyncLifetime
{
    private readonly Catalog _catalog = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private readonly AuditEntries _audit = new(database);

    private ApiFactory _api = null!;

    public static TheoryData<string> InvalidNames => ["", "   "];

    public static TheoryData<string> InvalidSlugs =>
        ["", "ab", new string('s', 64), "Acme", "acme corp", "acme_corp", "-acme", "acme-", "acme--corp", "acmé", "acme\n"];

    public static TheoryData<string, string> OwnerEmailsThatBreakARule => new()
    {
        { "not an email", "email_invalid" },
        { new string('x', 320) + "@acme.com", "too_long" },
    };

    public static TheoryData<string> InvalidIdempotencyKeys => ["", "has space", "tab\there", "ключ", new string('k', 256)];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), configureServices: services => services.AddFakeLogging());
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task CreateTenant_Valid_AnswersProvisioningAndBecomesActiveWithItsOwnerInvited()
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();

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
        var admin = await SystemAdminAsync();
        var slug = Slug();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = "owner@example.com", language = "de" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    [Fact]
    public async Task CreateTenant_SlugTaken_IsAConflict()
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateTenant_WithNameSlugAndOwnerEmail_ReturnsTheTenant()
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();

        var created = await admin.PostAsJsonAsync("/v1/system/tenants", new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task CreateTenant_InvalidName_IsRejected(string name)
    {
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new { name, slug = Slug(), ownerEmail = "ali@acme.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("tenant.name_invalid");
    }

    // The maintainer's decision 2 after audit fix 09: the validator checks the name's trimmed length, so the document states it. The
    // tenant keeps its own check.
    [Fact]
    public async Task CreateTenant_NameTooLong_IsAValidationProblemForTheName()
    {
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new { name = $" {new string('n', 101)} ", slug = Slug(), ownerEmail = "ali@acme.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(created, "name")).ShouldBe(("request.validation_failed", "too_long"));
    }

    // The maintainer's decision 3 after audit fix 09: the validator checks the slug's length and shape, so the document states both.
    [Theory]
    [MemberData(nameof(InvalidSlugs))]
    public async Task CreateTenant_InvalidSlug_IsAValidationProblemForTheSlug(string slug)
    {
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" });

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await FieldCodesOfAsync(created, "slug")).ShouldBe(("request.validation_failed", "slug_invalid"));
    }

    [Fact]
    public async Task CreateTenant_WithoutAName_IsRejected()
    {
        var admin = await SystemAdminAsync();

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
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new StringContent(body, Encoding.UTF8, "application/json"));

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetRawText().ShouldBe($$"""{"{{field}}":["required"]}""");
    }

    // A screen writes its own text for each rule a field broke, so the answer names the rule's code, never a message.
    [Theory]
    [MemberData(nameof(OwnerEmailsThatBreakARule))]
    public async Task CreateTenant_AnOwnerEmailThatBreaksARule_ListsTheRulesCode(string ownerEmail, string code)
    {
        var admin = await SystemAdminAsync();

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
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new StringContent(body, Encoding.UTF8, "application/json"));

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        created.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task CreateTenant_NameOfOneHundredCharacters_IsAccepted()
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();
        var slug = Slug();

        var created = await admin.PostAsJsonAsync(TenantRequests.Path, new { name = "Acme Ltd", slug, ownerEmail = "ali@acme.com" }, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await created.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString().ShouldBe("idempotency.key_invalid");
        (await CountAsync($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(InvalidIdempotencyKeys))]
    public async Task CreateTenant_WithAnInvalidIdempotencyKey_IsABadRequest(string key)
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();

        var created = await admin.CreateTenantAsync(new { name = "Acme Ltd", slug = Slug(), ownerEmail = "ali@acme.com" }, new string('~', 255));

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateTenant_TheSameKeyAndBodyAgain_ReturnsTheFirstTenantAndCreatesNothingNew()
    {
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();
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
        var admin = await SystemAdminAsync();
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
        var first = await SystemAdminAsync();
        var second = await SystemAdminAsync();
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

    [Fact]
    public async Task CreateTenant_EveryStepSucceeds_ActivatesTheTenantInvitesTheOwnerOnceAndRecordsIt()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var owner = Email();

        var created = await _api.WaitingForMessagesAsync(() => CreateTenantAsync(admin, Slug(), owner));

        var tenantId = await IdOfCreatedAsync(created);
        var invitationId = await InvitationIdAsync(tenantId);
        (await TenantStatusAsync(tenantId)).ShouldBe("Active");
        (await InvitationStatusAsync(tenantId)).ShouldBe("Pending");
        _api.Email.Sent.Where(email => email.To == owner).ShouldHaveSingleItem().IdempotencyKey.ShouldBe($"invite/{invitationId}");
        (await _audit.PlatformLogAsync(tenantId)).ShouldBe($"tenant.created|User|{await _catalog.UserIdOfAsync(admin)}|Tenant|{tenantId}|Acme Ltd");
        (await _audit.TenantLogAsync(tenantId)).ShouldBe($"{tenantId}:tenant.created|Platform|-|Tenant|{tenantId}|Acme Ltd");
        (await SagaStateAsync(tenantId)).ShouldBe("Completed");
        await _summaries.ShouldMatchTheRowsAsync(tenantId);
    }

    // Step 3: the owner has no account, so Clerk creates its invitation once, and the email carries its link, which leads to our accept
    // link.
    [Fact]
    public async Task CreateTenant_OwnerWithoutAnAccount_CreatesOneClerkInvitationAndEmailsItsLink()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var owner = Email();

        var created = await _api.WaitingForMessagesAsync(() => CreateTenantAsync(admin, Slug(), owner));

        var tenantId = await IdOfCreatedAsync(created);
        var signUp = _api.Identity.SignUps.Where(signUp => signUp.Email == owner).ShouldHaveSingleItem();
        signUp.AcceptLink.GetLeftPart(UriPartial.Path).ShouldBe(ApiFactory.AcceptUrl);
        signUp.ExpiresAt.ShouldBe(await database.ScalarAsSuperuserAsync<DateTime>($"SELECT expires_at FROM control_plane.invitations WHERE tenant_id = '{tenantId}'"));
        _api.Email.LinkSentTo(owner).ShouldBe(FakeIdentityProvider.ClerkLink(signUp.InvitationId, signUp.AcceptLink));
    }

    // Section 6, step 1: the tenant, the owner's invitation and the saga record are written in one transaction.
    [Fact]
    public async Task CreateTenant_TheSagaCannotBeWritten_WritesNoTenantAndNoInvitation()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var slug = Slug();
        var owner = Email();
        await database.RefuseAsync("tenant_creation_sagas", "INSERT", $"NEW.tenant_id IN (SELECT id FROM control_plane.tenants WHERE slug = '{slug}')");

        var created = await _api.TrackMessagesAsync(() => CreateTenantAsync(admin, slug, owner));

        created.Sent.AllMessages().ShouldBeEmpty();
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.tenants WHERE slug = '{slug}'")).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE email = '{owner}'")).ShouldBe(0);
    }

    // The activation is refused by the database, as a real failure would be; the step is retried, then compensated.
    [Fact]
    public async Task CreateTenant_TheActivationKeepsFailing_CancelsTheTenantAndItsInvitation()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var slug = Slug();
        var owner = Email();
        await database.RefuseAsync("tenants", "UPDATE", $"NEW.slug = '{slug}' AND NEW.status = 'Active'");

        HttpResponseMessage created = null!;
        var messages = await _api.TrackMessagesAsync(async () => created = await CreateTenantAsync(admin, slug, owner));

        var tenantId = await IdOfCreatedAsync(created);
        messages.MovedToErrorQueue.SingleMessage<ActivateTenantCommand>().ShouldNotBeNull();
        (await TenantStatusAsync(tenantId)).ShouldBe("Cancelled");
        (await database.ScalarAsSuperuserAsync<string>($"SELECT cancellation_reason FROM control_plane.tenants WHERE id = '{tenantId}'"))
            .ShouldBe("activation_failed");
        _api.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(record =>
            record.Id.Name == "TenantCancelled"
            && record.Message.Contains(tenantId.ToString(), StringComparison.Ordinal)
            && record.Message.Contains("activation_failed", StringComparison.Ordinal));
        (await InvitationStatusAsync(tenantId)).ShouldBe("Cancelled");
        (await SagaStateAsync(tenantId)).ShouldBe("Cancelled");
        _api.Email.Attempts.ShouldNotContain(attempt => attempt.Email.To == owner);
        (await _audit.TenantLogAsync(tenantId)).ShouldBeNull();
        (await _audit.PlatformLogAsync(tenantId)).ShouldBeNull();
        await _summaries.ShouldMatchTheRowsAsync(tenantId);
    }

    // After the pivot nothing is undone: the tenant stays active, the invitation nobody received is cancelled, and a person is told.
    [Fact]
    public async Task CreateTenant_TheEmailKeepsFailing_LeavesTheTenantActiveCancelsTheInvitationAndRaisesTheAlarm()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        _api.Email.IsDown = true;
        using var alarms = new MetricCollector<long>(_api.Services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(), "Modules.ControlPlane", "tenant_creation.needs_attention");

        HttpResponseMessage created = null!;
        var messages = await _api.TrackMessagesAsync(async () => created = await CreateTenantAsync(admin, Slug(), Email()));

        var tenantId = await IdOfCreatedAsync(created);
        messages.MovedToErrorQueue.SingleMessage<OwnerInvitationReadyEvent>().ShouldNotBeNull();
        (await TenantStatusAsync(tenantId)).ShouldBe("Active");
        (await InvitationStatusAsync(tenantId)).ShouldBe("Cancelled");
        (await SagaStateAsync(tenantId)).ShouldBe("NeedsAttention");
        _api.Services.GetFakeLogCollector().GetSnapshot().ShouldContain(record =>
            record.Level == LogLevel.Error
            && record.Id.Name == "TenantCreationNeedsAttention"
            && record.Message.Contains(tenantId.ToString(), StringComparison.Ordinal));
        alarms.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
        await _summaries.ShouldMatchTheRowsAsync(tenantId);
    }

    // Clerk that stays down ends step 3 as an email that never goes out: the tenant stays active, the invitation nobody can use is
    // cancelled, no email carries a link that leads nowhere, and a person is told.
    [Fact]
    public async Task CreateTenant_ClerkKeepsFailing_LeavesTheTenantActiveCancelsTheInvitationSendsNoEmailAndRaisesTheAlarm()
    {
        var admin = await _catalog.AddSystemAdminAsync();
        var owner = Email();
        _api.Identity.IsDown = true;
        using var alarms = new MetricCollector<long>(_api.Services.GetRequiredService<System.Diagnostics.Metrics.IMeterFactory>(), "Modules.ControlPlane", "tenant_creation.needs_attention");

        HttpResponseMessage created = null!;
        var messages = await _api.TrackMessagesAsync(async () => created = await CreateTenantAsync(admin, Slug(), owner));

        var tenantId = await IdOfCreatedAsync(created);
        messages.MovedToErrorQueue.SingleMessage<OpenOwnerSignUpCommand>().ShouldNotBeNull();
        (await TenantStatusAsync(tenantId)).ShouldBe("Active");
        (await InvitationStatusAsync(tenantId)).ShouldBe("Cancelled");
        (await SagaStateAsync(tenantId)).ShouldBe("NeedsAttention");
        _api.Email.Attempts.ShouldNotContain(attempt => attempt.Email.To == owner);
        alarms.GetMeasurementSnapshot().ShouldHaveSingleItem().Value.ShouldBe(1);
    }

    // The secret travels in the accept link, inside the tenant creation's messages (see the deviations list); the catalog keeps only its
    // hash.
    [Fact]
    public async Task CreateTenant_TheAcceptLinksSecret_IsStoredOnlyAsItsHash()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount($"user_{Guid.NewGuid():N}", email);
        await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));
        var secret = SecretOf(_api.Email.CodeSentTo(email));

        var stored = await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.invitations WHERE email = '{email}' AND secret_hash = encode(sha256(convert_to('{secret}', 'UTF8')), 'hex')");
        var holdingTheSecret = await database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.invitations WHERE position('{secret}' in invitations::text) > 0");

        stored.ShouldBe(1);
        holdingTheSecret.ShouldBe(0);
    }

    private static async Task<Guid> IdOfAsync(HttpResponseMessage created) =>
        (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();

    private static string Slug() => $"tenant-{Guid.NewGuid():N}"[..20];

    private static async Task<(string?, string?)> FieldCodesOfAsync(HttpResponseMessage response, string field)
    {
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        return (problem.GetProperty("code").GetString(), string.Join(", ", problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(code => code.GetString())));
    }

    private static async Task<Guid> IdOfCreatedAsync(HttpResponseMessage created)
    {
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
    }

    private static string Email() => $"{Guid.NewGuid():N}@example.com";

    private static string SecretOf(string code) => code[(code.IndexOf('.', StringComparison.Ordinal) + 1)..];

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> CountAsync(string sql) => database.ScalarAsSuperuserAsync<long>(sql);

    private Task<string?> StatusOfAsync(string slug) => database.ScalarAsync<string>($"SELECT status FROM control_plane.tenants WHERE slug = '{slug}'");

    private Task<HttpResponseMessage> CreateTenantAsync(string admin, string slug, string ownerEmail) =>
        _api.CreateClient(admin, secondFactor: true)
            .CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail });

    private async Task<Guid> InvitationIdAsync(Guid tenantId) =>
        await database.ScalarAsSuperuserAsync<Guid>($"SELECT id FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<string?> TenantStatusAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT status FROM control_plane.tenants WHERE id = '{tenantId}'");

    private Task<string?> InvitationStatusAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT status FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<string?> SagaStateAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT state FROM control_plane.tenant_creation_sagas WHERE id = '{tenantId}'");

    private async Task<string> CreateTenantWithOwnerAsync(string ownerEmail, string language = "en")
    {
        var slug = $"tenant-{Guid.NewGuid():N}"[..20];
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);
        (await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail, language })).EnsureSuccessStatusCode();

        return slug;
    }
}
