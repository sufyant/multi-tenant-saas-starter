using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Users;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me;

/// <summary>The signed-in user's own profile and preferences.</summary>
public static class MyProfile
{
    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet(string.Empty, GetMe);
        me.MapPatch("/preferences", ChangePreferences);
    }

    // Read without a tenant. A signed-in person the catalog does not know is not found: the web shows "no access yet".
    private static async Task<Result<MeResponse>> GetMe(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MeDetails>>(new GetMe(user.Id()), cancellationToken))
            .Map(me => new MeResponse(me.Id, me.Email, me.FirstName, me.LastName, me.IsSystemAdmin, me.Language, me.TimeZone));

    // Changing the preferences sets a state, so it takes no idempotency key (I1): the values the user has answer 200 again.
    private static async Task<Result<PreferencesResponse>> ChangePreferences(
        ChangePreferencesRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<PreferencesDetails>>(new ChangePreferences(user.Id(), request.Language, request.TimeZone), cancellationToken))
            .Map(preferences => new PreferencesResponse(preferences.Language, preferences.TimeZone));
}

/// <summary>
/// The signed-in user: the catalog's id, not the identity provider's, the copy of their profile, whether they are a system admin, and
/// their preferences. It has no status: a suspended user is refused on every me endpoint (A9), and a deleted user cannot sign in.
/// </summary>
public sealed record MeResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    bool IsSystemAdmin,
    Language Language,
    string? TimeZone);

/// <summary>
/// The user's language, one of the list, and the IANA name of their time zone, such as <c>Europe/Dublin</c>, or null for "not set":
/// the web then uses the browser's. Both are required, so a client that leaves the time zone out never clears it by mistake.
/// </summary>
public sealed record ChangePreferencesRequest([property: JsonRequired] Language Language, [property: JsonRequired] string? TimeZone);

/// <summary>The user's preferences, which follow them to every tenant and device. A null time zone is not set.</summary>
public sealed record PreferencesResponse(Language Language, string? TimeZone);
