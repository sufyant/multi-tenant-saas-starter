using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Tenancy;

namespace Api.IntegrationTests.Me;

// POST /v1/me/invitations/preview: the accept screen names the organisation, the inviter and the role before the person accepts, and
// says when the link is expired, used or cancelled. The code is the invitation's secret, so it travels in the body, as for accepting.
// The preview finds the invitation as accepting does: the code's tenant is declared, then the invitation is found by the hash of the
// secret. Only the invited person learns more than the email told them: a caller without the invited address is refused before the
// state is read, with the body accepting gives (the maintainer's decision in step 20l).
public sealed class GetInvitationPreviewTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly DateTimeOffset Later = DateTimeOffset.UtcNow.AddDays(3);

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetInvitationPreview_APendingInvitationForTheCaller_NamesTheTenantTheInviterAndTheRole()
    {
        var tenant = await _catalog.AddTenantAsync(name: "Preview Ltd");
        var ada = await _catalog.AddMemberAsync(tenant.Id, await _catalog.AddUserAsync(firstName: "Ada", lastName: "Lovelace"), role: "Owner");
        var invitation = await InvitationAsync(tenant.Id, role: "Admin", invitedBy: ada);

        var response = await PreviewAsync(invitation.Invitee, invitation.Code);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await response.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        preview.EnumerateObject().Select(field => field.Name).ShouldBe(["tenantName", "inviterName", "role", "expiresAt", "state"], ignoreOrder: true);
        preview.GetProperty("tenantName").GetString().ShouldBe("Preview Ltd");
        preview.GetProperty("inviterName").ToString().ShouldBe("""{"kind":"user","firstName":"Ada","lastName":"Lovelace","deleted":false}""");
        Roles.Of(preview).ShouldBe(Roles.BuiltIn("admin"));
        preview.GetProperty("expiresAt").GetDateTimeOffset().ShouldBe(Later, TimeSpan.FromMilliseconds(1));
        preview.GetProperty("state").GetString().ShouldBe("pending");
    }

    // The screen says which: a pending invitation past its expiry is expired, read from the expiry at the moment of the request.
    [Theory]
    [InlineData("Pending", 3, "pending")]
    [InlineData("Pending", -1, "expired")]
    [InlineData("Accepted", 3, "accepted")]
    [InlineData("Accepted", -1, "accepted")]
    [InlineData("Cancelled", 3, "cancelled")]
    [InlineData("Cancelled", -1, "cancelled")]
    public async Task GetInvitationPreview_AnInvitationInEachState_SaysWhichState(string status, int days, string state)
    {
        var tenant = await _catalog.AddTenantAsync();
        var invitation = await InvitationAsync(tenant.Id, status: status, expiresAt: DateTimeOffset.UtcNow.AddDays(days));

        var preview = await (await PreviewAsync(invitation.Invitee, invitation.Code)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        preview.GetProperty("state").GetString().ShouldBe(state);
    }

    // The invitation of a suspended tenant stays pending, and accepting it answers 409 tenant.suspended until the tenant is reactivated.
    [Fact]
    public async Task GetInvitationPreview_AnInvitationOfASuspendedTenant_IsPending()
    {
        var tenant = await _catalog.AddTenantAsync(status: "Suspended");
        var invitation = await InvitationAsync(tenant.Id);

        var preview = await (await PreviewAsync(invitation.Invitee, invitation.Code)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        preview.GetProperty("state").GetString().ShouldBe("pending");
    }

    // A system admin invited the tenant's Owner: the screen names the platform, never the system admin.
    [Fact]
    public async Task GetInvitationPreview_AnInvitationThePlatformSent_NamesThePlatformAsTheInviter()
    {
        var tenant = await _catalog.AddTenantAsync();
        var admin = await _catalog.AddUserAsync(firstName: "Sys", lastName: "Admin");
        await database.ScalarAsync<object>(
            $"INSERT INTO control_plane.system_admins (user_id, granted_at) SELECT id, now() FROM control_plane.users WHERE external_id = '{admin}'");
        var invitation = await InvitationAsync(tenant.Id, role: "Owner", invitedBy: admin);

        var preview = await (await PreviewAsync(invitation.Invitee, invitation.Code)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        preview.GetProperty("inviterName").ToString().ShouldBe("""{"kind":"platform","firstName":null,"lastName":null,"deleted":null}""");
        Roles.Of(preview).ShouldBe(Roles.BuiltIn("owner"));
    }

    // A system admin's deletion erased the inviter's names (A8): the screen says "Deleted user", as for audit (K3).
    [Fact]
    public async Task GetInvitationPreview_AnInviterWhoWasDeleted_HasNoNameAndSaysDeleted()
    {
        var tenant = await _catalog.AddTenantAsync();
        var inviter = await _catalog.AddUserAsync(firstName: "Gone", lastName: "Away");
        var invitation = await InvitationAsync(tenant.Id, invitedBy: inviter);
        await _catalog.DeleteUserAsync(inviter);

        var preview = await (await PreviewAsync(invitation.Invitee, invitation.Code)).Content.ReadFromJsonAsync<JsonElement>(Cancellation);

        preview.GetProperty("inviterName").ToString().ShouldBe("""{"kind":"user","firstName":null,"lastName":null,"deleted":true}""");
    }

    // A forwarded link tells its holder only what the email told them. A caller without the invited address is refused before the state
    // is read, with the body accepting gives, so the answer is the same for every state, and it holds no address.
    [Theory]
    [InlineData("Pending", 3)]
    [InlineData("Pending", -1)]
    [InlineData("Cancelled", 3)]
    [InlineData("Accepted", 3)]
    public async Task GetInvitationPreview_ACallerWithoutTheInvitedEmail_IsForbiddenWithTheBodyAcceptingGives(string status, int days)
    {
        var tenant = await _catalog.AddTenantAsync();
        var invitation = await InvitationAsync(tenant.Id, status: status, expiresAt: DateTimeOffset.UtcNow.AddDays(days));
        var pending = await InvitationAsync(tenant.Id);
        var stranger = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(stranger, $"{Guid.NewGuid():N}@example.org");

        var preview = await PreviewAsync(stranger, invitation.Code);
        var accepted = await AcceptAsync(stranger, pending.Code);

        preview.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        accepted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var body = await ProblemBodies.WithoutTraceIdAsync(preview);
        body.ShouldBe(await ProblemBodies.WithoutTraceIdAsync(accepted));
        body.ShouldContain("\"code\":\"invitation.email_mismatch\"");
        body.ShouldNotContain("@");
    }

    // An unknown code cannot be probed: a wrong secret, a tenant that does not exist and a code that is no code all answer the body
    // accepting gives.
    [Theory]
    [InlineData("{tenant}.no-such-secret")]
    [InlineData("{other}.no-such-secret")]
    [InlineData("no-dot-at-all")]
    [InlineData("not-a-uuid.k3J9xSecret")]
    [InlineData("{tenant}.")]
    public async Task GetInvitationPreview_AnUnknownCode_IsNotFoundWithTheBodyAcceptingGives(string code)
    {
        var tenant = await _catalog.AddTenantAsync();
        var invitation = await InvitationAsync(tenant.Id);
        var unknown = code.Replace("{tenant}", tenant.Id.ToString(), StringComparison.Ordinal).Replace("{other}", Guid.NewGuid().ToString(), StringComparison.Ordinal);

        var preview = await PreviewAsync(invitation.Invitee, unknown);
        var accepted = await AcceptAsync(invitation.Invitee, unknown);

        preview.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await ProblemBodies.WithoutTraceIdAsync(preview)).ShouldBe(await ProblemBodies.WithoutTraceIdAsync(accepted));
        (await CodeOfAsync(preview)).ShouldBe("invitation.not_found");
    }

    // The secret with another tenant's id finds nothing: the tenant of the code is declared, and row level security hides the invitation.
    [Fact]
    public async Task GetInvitationPreview_TheSecretWithAnotherTenantsId_IsNotFound()
    {
        var tenant = await _catalog.AddTenantAsync();
        var other = await _catalog.AddTenantAsync();
        var secret = Guid.NewGuid().ToString("N");
        var invitation = await InvitationAsync(tenant.Id, secret: secret);

        var preview = await PreviewAsync(invitation.Invitee, Catalog.InvitationCodeOf(other.Id, secret));

        preview.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(preview)).ShouldBe("invitation.not_found");
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"code":null}""")]
    [InlineData("""{"code":""}""")]
    [InlineData("""{"code":"   "}""")]
    public async Task GetInvitationPreview_WithoutACode_IsABadRequest(string body)
    {
        var preview = await _api.CreateClient($"user_{Guid.NewGuid():N}")
            .PostAsync("/v1/me/invitations/preview", new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        preview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(preview)).ShouldBe("invitation.code_required");
    }

    // A body the endpoint cannot read, such as a code that is not text, is a bad request, never a server error.
    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("""{"code":42}""")]
    public async Task GetInvitationPreview_WithoutAReadableBody_IsABadRequest(string body)
    {
        var preview = await _api.CreateClient($"user_{Guid.NewGuid():N}")
            .PostAsync("/v1/me/invitations/preview", new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        preview.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(preview)).ShouldBe("request.invalid");
    }

    // A read: the invitation's row keeps its version, and neither log gets a record (K3).
    [Fact]
    public async Task GetInvitationPreview_APendingInvitation_ChangesNothingAndWritesNoAuditRecord()
    {
        var tenant = await _catalog.AddTenantAsync();
        var invitation = await InvitationAsync(tenant.Id);
        var version = await RowVersionOfAsync(invitation.Id);

        var preview = await _api.WaitingForMessagesAsync(() => PreviewAsync(invitation.Invitee, invitation.Code));

        preview.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowVersionOfAsync(invitation.Id)).ShouldBe(version);
        (await _catalog.InvitationStatusOfAsync(invitation.Id)).ShouldBe("Pending");
        (await _audit.TenantLogAsync(invitation.Id)).ShouldBeNull();
        (await _audit.TenantLogAsync(tenant.Id)).ShouldBeNull();
        (await _audit.PlatformLogAsync(tenant.Id)).ShouldBeNull();
    }

    // A9: the me door refuses a suspended user, as it refuses them accepting.
    [Fact]
    public async Task GetInvitationPreview_ASuspendedUser_IsForbidden()
    {
        var tenant = await _catalog.AddTenantAsync();
        var invitation = await InvitationAsync(tenant.Id);
        var suspended = await _catalog.AddUserAsync();
        await _catalog.SuspendUserAsync(suspended);
        _api.Identity.AddAccount(suspended, invitation.Email);

        var preview = await PreviewAsync(suspended, invitation.Code);

        preview.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(preview)).ShouldBe("user.suspended");
    }

    [Fact]
    public async Task GetInvitationPreview_WithoutASignedInUser_IsUnauthorized()
    {
        var preview = await _api.CreateClient().PostAsJsonAsync("/v1/me/invitations/preview", new { code = "any" }, Cancellation);

        preview.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // API4: a guessed code costs a call to the identity provider, as for accepting, so the preview shares accepting's stricter limit.
    [Fact]
    public async Task GetInvitationPreview_AfterAcceptingsLimit_IsRejectedButTheUsersOtherRequestsAnswer()
    {
        var user = $"user_{Guid.NewGuid():N}";
        var code = $"{Guid.NewGuid()}.unknown-secret";
        var client = _api.CreateClient(user);
        HttpResponseMessage[] allowed = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.PostAsJsonAsync("/v1/me/invitations/accept", new { code }, Cancellation)));

        var rejected = await client.PostAsJsonAsync("/v1/me/invitations/preview", new { code }, Cancellation);
        var other = await client.GetAsync("/v1/me/tenants", Cancellation);

        allowed.Select(response => response.StatusCode).Distinct().ShouldBe([HttpStatusCode.NotFound]);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // An invitation of the tenant with a secret of its own, to an address that the identity provider has verified for a new account.
    private async Task<(Guid Id, string Code, string Email, string Invitee)> InvitationAsync(
        Guid tenantId,
        string role = "Member",
        string status = "Pending",
        DateTimeOffset? expiresAt = null,
        string? invitedBy = null,
        string? secret = null)
    {
        secret ??= Guid.NewGuid().ToString("N");
        var email = $"{Guid.NewGuid():N}@example.com";
        var id = await _catalog.AddInvitationAsync(tenantId, email, role, status, expiresAt ?? Later, invitedBy: invitedBy, secret: secret);
        var invitee = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(invitee, email);

        return (id, Catalog.InvitationCodeOf(tenantId, secret), email, invitee);
    }

    private Task<HttpResponseMessage> PreviewAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/preview", new { code }, Cancellation);

    private Task<HttpResponseMessage> AcceptAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName = "Ada", lastName = "Lovelace" }, Cancellation);

    private Task<string?> RowVersionOfAsync(Guid invitationId) =>
        database.ScalarAsSuperuserAsync<string>($"SELECT xmin::text FROM control_plane.invitations WHERE id = '{invitationId}'");

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
