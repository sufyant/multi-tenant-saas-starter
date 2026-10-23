using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Users.ChangePreferences;
using ControlPlane.Application.Users.GetMe;
using ControlPlane.Application.Users.RenameMe;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.Users;

/// <summary>The signed-in user's own profile, names and preferences.</summary>
public static class MyProfileEndpoints
{
    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet(string.Empty, GetMe);
        me.MapPatch(string.Empty, RenameMe);
        me.MapPatch("/preferences", ChangePreferences);
    }

    // Read without a tenant. A signed-in person who is not a user yet is not found: the web shows "no access yet".
    private static async Task<Result<MeResponse>> GetMe(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MeView>>(new GetMeQuery(user.Id()), cancellationToken)).Map(ToResponse);

    // Renaming sets a state, so it takes no idempotency key (I1): the names the user has answer 200 again. The token names the user, so a
    // user changes only their own names. It answers the user as GetMe reads them.
    private static async Task<Result<MeResponse>> RenameMe(
        RenameMeRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MeView>>(new RenameMeCommand(user.Id(), request.FirstName, request.LastName), cancellationToken)).Map(ToResponse);

    // Changing the preferences sets a state, so it takes no idempotency key (I1): the values the user has answer 200 again.
    private static async Task<Result<PreferencesResponse>> ChangePreferences(
        ChangePreferencesRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<PreferencesView>>(new ChangePreferencesCommand(user.Id(), request.Language, request.TimeZone), cancellationToken))
            .Map(ToResponse);

    private static MeResponse ToResponse(MeView me) => new(me.Id, me.Email, me.FirstName, me.LastName, me.IsSystemAdmin, me.Language, me.TimeZone);

    private static PreferencesResponse ToResponse(PreferencesView preferences) => new(preferences.Language, preferences.TimeZone);
}
