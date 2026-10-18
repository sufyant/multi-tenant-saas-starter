using System.Text.Json.Serialization;

namespace ControlPlane.Infrastructure.Clerk;

internal sealed record ClerkEmailAddress(
    [property: JsonPropertyName("email_address")] string EmailAddress,
    [property: JsonPropertyName("verification")] ClerkVerification? Verification);
