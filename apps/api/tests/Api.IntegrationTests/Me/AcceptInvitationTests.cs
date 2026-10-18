using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using Tenancy;

namespace Api.IntegrationTests.Me;

// People join a tenant only through an invitation.
public sealed class AcceptInvitationTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private readonly AuditLogs _audit = new(database);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 4, 9, 0, 0, TimeSpan.Zero));
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application), time: _time);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task AcceptInvitation_InvitedPerson_BecomesAMemberOfTheTenant()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();

        var accepted = await AcceptAsync(invitee, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tenant = await accepted.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        tenant.EnumerateObject().Select(field => field.Name).ShouldBe(["id", "name", "slug", "status", "role"], ignoreOrder: true);
        Roles.Of(tenant).ShouldBe(Roles.BuiltIn("owner"));
        tenant.GetProperty("id").GetGuid().ShouldBe(await database.ScalarAsync<Guid>($"SELECT id FROM control_plane.tenants WHERE slug = '{slug}'"));
        tenant.GetProperty("name").GetString().ShouldBe("Acme Ltd");
        tenant.GetProperty("slug").GetString().ShouldBe(slug);
        (await database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.memberships
            JOIN control_plane.tenants ON tenants.id = memberships.tenant_id JOIN control_plane.users ON users.id = memberships.user_id
            WHERE tenants.slug = '{slug}' AND users.external_id = '{invitee}'
            """)).ShouldBe(1);
    }

    // K3: the new member acted, and the record names only their catalog user id.
    [Fact]
    public async Task AcceptInvitation_InvitedPerson_IsRecordedInTheTenantLogAsJoined()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();

        await _api.WaitingForMessagesAsync(() => AcceptAsync(invitee, code));

        var tenantId = await database.ScalarAsync<Guid>($"SELECT id FROM control_plane.tenants WHERE slug = '{slug}'");
        var userId = await _catalog.UserIdOfAsync(invitee);
        (await _audit.TenantLogAsync(userId)).ShouldBe($"{tenantId}:member.joined|User|{userId}|User|{userId}|-");
        (await _audit.PlatformLogAsync(userId)).ShouldBeNull();
    }

    // K6: accepting changes the tenant aggregate, a membership and an invitation, so it locks the tenant row first. A suspension that
    // holds the row makes the acceptance wait, and the acceptance then finds the tenant suspended.
    [Fact]
    public async Task AcceptInvitation_WhileASuspensionHoldsTheTenant_WaitsAndIsThenAConflict()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        await using var suspension = new NpgsqlConnection(database.SuperuserConnectionString);
        await suspension.OpenAsync(Cancellation);
        await using var holding = await suspension.BeginTransactionAsync(Cancellation);
        await using var suspend = new NpgsqlCommand($"UPDATE control_plane.tenants SET status = 'Suspended' WHERE slug = '{slug}'", suspension, holding);
        await suspend.ExecuteNonQueryAsync(Cancellation);

        var accepting = AcceptAsync(invitee, code);
        await Waiting.UntilAsync(async () => accepting.IsCompleted || await IsBlockingAsync(suspension.ProcessID));
        await holding.CommitAsync(Cancellation);
        var accepted = await accepting;

        accepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(accepted)).ShouldBe("tenant.suspended");
        (await MembersAsync(slug)).ShouldBe(0);
    }

    // A8, K6: a system admin's deletion holds the user's row while the user accepts an invitation, which passed the door before the
    // deletion. The acceptance waits for the row, finds the user deleted, and adds no membership: a deleted user is never a member.
    [Fact]
    public async Task AcceptInvitation_WhileADeletionHoldsTheUser_WaitsAndIsRefused()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var known = await _catalog.AddUserAsync(email: email, firstName: "Grace");
        _api.Identity.AddAccount(known, email);
        var slug = await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));
        await using var deletion = new NpgsqlConnection(database.SuperuserConnectionString);
        await deletion.OpenAsync(Cancellation);
        await using var holding = await deletion.BeginTransactionAsync(Cancellation);
        await using var delete = new NpgsqlCommand(
            $"UPDATE control_plane.users SET email = NULL, first_name = NULL, deleted_at = now() WHERE external_id = '{known}'", deletion, holding);
        await delete.ExecuteNonQueryAsync(Cancellation);

        var accepting = AcceptAsync(known, _api.Email.CodeSentTo(email));
        await Waiting.UntilAsync(async () => accepting.IsCompleted || await IsBlockingAsync(deletion.ProcessID));
        await holding.CommitAsync(Cancellation);
        var accepted = await accepting;

        accepted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(accepted)).ShouldBe("user.deleted");
        (await MembersAsync(slug)).ShouldBe(0);
    }

    // K6, R13: one known user accepts invitations of two tenants at once. Each acceptance counts the membership on the user's row, so each
    // takes the row's update lock first, and they run one after the other. With a share lock, both would hold the row and then both wait
    // to update it: a deadlock, and one acceptance would fail. A holder keeps the row until both acceptances wait behind it.
    [Fact]
    public async Task AcceptInvitation_OneKnownUserIntoTwoTenantsAtOnce_JoinsBothAndCountsTwo()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var (firstSecret, secondSecret) = (Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"));
        var known = await _catalog.AddUserAsync(email: email, firstName: "Grace");
        _api.Identity.AddAccount(known, email);
        var first = await _catalog.AddTenantAsync();
        var second = await _catalog.AddTenantAsync();
        await _catalog.AddInvitationAsync(first.Id, email, secret: firstSecret);
        await _catalog.AddInvitationAsync(second.Id, email, secret: secondSecret);
        await using var holder = new NpgsqlConnection(database.SuperuserConnectionString);
        await holder.OpenAsync(Cancellation);
        await using var holding = await holder.BeginTransactionAsync(Cancellation);
        await using var hold = new NpgsqlCommand($"SELECT 1 FROM control_plane.users WHERE external_id = '{known}' FOR UPDATE", holder, holding);
        await hold.ExecuteScalarAsync(Cancellation);

        var accepting = Task.WhenAll(
            AcceptAsync(known, Catalog.InvitationCodeOf(first.Id, firstSecret)),
            AcceptAsync(known, Catalog.InvitationCodeOf(second.Id, secondSecret)));
        await Waiting.UntilAsync(async () => accepting.IsCompleted || await WaitingBehindAsync(holder.ProcessID) == 2);
        await holding.CommitAsync(Cancellation);
        var accepted = await accepting;

        accepted.Select(response => response.StatusCode).ShouldBe([HttpStatusCode.OK, HttpStatusCode.OK]);
        (await _catalog.StoredMembershipCountOfAsync(known)).ShouldBe(2);
        (await _catalog.CountedMembershipsOfAsync(known)).ShouldBe(2);
    }

    // A7: a new catalog user gets the invited address, which the identity provider verified, and the names the person typed, without the
    // spaces around them. The identity provider keeps no name.
    [Fact]
    public async Task AcceptInvitation_NewUser_StoresTheInvitedAddressAndTheTypedNames()
    {
        var invitee = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.com";
        await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));
        _api.Identity.AddAccount(invitee, email.ToUpperInvariant());

        var accepted = await _api.CreateClient(invitee).PostAsJsonAsync(
            "/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email), firstName = " Ada ", lastName = "Lovelace  " }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(invitee)).ShouldBe(new StoredUser(email, "Ada", "Lovelace", Deleted: false));
    }

    // Decision 6 of step 20j: a new user starts with the language of the invitation they accept, and with no time zone.
    [Fact]
    public async Task AcceptInvitation_NewUser_StartsWithTheInvitationsLanguage()
    {
        var invitee = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.com";
        await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email, language: "tr"));
        _api.Identity.AddAccount(invitee, email);

        var accepted = await _api.CreateClient(invitee).PostAsJsonAsync(
            "/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email), firstName = "Ada", lastName = "Lovelace" }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(invitee)).ShouldBe(new StoredUser(email, "Ada", "Lovelace", Deleted: false, "tr", null));
    }

    // A user the catalog knows keeps the language and the time zone they chose.
    [Fact]
    public async Task AcceptInvitation_KnownUser_KeepsTheirLanguageAndTimeZone()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var known = await _catalog.AddUserAsync(email: email, firstName: "Grace", lastName: "Hopper", language: "ar", timeZone: "Asia/Dubai");
        _api.Identity.AddAccount(known, email);
        await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email, language: "tr"));

        var accepted = await _api.CreateClient(known).PostAsJsonAsync("/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email) }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(known)).ShouldBe(new StoredUser(email, "Grace", "Hopper", Deleted: false, "ar", "Asia/Dubai"));
    }

    // A person the catalog does not know gives both names; a missing one is a field that failed validation (K5). The invitation stays
    // pending, and no user is written.
    [Fact]
    public async Task AcceptInvitation_NewUserWithoutNames_IsABadRequestThatNamesEachMissingField()
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();

        var accepted = await _api.CreateClient(invitee).PostAsJsonAsync("/v1/me/invitations/accept", new { code }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await accepted.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("code").GetString().ShouldBe("request.validation_failed");
        problem.GetProperty("errors").GetProperty("firstName").EnumerateArray().Select(error => error.GetString()).ShouldBe(["required"]);
        problem.GetProperty("errors").GetProperty("lastName").EnumerateArray().Select(error => error.GetString()).ShouldBe(["required"]);
        (await _catalog.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{invitee}'")).ShouldBe(0);
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("   ", "required")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "too_long")]
    public async Task AcceptInvitation_NewUserWithAWrongName_IsABadRequestForThatField(string firstName, string fieldCode)
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();

        var accepted = await _api.CreateClient(invitee).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName, lastName = "Lovelace" }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await accepted.Content.ReadFromJsonAsync<JsonElement>(Cancellation);
        problem.GetProperty("errors").GetProperty("firstName").EnumerateArray().Select(error => error.GetString()).ShouldBe([fieldCode]);
        (await _catalog.CountAsync($"SELECT count(*) FROM control_plane.users WHERE external_id = '{invitee}'")).ShouldBe(0);
    }

    // A user the catalog knows keeps their names: they need none to join another tenant.
    [Fact]
    public async Task AcceptInvitation_KnownUserWithoutNames_JoinsAndKeepsTheirNames()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        var known = await _catalog.AddUserAsync(email: email, firstName: "Grace", lastName: "Hopper");
        _api.Identity.AddAccount(known, email);
        await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));

        var accepted = await _api.CreateClient(known).PostAsJsonAsync("/v1/me/invitations/accept", new { code = _api.Email.CodeSentTo(email) }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _catalog.UserOfAsync(known)).ShouldBe(new StoredUser(email, "Grace", "Hopper", Deleted: false));
    }

    // API4: accepting an invitation has a stricter limit of its own, ten in a minute by default, on top of the general one; the
    // user's other requests still have the general limit left.
    [Fact]
    public async Task AcceptInvitation_OverItsOwnLimit_IsRejectedButTheUsersOtherRequestsAnswer()
    {
        var user = $"user_{Guid.NewGuid():N}";
        var code = $"{Guid.NewGuid()}.unknown-secret";
        var client = _api.CreateClient(user);
        HttpResponseMessage[] allowed = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.PostAsJsonAsync("/v1/me/invitations/accept", new { code }, Cancellation)));

        var rejected = await client.PostAsJsonAsync("/v1/me/invitations/accept", new { code }, Cancellation);
        var other = await client.GetAsync("/v1/me/tenants", Cancellation);

        allowed.Select(response => response.StatusCode).Distinct().ShouldBe([HttpStatusCode.NotFound]);
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        other.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // A suspended tenant takes no new member. The invitation stays pending, so its person can accept it once the tenant is
    // reactivated.
    [Fact]
    public async Task AcceptInvitation_ToASuspendedTenant_IsAConflictAndCanBeAcceptedAfterReactivation()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        await SetStatusAsync(slug, "Suspended");

        var refused = await AcceptAsync(invitee, code);
        var pendingAfterTheRefusal = await InvitationStatusAsync(slug);
        var membersAfterTheRefusal = await MembersAsync(slug);
        (await _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true)
            .PostAsync($"/v1/platform/tenants/{TenantIdOf(code)}/reactivate", null, Cancellation)).EnsureSuccessStatusCode();
        var accepted = await AcceptAsync(invitee, code);

        refused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(refused)).ShouldBe("tenant.suspended");
        pendingAfterTheRefusal.ShouldBe("Pending");
        membersAfterTheRefusal.ShouldBe(0);
        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MembersAsync(slug)).ShouldBe(1);
    }

    // As T3: only the invited person with a live invitation learns that the tenant is suspended. A stranger with the link keeps the
    // invitation's own answer, and an expired invitation stays dead after a reactivation.
    [Fact]
    public async Task AcceptInvitation_ToASuspendedTenantWithoutTheInvitedEmail_KeepsItsOwnError()
    {
        var (slug, _, code) = await InviteSomeoneWithAnAccountAsync();
        await SetStatusAsync(slug, "Suspended");
        var stranger = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(stranger, $"{Guid.NewGuid():N}@example.com");

        var accepted = await AcceptAsync(stranger, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.email_mismatch");
    }

    [Fact]
    public async Task AcceptInvitation_ExpiredToASuspendedTenant_KeepsItsOwnError()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        await SetStatusAsync(slug, "Suspended");
        _time.Advance(TimeSpan.FromDays(7));

        var accepted = await AcceptAsync(invitee, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.expired");
    }

    [Fact]
    public async Task AcceptInvitation_AlreadyUsedToASuspendedTenant_KeepsItsOwnError()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        var sameEmail = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(sameEmail, (await _api.Identity.FindVerifiedEmailsAsync(invitee, Cancellation))[0]);
        (await AcceptAsync(invitee, code)).EnsureSuccessStatusCode();
        await SetStatusAsync(slug, "Suspended");

        var reused = await AcceptAsync(sameEmail, code);

        reused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(reused)).ShouldBe("invitation.not_pending");
    }

    // T5: system admins are not tenant members, even with the code and the invited email address verified.
    [Fact]
    public async Task AcceptInvitation_ASystemAdmin_IsRefusedAndJoinsNothing()
    {
        var systemAdmin = await _catalog.AddSystemAdminAsync();
        var email = $"{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount(systemAdmin, email);
        var slug = await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));

        var accepted = await AcceptAsync(systemAdmin, _api.Email.CodeSentTo(email));

        accepted.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.system_admin_cannot_join");
        (await database.ScalarAsSuperuserAsync<long>(
            $"""
            SELECT count(*) FROM control_plane.memberships JOIN control_plane.tenants ON tenants.id = memberships.tenant_id
            WHERE tenants.slug = '{slug}'
            """)).ShouldBe(0);
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT invitations.status FROM control_plane.invitations JOIN control_plane.tenants ON tenants.id = invitations.tenant_id WHERE tenants.slug = '{slug}'"))
            .ShouldBe("Pending");
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

    // Clerk is asked before the acceptance locks the invitation, never while its transaction holds the lock.
    [Fact]
    public async Task AcceptInvitation_AskingTheIdentityProvider_HappensBeforeTheInvitationIsLocked()
    {
        var (slug, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        var tenantId = await database.ScalarAsync<Guid>($"SELECT id FROM control_plane.tenants WHERE slug = '{slug}'");
        var lockedWhileAsked = false;
        _api.Identity.WhileReadingVerifiedEmails = async () => lockedWhileAsked = await database.IsLockedAsync(
            tenantId, $"SELECT 1 FROM control_plane.invitations WHERE tenant_id = '{tenantId}' FOR UPDATE NOWAIT");

        var accepted = await AcceptAsync(invitee, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.OK);
        lockedWhileAsked.ShouldBeFalse();
    }

    [Fact]
    public async Task AcceptInvitation_CodeUsedTwice_IsRejected()
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        var sameEmail = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(sameEmail, (await _api.Identity.FindVerifiedEmailsAsync(invitee, Cancellation))[0]);
        await AcceptAsync(invitee, code);

        var reused = await AcceptAsync(sameEmail, code);

        reused.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(reused)).ShouldBe("invitation.not_pending");
    }

    [Fact]
    public async Task AcceptInvitation_AfterItExpires_IsAConflict()
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        _time.Advance(TimeSpan.FromDays(7));

        var accepted = await AcceptAsync(invitee, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.expired");
    }

    [Fact]
    public async Task AcceptInvitation_WithoutTheInvitedEmail_IsForbidden()
    {
        var (_, _, code) = await InviteSomeoneWithAnAccountAsync();
        var stranger = $"user_{Guid.NewGuid():N}";
        _api.Identity.AddAccount(stranger, $"{Guid.NewGuid():N}@example.com");

        var accepted = await AcceptAsync(stranger, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.email_mismatch");
    }

    [Fact]
    public async Task AcceptInvitation_UnknownCode_IsNotFound()
    {
        var (tenantId, _) = await _catalog.AddTenantAsync();

        var accepted = await AcceptAsync($"user_{Guid.NewGuid():N}", $"{tenantId}.no-such-secret");

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.not_found");
    }

    // A cancelled invitation answers like one that does not exist.
    [Fact]
    public async Task AcceptInvitation_Cancelled_IsNotFound()
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        await database.ScalarAsSuperuserAsync<object>($"UPDATE control_plane.invitations SET status = 'Cancelled' WHERE tenant_id = '{TenantIdOf(code)}'");

        var accepted = await AcceptAsync(invitee, code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.not_found");
    }

    // The tenant id in the code is not secret: with another tenant's id the right secret finds nothing, and the answer does not
    // tell which part was wrong.
    [Fact]
    public async Task AcceptInvitation_TheSecretWithAnotherTenantsId_IsNotFound()
    {
        var (_, invitee, code) = await InviteSomeoneWithAnAccountAsync();
        var (otherTenantId, _) = await _catalog.AddTenantAsync();

        var accepted = await AcceptAsync(invitee, $"{otherTenantId}.{SecretOf(code)}");

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.not_found");
        (await database.ScalarAsSuperuserAsync<string>(
            $"SELECT status FROM control_plane.invitations WHERE tenant_id = '{TenantIdOf(code)}'")).ShouldBe("Pending");
    }

    [Theory]
    [InlineData("no-dot-at-all")]
    [InlineData("not-a-uuid.k3J9xSecret")]
    [InlineData(".k3J9xSecret")]
    [InlineData("018f3a2c-7d4e-7a1b-9c2d-3e4f5a6b9b1e.")]
    public async Task AcceptInvitation_MalformedCode_IsNotFound(string code)
    {
        var accepted = await AcceptAsync($"user_{Guid.NewGuid():N}", code);

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.not_found");
    }

    // OWASP API8: a request that leaves out the code, sends it as null or sends it empty or blank is a bad request, one shape for
    // all of them, never a server error.
    [Theory]
    [InlineData("{}")]
    [InlineData("""{"code":null}""")]
    [InlineData("""{"code":""}""")]
    [InlineData("""{"code":"   "}""")]
    public async Task AcceptInvitation_WithoutACode_IsABadRequest(string body)
    {
        var accepted = await _api.CreateClient($"user_{Guid.NewGuid():N}")
            .PostAsync("/v1/me/invitations/accept", new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await CodeOfAsync(accepted)).ShouldBe("invitation.code_required");
    }

    // A body the endpoint cannot read at all is a bad request in every environment, never a server error.
    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{")]
    public async Task AcceptInvitation_WithoutAReadableBody_IsABadRequest(string body)
    {
        var accepted = await _api.CreateClient($"user_{Guid.NewGuid():N}")
            .PostAsync("/v1/me/invitations/accept", new StringContent(body, Encoding.UTF8, "application/json"), Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        accepted.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task AcceptInvitation_WithoutASignedInUser_IsUnauthorized()
    {
        var accepted = await _api.CreateClient().PostAsJsonAsync("/v1/me/invitations/accept", new { code = "any" }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // N5: accepting is the signed-in user's own work, so it is under /v1/me; the old path is gone. An empty code would answer 400 on
    // a live route, so the 404 can only come from the missing route.
    [Fact]
    public async Task AcceptInvitation_OnTheOldPath_IsNotFound()
    {
        var client = _api.CreateClient($"user_{Guid.NewGuid():N}");

        var accepted = await client.PostAsJsonAsync("/v1/invitations/accept", new { code = "" }, Cancellation);

        accepted.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Creating a tenant invites its first owner, here someone who already has an account, so the link carries our invitation
    // code.
    private async Task<(string Slug, string Invitee, string Code)> InviteSomeoneWithAnAccountAsync()
    {
        var invitee = $"user_{Guid.NewGuid():N}";
        var email = $"{Guid.NewGuid():N}@example.com";
        _api.Identity.AddAccount(invitee, email);
        var slug = await _api.WaitingForMessagesAsync(() => CreateTenantWithOwnerAsync(email));

        return (slug, invitee, _api.Email.CodeSentTo(email));
    }

    private async Task<string> CreateTenantWithOwnerAsync(string ownerEmail, string language = "en")
    {
        var slug = $"tenant-{Guid.NewGuid():N}"[..20];
        var admin = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);
        (await admin.CreateTenantAsync(new { name = "Acme Ltd", slug, ownerEmail, language })).EnsureSuccessStatusCode();

        return slug;
    }

    private async Task SetStatusAsync(string slug, string status) =>
        await database.ScalarAsync<object>($"UPDATE control_plane.tenants SET status = '{status}' WHERE slug = '{slug}'");

    private Task<string?> InvitationStatusAsync(string slug) =>
        database.ScalarAsSuperuserAsync<string>(
            $"SELECT invitations.status FROM control_plane.invitations JOIN control_plane.tenants ON tenants.id = invitations.tenant_id WHERE tenants.slug = '{slug}'");

    private Task<long> MembersAsync(string slug) =>
        database.ScalarAsSuperuserAsync<long>(
            $"SELECT count(*) FROM control_plane.memberships JOIN control_plane.tenants ON tenants.id = memberships.tenant_id WHERE tenants.slug = '{slug}'");

    // How many other sessions wait behind the holder's session: for a lock it holds, or for a session that waits behind it. A second
    // waiter for one row waits for the first waiter, which holds the row's place in the queue.
    private Task<long> WaitingBehindAsync(int holder) =>
        database.ScalarAsSuperuserAsync<long>(
            $"""
            WITH RECURSIVE behind (pid) AS (
                SELECT pid FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))
                UNION SELECT waiting.pid FROM pg_stat_activity AS waiting JOIN behind ON behind.pid = ANY(pg_blocking_pids(waiting.pid)))
            SELECT count(*) FROM behind
            """);

    // Whether another session waits for a lock that the holder's session holds.
    private async Task<bool> IsBlockingAsync(int holder) =>
        await database.ScalarAsSuperuserAsync<long>($"SELECT count(*) FROM pg_stat_activity WHERE {holder} = ANY(pg_blocking_pids(pid))") > 0;

    private Task<HttpResponseMessage> AcceptAsync(string userId, string code) =>
        _api.CreateClient(userId).PostAsJsonAsync("/v1/me/invitations/accept", new { code, firstName = "Ada", lastName = "Lovelace" }, Cancellation);

    // An invitation code is `<tenantId>.<secret>`.
    private static string TenantIdOf(string code) => code[..code.IndexOf('.', StringComparison.Ordinal)];

    private static string SecretOf(string code) => code[(code.IndexOf('.', StringComparison.Ordinal) + 1)..];

    private static async Task<string?> CodeOfAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation))!.Extensions["code"]?.ToString();
}
