using System.Text.Json.Serialization;
using ControlPlane.Application.Ports;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's user object, as its Backend API returns it and as its user.created and user.updated events carry it. Only the fields the
// catalog needs are read. The email is the address marked primary, not the first in the list. Clerk's updated_at is in milliseconds.
internal sealed record ClerkUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("primary_email_address_id")] string? PrimaryEmailAddressId,
    [property: JsonPropertyName("email_addresses")] IReadOnlyList<ClerkEmailAddress> EmailAddresses,
    [property: JsonPropertyName("first_name")] string? FirstName,
    [property: JsonPropertyName("last_name")] string? LastName,
    [property: JsonPropertyName("updated_at")] long UpdatedAt)
{
    public IReadOnlyList<string> VerifiedEmails =>
        [.. EmailAddresses.Where(address => address.Verification?.Status == "verified").Select(address => address.EmailAddress)];

    public UserProfile Profile => new(
        EmailAddresses.FirstOrDefault(address => address.Id == PrimaryEmailAddressId)?.EmailAddress,
        FirstName,
        LastName,
        DateTimeOffset.FromUnixTimeMilliseconds(UpdatedAt));
}

internal sealed record ClerkEmailAddress(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("email_address")] string EmailAddress,
    [property: JsonPropertyName("verification")] ClerkVerification? Verification);

internal sealed record ClerkVerification([property: JsonPropertyName("status")] string Status);
