using System.Text.Json.Serialization;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's user object, as its Backend API returns it. Only the verified email addresses are read: Clerk keeps no name of ours, and the
// catalog owns the names (A7).
internal sealed record ClerkUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email_addresses")] IReadOnlyList<ClerkEmailAddress> EmailAddresses)
{
    public IReadOnlyList<string> VerifiedEmails =>
        [.. EmailAddresses.Where(address => address.Verification?.Status == "verified").Select(address => address.EmailAddress)];
}

internal sealed record ClerkEmailAddress(
    [property: JsonPropertyName("email_address")] string EmailAddress,
    [property: JsonPropertyName("verification")] ClerkVerification? Verification);

internal sealed record ClerkVerification([property: JsonPropertyName("status")] string Status);
