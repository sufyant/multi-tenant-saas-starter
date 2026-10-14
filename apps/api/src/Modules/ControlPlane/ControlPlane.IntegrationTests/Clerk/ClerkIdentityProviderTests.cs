using System.Net;
using System.Text;
using ControlPlane.Application.Ports;
using Microsoft.Extensions.DependencyInjection;

namespace ControlPlane.IntegrationTests.Clerk;

// Clerk's Backend API is a system we do not own; these tests pin down the requests the adapter makes and how it reads the answers.
public sealed class ClerkIdentityProviderTests(Database database) : IDisposable
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly StubClerk _clerk = new();

    public void Dispose() => _clerk.Dispose();

    [Fact]
    public async Task CallClerk_AnyRequest_IsAuthorizedWithTheSecretKey()
    {
        _clerk.Respond("""{"id":"user_1","email_addresses":[]}""");

        await Identity().FindAccountAsync("user_1", Cancellation);

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

        var account = await Identity().FindAccountAsync("user_1", Cancellation);

        account.VerifiedEmails.ShouldBe(["ada@example.com"]);
        _clerk.Requests.ShouldHaveSingleItem().Uri.ShouldBe(new Uri("https://api.clerk.test/v1/users/user_1"));
    }

    // The profile copy takes the address marked primary, not the first in the list, and Clerk's time of the user's last change.
    [Fact]
    public async Task FindAccount_PrimaryAddressNotFirst_ReturnsThePrimaryInTheProfile()
    {
        _clerk.Respond("""
            {"id":"user_1","primary_email_address_id":"idn_2","first_name":"Ada","last_name":"Lovelace","updated_at":1760432400123,
             "email_addresses":[
              {"id":"idn_1","email_address":"ada@old.example","verification":{"status":"verified"}},
              {"id":"idn_2","email_address":"ada@example.com","verification":{"status":"verified"}}],
             "image_url":"https://img.clerk.test/ada.png","phone_numbers":[{"phone_number":"+15550100"}],"public_metadata":{"plan":"x"}}
            """);

        var account = await Identity().FindAccountAsync("user_1", Cancellation);

        account.Profile.ShouldBe(new UserProfile("ada@example.com", "Ada", "Lovelace", DateTimeOffset.FromUnixTimeMilliseconds(1760432400123)));
    }

    [Fact]
    public async Task CallClerk_KeepsFailing_IsAnError()
    {
        _clerk.Respond("""{"errors":[{"code":"internal"}]}""", HttpStatusCode.InternalServerError);

        var find = async () => await Identity().FindAccountAsync("user_1", Cancellation);

        await find.ShouldThrowAsync<HttpRequestException>();
    }

    // Every call to Clerk has a time limit, set from configuration.
    [Fact]
    public async Task CallClerk_SlowerThanTheTimeout_IsGivenUp()
    {
        _clerk.Delay = TimeSpan.FromSeconds(30);

        var find = async () => await Identity(timeout: "00:00:00.100").FindAccountAsync("user_1", Cancellation);

        await find.ShouldThrowAsync<TaskCanceledException>();
    }

    // The module's real adapter, as the host registers it, with Clerk replaced at the HTTP boundary.
    private IIdentityProvider Identity(string timeout = "00:00:10")
    {
        var services = database.BuildServices(
            services => services.ConfigureHttpClientDefaults(client => client.ConfigurePrimaryHttpMessageHandler(() => _clerk)),
            settings: new()
            {
                ["ControlPlane:Clerk:SecretKey"] = "sk_test_secret",
                ["ControlPlane:Clerk:BackendApiUrl"] = "https://api.clerk.test/v1/",
                ["ControlPlane:Clerk:Timeout"] = timeout,
            },
            realIdentityProvider: true);

        return services.GetRequiredService<IIdentityProvider>();
    }

    private sealed class StubClerk : HttpMessageHandler
    {
        private string _body = "{}";
        private HttpStatusCode _status = HttpStatusCode.OK;

        public List<(HttpMethod Method, Uri Uri, string? Authorization, string? Body)> Requests { get; } = [];

        public void Respond(string body, HttpStatusCode status = HttpStatusCode.OK) => (_body, _status) = (body, status);

        public TimeSpan Delay { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add((
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.ToString(),
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));

            await Task.Delay(Delay, cancellationToken);
            return new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") };
        }
    }
}
