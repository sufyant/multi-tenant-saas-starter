using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Api.IntegrationTests.Authorization;

// The terms gate in the API (docs/flows/terms-of-use.md, section 8): when GetMyTerms would answer `acceptance`, a tenant user's call is
// refused with 403 and the version to accept, on every me and tenant endpoint but the exempt ones. The versions belong to no tenant, so
// each test runs on a database of its own (OwnTerms).
public sealed class TermsGateTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CallEndpoint_AVersionInForceNotAccepted_RefusesEveryEndpointButTheExemptOnes()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var endpoints = TermsGateRoutes.MeAndTenantEndpoints(own.Api.Services);

        var (notRefused, exemptRefused) = await TermsGateRoutes.CallEveryAsync(person, tenantId, endpoints);

        endpoints.Count.ShouldBeGreaterThan(TermsGateRoutes.Exempt.Length);
        notRefused.ShouldBeEmpty();
        exemptRefused.ShouldBeEmpty();
    }

    [Fact]
    public async Task CallEndpoint_AVersionInForceNotAccepted_IsForbiddenWithTheVersion()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var answer = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        answer.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        RefusalOf(await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe($"{TermsGateRoutes.RefusalCode}:{versionId}");
    }

    [Fact]
    public async Task CallEndpoint_AfterTheAcceptance_IsAnswered()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var versionId = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var before = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        (await person.AcceptTermsVersionAsync(versionId)).EnsureSuccessStatusCode();
        var members = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);
        var tenants = await person.GetAsync("/v1/me/tenants", Cancellation);

        before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        members.StatusCode.ShouldBe(HttpStatusCode.OK);
        tenants.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A version that requires acceptance asks from the moment it is created, as the gate does (section 4, step 2).
    [Fact]
    public async Task CallEndpoint_AScheduledVersionRequiringAcceptance_IsRefusedBeforeItsDay()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var scheduled = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        var answer = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        RefusalOf(await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe($"{TermsGateRoutes.RefusalCode}:{scheduled}");
    }

    // The version in force first, then the scheduled one, as GetMyTerms asks.
    [Fact]
    public async Task CallEndpoint_TheVersionInForceAcceptedAndTheScheduledOneNot_NamesTheScheduledVersion()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var current = await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        var scheduled = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), "https://brand.example/terms/next");
        var first = await person.GetAsync("/v1/me/tenants", Cancellation);

        (await person.AcceptTermsVersionAsync(current)).EnsureSuccessStatusCode();
        var second = await person.GetAsync("/v1/me/tenants", Cancellation);

        RefusalOf(await first.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe($"{TermsGateRoutes.RefusalCode}:{current}");
        RefusalOf(await second.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).ShouldBe($"{TermsGateRoutes.RefusalCode}:{scheduled}");
        TermsRequests.AskOf(await person.MyTermsAsync()).ShouldBe($"acceptance:{scheduled}");
    }

    // A notice never refuses: the person reads it on Home.
    [Fact]
    public async Task CallEndpoint_VersionsRequiringNoAcceptance_RefuseNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        await own.Api.WaitingForMessagesAsync(async () =>
        {
            await own.Admin.AddTermsVersionAsync(TermsRequests.Today, requiresAcceptance: false);
            return await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10), "https://brand.example/terms/next", requiresAcceptance: false);
        });

        var answer = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        TermsRequests.AskOf(await person.MyTermsAsync()).ShouldStartWith("notice:");
    }

    [Fact]
    public async Task CallEndpoint_ACancelledVersionRequiringAcceptance_RefusesNothing()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        var scheduled = await own.Admin.AddTermsVersionAsync(TermsRequests.Today.AddDays(10));

        (await own.Admin.CancelTermsVersionAsync(scheduled)).EnsureSuccessStatusCode();
        var answer = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The gate comes after the membership: a caller who is no member learns nothing of the tenant (T3).
    [Fact]
    public async Task EnterTenant_ANonMemberWhoMustAccept_IsToldTheTenantIsNotFound()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (otherId, _) = await own.Data.AddTenantAsync();
        var (_, person) = await own.AddTenantUserAsync(tenantId);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var answer = await person.GetAsync($"/v1/tenants/{otherId}/members", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await TermsRequests.CodeOfAsync(answer)).ShouldBe("tenant.not_found");
    }

    // The user's own state comes first, as A9 says.
    [Fact]
    public async Task CallEndpoint_ASuspendedUserWhoMustAccept_IsToldTheUserIsSuspended()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        var (externalId, person) = await own.AddTenantUserAsync(tenantId);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);
        await own.Data.SuspendUserAsync(externalId);

        var answer = await person.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        (await TermsRequests.CodeOfAsync(answer)).ShouldBe("user.suspended");
    }

    // The platform has no gate (section 3): a system admin who is also a member keeps the platform's door.
    [Fact]
    public async Task CallPlatform_ASystemAdminWhoIsAMemberAndMustAccept_IsAnswered()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        var (tenantId, _) = await own.Data.AddTenantAsync();
        await own.Data.AddMemberAsync(tenantId, own.AdminId);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var platform = await own.Admin.GetAsync("/v1/platform/tenants", Cancellation);
        var tenant = await own.Admin.GetAsync($"/v1/tenants/{tenantId}/members", Cancellation);

        platform.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await TermsRequests.CodeOfAsync(tenant)).ShouldBe(TermsGateRoutes.RefusalCode);
    }

    // GetMyTerms asks nothing of a person who is not a user yet: they accept with their invitation (section 5 of the flow's Accept invitation).
    [Fact]
    public async Task ListMyTenants_APersonWhoIsNotAUserYet_IsAnswered()
    {
        await using var own = await OwnTerms.CreateAsync(database);
        await own.Admin.AddTermsVersionAsync(TermsRequests.Today);

        var answer = await own.Api.CreateClient($"user_{Guid.NewGuid():N}").GetAsync("/v1/me/tenants", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // The exempt endpoints are a list of the flow, so a new endpoint is exempt only by a change of that list.
    [Fact]
    public async Task ExemptFromTermsGate_TheMarkedEndpoints_AreTheFlowsList()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var marked = TermsGateRoutes.MarkedExempt(TermsGateRoutes.MeAndTenantEndpoints(api.Services));

        marked.ShouldBe(TermsGateRoutes.Exempt.Order(StringComparer.Ordinal), ignoreOrder: false);
    }

    private static string RefusalOf(JsonElement problem) =>
        $"{problem.GetProperty("code").GetString()}:{problem.GetProperty("termsVersionId").GetGuid()}";
}
