using System.Text.Json;
using System.Text.Json.Serialization;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Users;
using Microsoft.Extensions.Options;
using SharedKernel;
using Svix;
using Svix.Exceptions;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's webhook. Clerk sends it through Svix, and Svix's own library checks the signature and the timestamp's tolerance of five
// minutes. Only user.updated and user.deleted change the copy; user.created does not, because a Clerk account becomes a catalog user
// only through an invitation or as the first system admin, and those paths fill the copy themselves.
internal sealed class ClerkWebhook(IOptions<ClerkOptions> options) : IIdentityProviderWebhook
{
    private static readonly Error SignatureInvalid = Error.Validation("webhook.signature_invalid", "The webhook signature is missing, wrong or too old.");

    private readonly Lazy<Webhook> _webhook = new(() => new Webhook(options.Value.WebhookSigningSecret!));

    public Result<object?> Read(string payload, Func<string, string?> header)
    {
        try
        {
            _webhook.Value.Verify(payload, name => header(name!));
        }
        catch (WebhookVerificationException)
        {
            return SignatureInvalid;
        }

        var clerkEvent = JsonSerializer.Deserialize<ClerkEvent>(payload)!;
        return clerkEvent.Type switch
        {
            "user.updated" when clerkEvent.Data.Deserialize<ClerkUser>() is { } user => new UpdateUserProfile(user.Id, user.Profile),
            "user.deleted" when clerkEvent.Data.TryGetProperty("id", out var id) && id.GetString() is { } userId => new DeleteUser(userId),
            _ => (object?)null,
        };
    }

    // A signing secret is "whsec_" and a base64 key, as the Clerk dashboard shows it.
    public static bool IsSigningSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret) || !secret.StartsWith("whsec_", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            _ = new Webhook(secret);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or EmptyWebhookSecretException)
        {
            return false;
        }
    }

    private sealed record ClerkEvent([property: JsonPropertyName("type")] string Type, [property: JsonPropertyName("data")] JsonElement Data);
}
