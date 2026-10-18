using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ControlPlane.Application.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;

namespace ControlPlane.IntegrationTests.Clerk;

// Clerk's Backend API is a system we do not own; these tests pin down the requests the adapter makes and how it reads the answers.
public sealed class ClerkIdentityProviderTests(Database database) : IDisposable
{
    private static readonly Uri AcceptLink = new("https://app.test/invitations/accept?code=t.s");

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly StubClerk _clerk = new();

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 17, 9, 0, 0, TimeSpan.Zero));

    public void Dispose() => _clerk.Dispose();

    [Fact]
    public async Task CallClerk_AnyRequest_IsAuthorizedWithTheSecretKey()
    {
        _clerk.Respond("""{"id":"user_1","email_addresses":[]}""");

        await Identity().FindVerifiedEmailsAsync("user_1", Cancellation);

        _clerk.Requests.ShouldHaveSingleItem().Authorization.ShouldBe("Bearer sk_test_secret");
    }

    [Fact]
    public async Task FindVerifiedEmails_SomeUnverified_ReturnsOnlyTheVerified()
    {
        _clerk.Respond("""
            {"id":"user_1","email_addresses":[
              {"email_address":"ada@example.com","verification":{"status":"verified"}},
              {"email_address":"ada@work.example","verification":{"status":"unverified"}},
              {"email_address":"ada@old.example","verification":null}]}
            """);

        var emails = await Identity().FindVerifiedEmailsAsync("user_1", Cancellation);

        emails.ShouldBe(["ada@example.com"]);
        _clerk.Requests.ShouldHaveSingleItem().Uri.ShouldBe(new Uri("https://api.clerk.test/v1/users/user_1"));
    }

    // Clerk sends no email and keeps no name: the invitation carries only the address, our accept link and our expiry.
    [Fact]
    public async Task OpenSignUp_EmailWithoutAccount_CreatesAnInvitationThatSendsNoEmail()
    {
        _clerk.Respond("[]");
        _clerk.Respond("""{"object":"invitation","id":"inv_1","email_address":"ada@example.com","status":"pending","url":"https://clerk.test/v1/tickets/accept?ticket=t_1"}""");

        var link = await Identity().OpenSignUpAsync("ada@example.com", AcceptLink, _time.GetUtcNow().AddDays(7), Cancellation);

        link.ShouldBe(new SignUpLink(new Uri("https://clerk.test/v1/tickets/accept?ticket=t_1"), "inv_1"));
        _clerk.Requests.Count.ShouldBe(2);
        _clerk.Requests[0].Method.ShouldBe(HttpMethod.Get);
        _clerk.Requests[0].Uri.ShouldBe(new Uri("https://api.clerk.test/v1/users?email_address=ada%40example.com"));
        _clerk.Requests[1].Method.ShouldBe(HttpMethod.Post);
        _clerk.Requests[1].Uri.ShouldBe(new Uri("https://api.clerk.test/v1/invitations"));
        JsonNode.DeepEquals(
            JsonNode.Parse(_clerk.Requests[1].Body!),
            JsonNode.Parse("""
                {"email_address":"ada@example.com","redirect_url":"https://app.test/invitations/accept?code=t.s","notify":false,
                 "ignore_existing":true,"expires_in_days":7}
                """)).ShouldBeTrue(_clerk.Requests[1].Body);
    }

    // A person with an account signs in and accepts; Clerk would refuse an invitation for their address.
    [Fact]
    public async Task OpenSignUp_EmailWithAccount_CreatesNoInvitationAndAnswersTheAcceptLink()
    {
        _clerk.Respond("""[{"id":"user_1","email_addresses":[]}]""");

        var link = await Identity().OpenSignUpAsync("ada@example.com", AcceptLink, _time.GetUtcNow().AddDays(7), Cancellation);

        link.ShouldBe(new SignUpLink(AcceptLink, null));
        _clerk.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    // Clerk counts an invitation's life in whole days. The link must not die before our invitation, so a part of a day counts as a day.
    [Theory]
    [InlineData(6.5, 7)]
    [InlineData(0.1, 1)]
    [InlineData(-1, 1)]
    [InlineData(400, 365)]
    public async Task OpenSignUp_Expiry_IsInWholeDaysRoundedUpWithinClerksRange(double daysLeft, int expected)
    {
        _clerk.Respond("[]");
        _clerk.Respond("""{"id":"inv_1","url":"https://clerk.test/v1/tickets/accept?ticket=t_1"}""");

        await Identity().OpenSignUpAsync("ada@example.com", AcceptLink, _time.GetUtcNow().AddDays(daysLeft), Cancellation);

        JsonNode.Parse(_clerk.Requests[1].Body!)!["expires_in_days"]!.GetValue<int>().ShouldBe(expected);
    }

    [Fact]
    public async Task WithdrawSignUp_PendingInvitation_RevokesIt()
    {
        _clerk.Respond("""{"id":"inv_1","status":"revoked","revoked":true}""");

        await Identity().WithdrawSignUpAsync("inv_1", Cancellation);

        var request = _clerk.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.ShouldBe(new Uri("https://api.clerk.test/v1/invitations/inv_1/revoke"));
    }

    // Clerk revokes only a pending invitation: one already accepted, revoked or expired answers 400, and an unknown one 404. Either way
    // nobody can sign up with it any more.
    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task WithdrawSignUp_InvitationNoLongerPending_IsDone(HttpStatusCode status)
    {
        _clerk.Respond("""{"errors":[{"code":"invitation_not_pending"}]}""", status);

        await Identity().WithdrawSignUpAsync("inv_1", Cancellation);

        _clerk.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task DeleteUser_KnownUser_DeletesIt()
    {
        _clerk.Respond("""{"id":"user_1","object":"user","deleted":true}""");

        await Identity().DeleteUserAsync("user_1", Cancellation);

        var request = _clerk.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.Uri.ShouldBe(new Uri("https://api.clerk.test/v1/users/user_1"));
    }

    // A retried deletion finds the user gone.
    [Fact]
    public async Task DeleteUser_UserAlreadyGone_IsDone()
    {
        _clerk.Respond("""{"errors":[{"code":"resource_not_found"}]}""", HttpStatusCode.NotFound);

        await Identity().DeleteUserAsync("user_1", Cancellation);

        _clerk.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task CallClerk_KeepsFailing_IsAnError()
    {
        _clerk.Respond("""{"errors":[{"code":"internal"}]}""", HttpStatusCode.InternalServerError);

        var delete = async () => await Identity().DeleteUserAsync("user_1", Cancellation);

        await delete.ShouldThrowAsync<HttpRequestException>();
    }

    // Every call to Clerk has a time limit, set from configuration.
    [Fact]
    public async Task CallClerk_SlowerThanTheTimeout_IsGivenUp()
    {
        _clerk.Delay = TimeSpan.FromSeconds(30);

        var find = async () => await Identity(timeout: "00:00:00.100").FindVerifiedEmailsAsync("user_1", Cancellation);

        await find.ShouldThrowAsync<TaskCanceledException>();
    }

    // The module's real adapter, as the host registers it, with Clerk replaced at the HTTP boundary.
    private IIdentityProvider Identity(string timeout = "00:00:10")
    {
        var services = database.BuildServices(
            services =>
            {
                services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => _clerk));
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(_time));
            },
            settings: new()
            {
                ["ControlPlane:Clerk:SecretKey"] = "sk_test_secret",
                ["ControlPlane:Clerk:BackendApiUrl"] = "https://api.clerk.test/v1/",
                ["ControlPlane:Clerk:Timeout"] = timeout,
            },
            realIdentityProvider: true);

        return services.GetRequiredService<IIdentityProvider>();
    }

    // Answers the requests in the order the test gives the answers; the last answer repeats.
    private sealed class StubClerk : HttpMessageHandler
    {
        private readonly Queue<(string Body, HttpStatusCode Status)> _answers = new();
        private (string Body, HttpStatusCode Status) _last = ("{}", HttpStatusCode.OK);

        public List<(HttpMethod Method, Uri Uri, string? Authorization, string? Body)> Requests { get; } = [];

        public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK) => _answers.Enqueue((body, status));

        public TimeSpan Delay { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            await Task.Delay(Delay, cancellationToken);
            _last = _answers.TryDequeue(out var answer) ? answer : _last;
            return new HttpResponseMessage(_last.Status) { Content = new StringContent(_last.Body, Encoding.UTF8, "application/json") };
        }
    }
}
