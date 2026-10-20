using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Platform.Invitations;

// POST /v1/platform/tenants/{tenantId}/invitations: a tenant has no Owner only while its first Owner has not accepted. The platform
// then invites an Owner. At most one platform Owner invitation is pending: a new one replaces the pending one, also to another address,
// and the old link answers as a cancelled one. A tenant with an Owner is refused, under the tenant's lock (K6j).
public sealed class InviteOwnerTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private readonly AuditEntries _audit = new(database);

    private readonly TenantSummaries _summaries = new(database);

    private ApiFactory _api = null!;

    private PlatformTenants _tenants = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        _tenants = new PlatformTenants(database, _api);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task InviteOwner_ATenantWithoutAnOwner_AnswersTheInvitationAndSendsItsEmail()
    {
        var admin = await SystemAdminAsync();
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        var owner = PlatformTenants.Email();

        var invited = await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, owner));

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        var invitation = await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        invitation.EnumerateObject().Select(field => field.Name)
            .ShouldBe(["id", "email", "role", "invitedBy", "invitedByName", "createdAt", "expiresAt", "state", "language"], ignoreOrder: true);
        invitation.GetProperty("state").GetString().ShouldBe("pending");
        (invitation.GetProperty("email").GetString(), Roles.Of(invitation)).ShouldBe((owner, Roles.BuiltIn("owner")));
        invitation.GetProperty("invitedByName").ToString().ShouldBe("""{"firstName":null,"lastName":null,"deleted":false}""");
        var id = invitation.GetProperty("id").GetGuid();
        (await _testData.InvitationStatusOfAsync(id)).ShouldBe("Pending");
        _api.Email.Sent.Where(email => email.To == owner).ShouldHaveSingleItem().IdempotencyKey.ShouldBe($"invite/{id}");
        (await _tenants.AcceptAsync(id, owner)).Accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        await _summaries.ShouldMatchTheRowsAsync(tenant);
    }

    // The invitation the tenant creation sent is the pending one; a new invitation to another address cancels it, and its link answers as a
    // cancelled one.
    [Fact]
    public async Task InviteOwner_AnotherEmail_CancelsThePendingInvitationAndItsLinkIsDead()
    {
        var admin = await SystemAdminAsync();
        var first = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, first);
        var replaced = await _tenants.LatestInvitationAsync(tenant);

        await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, PlatformTenants.Email()));

        (await _testData.InvitationStatusOfAsync(replaced)).ShouldBe("Cancelled");
        (await PendingInvitationsOfAsync(tenant)).ShouldBe(1);
        var accepted = await _tenants.AcceptAsync(replaced, first);
        accepted.Accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(accepted.Accepted)).ShouldBe("invitation.not_found");
        (await MembersOfAsync(tenant)).ShouldBe(0);
        _api.Identity.Withdrawn.Count.ShouldBe(1);
        await _summaries.ShouldMatchTheRowsAsync(tenant);
    }

    [Fact]
    public async Task InviteOwner_TheSameEmailAgain_ReplacesThePendingInvitation()
    {
        var admin = await SystemAdminAsync();
        var owner = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, owner);
        var replaced = await _tenants.LatestInvitationAsync(tenant);

        var invited = await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, owner.ToUpperInvariant()));

        var id = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        (await _testData.InvitationStatusOfAsync(replaced)).ShouldBe("Cancelled");
        (await _testData.InvitationStatusOfAsync(id)).ShouldBe("Pending");
        (await _tenants.AcceptAsync(replaced, owner)).Accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await _tenants.AcceptAsync(id, owner)).Accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InviteOwner_ATenantWithAnOwner_IsAConflictAndInvitesNobody()
    {
        var admin = await SystemAdminAsync();
        var owner = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, owner);
        await _tenants.AcceptAsync(await _tenants.LatestInvitationAsync(tenant), owner);

        var invited = await admin.InviteOwnerAsync(tenant, PlatformTenants.Email());

        invited.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(invited)).ShouldBe("tenant.has_owner");
        (await InvitationsOfAsync(tenant)).ShouldBe(1);
    }

    [Theory]
    [InlineData("Provisioning")]
    [InlineData("Suspended")]
    [InlineData("Cancelled")]
    [InlineData("Closed")]
    public async Task InviteOwner_ATenantThatIsNotActive_IsAConflictAndInvitesNobody(string status)
    {
        var tenant = await _testData.AddTenantAsync(status: status);

        var invited = await (await SystemAdminAsync()).InviteOwnerAsync(tenant.Id, PlatformTenants.Email());

        invited.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(invited)).ShouldBe("tenant.not_active");
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task InviteOwner_AnIdNoTenantHas_IsNotFound()
    {
        var invited = await (await SystemAdminAsync()).InviteOwnerAsync(Guid.NewGuid(), PlatformTenants.Email());

        invited.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(invited)).ShouldBe("tenant.not_found");
    }

    [Theory]
    [InlineData("not-an-email", "email_invalid")]
    [InlineData("", "required")]
    public async Task InviteOwner_AnEmailThatIsNoEmailAddress_IsABadRequest(string email, string fieldCode)
    {
        var tenant = await _testData.AddTenantAsync();

        var invited = await (await SystemAdminAsync()).InviteOwnerAsync(tenant.Id, email);

        invited.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("errors").GetProperty("email")[0].GetString().ShouldBe(fieldCode);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    // K8: without a language, the invitation takes the system admin's own; with one, it keeps it.
    [Theory]
    [InlineData(null, "tr")]
    [InlineData("ar", "ar")]
    public async Task InviteOwner_WithOrWithoutALanguage_KeepsTheLanguageOfItsEmail(string? language, string kept)
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(language: "tr"), secondFactor: true);
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        var owner = PlatformTenants.Email();

        await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, owner, language));

        (await _testData.InvitationLanguageOfAsync(owner)).ShouldBe(kept);
    }

    // K8: the answer and the tenant's detail carry the language the invitation keeps, so "Invite the Owner again" can send it back.
    [Fact]
    public async Task InviteOwner_InAnotherLanguage_AnswersItAndTheTenantDetailShowsIt()
    {
        var admin = await SystemAdminAsync();
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());

        var invited = await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, PlatformTenants.Email(), "tr"));

        invited.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("language").GetString().ShouldBe("tr");
        var found = await admin.GetFromJsonAsync<JsonElement>($"/v1/platform/tenants/{tenant}", Cancellation);
        found.GetProperty("ownerInvitation").GetProperty("language").GetString().ShouldBe("tr");
        await _summaries.ShouldMatchTheRowsAsync(tenant);
    }

    // I1: the endpoint creates an invitation, so the same key with the same request answers with the first invitation again.
    [Fact]
    public async Task InviteOwner_TheSameIdempotencyKeyTwice_CreatesOneInvitation()
    {
        var admin = await SystemAdminAsync();
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        var owner = PlatformTenants.Email();
        var key = Guid.NewGuid().ToString();

        var first = await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, owner, idempotencyKey: key));
        var again = await admin.InviteOwnerAsync(tenant, owner, idempotencyKey: key);

        again.StatusCode.ShouldBe(HttpStatusCode.Created);
        again.Headers.GetValues("Idempotent-Replayed").ShouldBe(["true"]);
        (await again.Content.ReadAsStringAsync(Cancellation)).ShouldBe(await first.Content.ReadAsStringAsync(Cancellation));
        (await InvitationsOfAsync(tenant)).ShouldBe(2);
    }

    // K3: the platform's log holds the system admin's action, and the tenant's log the same action by the platform. Both name the tenant
    // and hold the invitation's id and the one it replaced, never the address.
    [Fact]
    public async Task InviteOwner_ATenantWithoutAnOwner_RecordsItInBothLogs()
    {
        var adminId = await _testData.AddSystemAdminAsync();
        var admin = _api.CreateClient(adminId, secondFactor: true);
        var tenant = await _tenants.CreateAsync(admin, PlatformTenants.Email());
        var replaced = await _tenants.LatestInvitationAsync(tenant);
        var owner = PlatformTenants.Email();

        var invited = await _api.WaitingForMessagesAsync(() => admin.InviteOwnerAsync(tenant, owner));

        var id = (await invited.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("id").GetGuid();
        var admins = await _testData.UserIdOfAsync(adminId);
        (await _audit.PlatformLogAsync(tenant)).ShouldBe(
            $"tenant.created|User|{admins}|Tenant|{tenant}|Acme Ltd, tenant.owner_invited|User|{admins}|Tenant|{tenant}|Acme Ltd");
        (await _audit.TenantLogAsync(tenant)).ShouldBe(
            $"{tenant}:tenant.created|Platform|-|Tenant|{tenant}|Acme Ltd, {tenant}:tenant.owner_invited|Platform|-|Tenant|{tenant}|Acme Ltd");
        var details = $$"""{"invitationId": "{{id}}", "replacedInvitationId": "{{replaced}}"}""";
        (await _audit.PlatformDetailsAsync(tenant)).ShouldBe($"-, {details}");
        (await _audit.TenantDetailsAsync(tenant)).ShouldBe($"-, {details}");
        (await _audit.TenantRecordsHoldingAsync(tenant, owner)).ShouldBe(0);
    }

    [Fact]
    public void MapInviteOwner_TheEndpoint_RequiresItsPermissionAndAnIdempotencyKey()
    {
        var endpoint = _api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(found =>
            found.RoutePattern.RawText == "/v1/platform/tenants/{tenantId:guid}/invitations"
            && found.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains("POST"));

        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ShouldContain("platform.tenants.invite_owner");
        endpoint.Metadata.GetMetadata<IdempotencyKeyRequired>().ShouldNotBeNull();
    }

    [Fact]
    public async Task InviteOwner_AsATenantMember_IsForbidden()
    {
        var tenant = await _testData.AddTenantAsync();
        var member = _api.CreateClient(await _testData.AddMemberAsync(tenant.Id, role: "Owner"), secondFactor: true);

        var invited = await member.InviteOwnerAsync(tenant.Id, PlatformTenants.Email());

        invited.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await InvitationsOfAsync(tenant.Id)).ShouldBe(0);
    }

    // K6j: the acceptance and the new invitation both lock the tenant row first. The acceptance waits first and wins: the tenant has its
    // Owner, and the new invitation is refused.
    [Fact]
    public async Task AcceptAndInviteOwner_AtOnceWithTheAcceptanceFirst_KeepTheOwnerAndInviteNobody()
    {
        var admin = await SystemAdminAsync();
        var owner = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, owner);
        var invitation = await _tenants.LatestInvitationAsync(tenant);
        var userId = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(userId, owner);
        var code = _api.Email.CodeSentFor(invitation);

        var responses = await database.OneAfterTheOtherAsync(
            tenant, () => _tenants.AcceptAsync(userId, code), () => admin.InviteOwnerAsync(tenant, PlatformTenants.Email()));

        responses[0].StatusCode.ShouldBe(HttpStatusCode.OK);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(responses[1])).ShouldBe("tenant.has_owner");
        (await PendingInvitationsOfAsync(tenant)).ShouldBe(0);
        (await MembersOfAsync(tenant)).ShouldBe(1);
        await _summaries.ShouldMatchTheRowsAsync(tenant);
    }

    // The new invitation waits first and wins: the old link is dead, and nobody joins with it.
    [Fact]
    public async Task AcceptAndInviteOwner_AtOnceWithTheInvitationFirst_CancelTheOldLinkAndLeaveOnePendingInvitation()
    {
        var admin = await SystemAdminAsync();
        var owner = PlatformTenants.Email();
        var tenant = await _tenants.CreateAsync(admin, owner);
        var invitation = await _tenants.LatestInvitationAsync(tenant);
        var userId = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(userId, owner);
        var code = _api.Email.CodeSentFor(invitation);

        var responses = await database.OneAfterTheOtherAsync(
            tenant, () => admin.InviteOwnerAsync(tenant, PlatformTenants.Email()), () => _tenants.AcceptAsync(userId, code));

        responses[0].StatusCode.ShouldBe(HttpStatusCode.Created);
        responses[1].StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(responses[1])).ShouldBe("invitation.not_found");
        (await PendingInvitationsOfAsync(tenant)).ShouldBe(1);
        (await MembersOfAsync(tenant)).ShouldBe(0);
        await _summaries.ShouldMatchTheRowsAsync(tenant);
    }

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();

    private async Task<HttpClient> SystemAdminAsync() => _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

    private Task<long> InvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}'");

    private Task<long> PendingInvitationsOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.invitations WHERE tenant_id = '{tenantId}' AND status = 'Pending'");

    private Task<long> MembersOfAsync(Guid tenantId) =>
        database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM control_plane.memberships WHERE tenant_id = '{tenantId}'");
}
