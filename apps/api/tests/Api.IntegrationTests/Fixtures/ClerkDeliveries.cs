using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Svix;

namespace Api.IntegrationTests.Fixtures;

// Clerk's webhook deliveries, as Svix sends them: the event's JSON, signed with the Svix library's own signing function. The secrets are
// random, made when the tests run, so no signing secret is in the repository.
internal static class ClerkDeliveries
{
    public const string Path = "/v1/webhooks/clerk";

    public static readonly string SigningSecret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    public static readonly string AnotherSecret = "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    // A delivery signed now with the API's secret, unless a test names another secret or time.
    public static HttpRequestMessage Signed(string payload, string? secret = null, DateTimeOffset? at = null)
    {
        var id = $"msg_{Guid.NewGuid():N}";
        var timestamp = at ?? DateTimeOffset.UtcNow;
        var request = Unsigned(payload);
        request.Headers.Add("svix-id", id);
        request.Headers.Add("svix-timestamp", timestamp.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("svix-signature", new Webhook(secret ?? SigningSecret).Sign(id, timestamp, payload));
        return request;
    }

    public static HttpRequestMessage Unsigned(string payload) =>
        new(HttpMethod.Post, Path) { Content = new StringContent(payload, Encoding.UTF8, "application/json") };

    // A user event as Clerk sends it. The primary address is the second in the list, and the user has fields the copy leaves out.
    public static string UserEvent(string type, string userId, string email, string? firstName, string? lastName, DateTimeOffset updatedAt) =>
        Event(type, new Dictionary<string, object?>
        {
            ["id"] = userId,
            ["object"] = "user",
            ["primary_email_address_id"] = "idn_primary",
            ["email_addresses"] = new object[]
            {
                new Dictionary<string, object?> { ["id"] = "idn_other", ["email_address"] = $"other.{email}", ["verification"] = new { status = "verified" } },
                new Dictionary<string, object?> { ["id"] = "idn_primary", ["email_address"] = email, ["verification"] = new { status = "verified" } },
            },
            ["first_name"] = firstName,
            ["last_name"] = lastName,
            ["updated_at"] = updatedAt.ToUnixTimeMilliseconds(),
            ["image_url"] = "https://img.clerk.test/user.png",
            ["phone_numbers"] = new object[] { new { phone_number = "+15550100" } },
            ["public_metadata"] = new { plan = "any" },
        });

    public static string UserUpdated(string userId, string email, string? firstName, string? lastName, DateTimeOffset updatedAt) =>
        UserEvent("user.updated", userId, email, firstName, lastName, updatedAt);

    // Clerk's user.deleted event carries only the deleted object's id.
    public static string UserDeleted(string userId) =>
        Event("user.deleted", new Dictionary<string, object?> { ["id"] = userId, ["object"] = "user", ["deleted"] = true });

    private static string Event(string type, object data) =>
        JsonSerializer.Serialize(new { type, @object = "event", timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), data });
}
