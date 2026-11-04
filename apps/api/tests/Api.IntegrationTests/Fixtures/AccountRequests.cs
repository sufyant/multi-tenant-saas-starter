using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api.IntegrationTests.Fixtures;

// The person's own account through the API, as Account and security asks: a code for a new address or for the deletion of the account,
// and the code given back. The code is read from the email that carried it, as the person reads it.
internal static partial class AccountRequests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> RequestEmailChangeAsync(ApiFactory api, string user, string email) =>
        PostWithKeyAsync(api, user, "/v1/me/email-change", new { email });

    public static Task<HttpResponseMessage> ChangeEmailAsync(ApiFactory api, string user, string code) =>
        api.CreateClient(user).PostAsJsonAsync("/v1/me/email-change/confirm", new { code }, Cancellation);

    public static Task<HttpResponseMessage> RequestAccountDeletionAsync(ApiFactory api, string user) =>
        PostWithKeyAsync(api, user, "/v1/me/account-deletion", null);

    public static Task<HttpResponseMessage> ConfirmAccountDeletionAsync(ApiFactory api, string user, string code) =>
        api.CreateClient(user).PostAsJsonAsync("/v1/me/account-deletion/confirm", new { code }, Cancellation);

    // The six digits of the latest code that went to the address.
    public static string CodeSentTo(ApiFactory api, string email) =>
        Digits().Match(api.Email.Sent.Where(sent => sent.To == email && sent.IdempotencyKey.StartsWith("verification-code/", StringComparison.Ordinal)).Last().Text).Value;

    public static async Task<string?> CodeOfAsync(HttpResponseMessage answer) =>
        (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString();

    private static Task<HttpResponseMessage> PostWithKeyAsync(ApiFactory api, string user, string path, object? body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return api.CreateClient(user).SendAsync(request, Cancellation);
    }

    [GeneratedRegex(@"(?<!\d)\d{6}(?!\d)")]
    private static partial Regex Digits();
}
