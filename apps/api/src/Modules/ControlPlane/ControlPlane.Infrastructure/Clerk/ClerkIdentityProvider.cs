using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ControlPlane.Application.Identity;

namespace ControlPlane.Infrastructure.Clerk;

// Clerk's Backend API, as far as signing people in needs it. A call that fails or runs out of time throws, so the message that made it
// is tried again. Clerk sends no email and keeps no name: an invitation has notify false and carries only the address, our accept link
// and the expiry.
internal sealed class ClerkIdentityProvider(HttpClient http, TimeProvider time) : IIdentityProvider
{
    // Clerk counts an invitation's life in whole days, from 1 to 365.
    private const int MaxInvitationDays = 365;

    // An address that belongs to an account needs no invitation: Clerk refuses one, and the person signs in. Any other address gets an
    // invitation even when Clerk holds one for it already (ignore_existing), as when two tenants invite the same person, or a replaced
    // invitation's withdrawal has not run yet. The link must not die before our invitation, so a part of a day counts as a day.
    public async Task<SignUpLink> OpenSignUpAsync(string email, Uri acceptLink, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        var accounts = await http.GetFromJsonAsync<IReadOnlyList<ClerkUser>>($"users?email_address={Uri.EscapeDataString(email)}", cancellationToken);
        if (accounts!.Count > 0)
        {
            return new SignUpLink(acceptLink, null);
        }

        var days = (int)Math.Clamp(Math.Ceiling((expiresAt - time.GetUtcNow()).TotalDays), 1, MaxInvitationDays);
        using var response = await http.PostAsJsonAsync(
            "invitations",
            new ClerkInvitationRequest(email, acceptLink.ToString(), Notify: false, IgnoreExisting: true, days),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var invitation = await response.Content.ReadFromJsonAsync<ClerkInvitation>(cancellationToken);
        return new SignUpLink(new Uri(invitation!.Url), invitation.Id);
    }

    // Clerk revokes only a pending invitation: one already accepted, revoked or expired answers 400, and an unknown one 404. Either way
    // nobody can sign up with it any more.
    public async Task WithdrawSignUpAsync(string invitationId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync($"invitations/{Uri.EscapeDataString(invitationId)}/revoke", content: null, cancellationToken);
        if (response.StatusCode is not (HttpStatusCode.BadRequest or HttpStatusCode.NotFound))
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<IReadOnlyList<string>> FindVerifiedEmailsAsync(string externalUserId, CancellationToken cancellationToken)
    {
        var user = await http.GetFromJsonAsync<ClerkUser>($"users/{Uri.EscapeDataString(externalUserId)}", cancellationToken);
        return user!.VerifiedEmails;
    }

    // Clerk's addresses of the user become the one address ControlPlane keeps (A7): it is added verified and primary, or made so when a
    // retried call added it already, and every other address is deleted. A linked Google or Microsoft account is an external account, not
    // an address, so it stays a sign-in method. An address already gone answers 404, which is done.
    public async Task ReplaceEmailAsync(string externalUserId, string email, CancellationToken cancellationToken)
    {
        var user = await http.GetFromJsonAsync<ClerkUser>($"users/{Uri.EscapeDataString(externalUserId)}", cancellationToken);
        var existing = user!.EmailAddresses.FirstOrDefault(address => string.Equals(address.EmailAddress, email, StringComparison.OrdinalIgnoreCase));
        if (existing is { Id: { } id })
        {
            using var made = await http.PatchAsJsonAsync($"email_addresses/{Uri.EscapeDataString(id)}", new ClerkPrimaryRequest(true, true), cancellationToken);
            made.EnsureSuccessStatusCode();
        }
        else
        {
            using var added = await http.PostAsJsonAsync("email_addresses", new ClerkEmailAddressRequest(externalUserId, email, true, true), cancellationToken);
            added.EnsureSuccessStatusCode();
        }

        foreach (var other in user.EmailAddresses.Where(address => address != existing && address.Id is not null))
        {
            using var deleted = await http.DeleteAsync($"email_addresses/{Uri.EscapeDataString(other.Id!)}", cancellationToken);
            if (deleted.StatusCode != HttpStatusCode.NotFound)
            {
                deleted.EnsureSuccessStatusCode();
            }
        }
    }

    // Clerk finds the users who have the address, verified or not, by the same search as a sign-up (OpenSignUpAsync). The person's own
    // account does not count.
    public async Task<bool> IsEmailTakenByAnotherAccountAsync(string email, string externalUserId, CancellationToken cancellationToken)
    {
        var accounts = await http.GetFromJsonAsync<IReadOnlyList<ClerkUser>>($"users?email_address={Uri.EscapeDataString(email)}", cancellationToken);
        return accounts!.Any(account => account.Id != externalUserId);
    }

    // A user already gone answers 404: a retried deletion is done.
    public async Task DeleteUserAsync(string externalUserId, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync($"users/{Uri.EscapeDataString(externalUserId)}", cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed record ClerkInvitationRequest(
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("redirect_url")] string RedirectUrl,
        [property: JsonPropertyName("notify")] bool Notify,
        [property: JsonPropertyName("ignore_existing")] bool IgnoreExisting,
        [property: JsonPropertyName("expires_in_days")] int ExpiresInDays);

    private sealed record ClerkEmailAddressRequest(
        [property: JsonPropertyName("user_id")] string UserId,
        [property: JsonPropertyName("email_address")] string EmailAddress,
        [property: JsonPropertyName("verified")] bool Verified,
        [property: JsonPropertyName("primary")] bool Primary);

    private sealed record ClerkPrimaryRequest(
        [property: JsonPropertyName("verified")] bool Verified,
        [property: JsonPropertyName("primary")] bool Primary);

    private sealed record ClerkInvitation([property: JsonPropertyName("id")] string Id, [property: JsonPropertyName("url")] string Url);
}
