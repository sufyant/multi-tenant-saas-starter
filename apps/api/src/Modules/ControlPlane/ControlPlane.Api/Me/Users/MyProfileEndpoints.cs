using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Platform.Users;
using ControlPlane.Application.Users;
using ControlPlane.Application.Users.ChangeEmail;
using ControlPlane.Application.Users.ChangePreferences;
using ControlPlane.Application.Users.ConfirmAccountDeletion;
using ControlPlane.Application.Users.GetMe;
using ControlPlane.Application.Users.RenameMe;
using ControlPlane.Application.Users.RequestAccountDeletion;
using ControlPlane.Application.Users.RequestEmailChange;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.Users;

/// <summary>The signed-in user's own profile, names, preferences and address, and the deletion of their own account.</summary>
public static class MyProfileEndpoints
{
    internal const string NoLocation =
        "Answers 201 with the code's expiry and without Location: a code is never read back, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet(string.Empty, GetMe);
        me.MapPatch(string.Empty, RenameMe);
        me.MapPatch("/preferences", ChangePreferences);
        me.MapPost("/email-change", RequestEmailChange)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .Produces<VerificationCodeResponse>(StatusCodes.Status201Created);
        me.MapPost("/email-change/confirm", ChangeEmail);
        me.MapPost("/account-deletion", RequestAccountDeletion)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .Produces<VerificationCodeResponse>(StatusCodes.Status201Created);
        me.MapPost("/account-deletion/confirm", ConfirmAccountDeletion);
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

    // A code is sent, so the request takes an idempotency key (I1): a replay sends no second code, and "Send a new code" is a new key. The
    // token names the user, so a user changes only their own address (A7).
    private static async Task<Result<VerificationCodeResponse>> RequestEmailChange(
        RequestEmailChangeRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken) =>
        (await idempotency.ExecuteAsync<VerificationCodeView>(new RequestEmailChangeCommand(user.Id(), request.Email), null, cancellationToken)).Map(ToResponse);

    // The change sets a state, so it takes no idempotency key (I1). It answers the user as GetMe reads them, with the new address.
    private static async Task<Result<MeResponse>> ChangeEmail(
        ChangeEmailRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MeView>>(new ChangeEmailCommand(user.Id(), request.Code), cancellationToken)).Map(ToResponse);

    // The deletion's check runs before the code goes out, so the screen says at once why it cannot run: 409 user.last_owner names the
    // tenants of which the user is the last Owner, and 409 user.is_system_admin a system admin, whose rights go first. A code is sent, so
    // the request takes an idempotency key (I1).
    private static async Task<Result<VerificationCodeResponse>> RequestAccountDeletion(
        ClaimsPrincipal user,
        IMessageBus bus,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var me = await bus.InvokeAsync<Result<MeView>>(new GetMeQuery(user.Id()), cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error;
        }

        var check = await UserEndpoints.CheckDeletionAsync(bus, me.Value.Id, cancellationToken);
        if (!check.IsSuccess)
        {
            return check.Error;
        }

        return (await idempotency.ExecuteAsync<VerificationCodeView>(new RequestAccountDeletionCommand(user.Id()), null, cancellationToken)).Map(ToResponse);
    }

    // The deletion is immediate, after the code (docs/flows/user-deletion.md): the check, then the code, then the steps of a system admin's
    // deletion, by the user. A deletion sets a state, so it takes no idempotency key (I1).
    private static async Task<Result> ConfirmAccountDeletion(
        ConfirmAccountDeletionRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var me = await bus.InvokeAsync<Result<MeView>>(new GetMeQuery(user.Id()), cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error;
        }

        var memberships = await UserEndpoints.CheckDeletionAsync(bus, me.Value.Id, cancellationToken);
        if (!memberships.IsSuccess)
        {
            return memberships.Error;
        }

        var confirmed = await bus.InvokeAsync<Result>(new ConfirmAccountDeletionCommand(user.Id(), request.Code), cancellationToken);
        return confirmed.IsSuccess
            ? await UserEndpoints.RemoveAndDeleteAsync(bus, user.Id(), me.Value.Id, memberships.Value, cancellationToken)
            : confirmed;
    }

    private static VerificationCodeResponse ToResponse(VerificationCodeView code) => new(code.ExpiresAt);

    private static MeResponse ToResponse(MeView me) => new(me.Id, me.Email, me.FirstName, me.LastName, me.IsSystemAdmin, me.Language, me.TimeZone);

    private static PreferencesResponse ToResponse(PreferencesView preferences) => new(preferences.Language, preferences.TimeZone);
}
