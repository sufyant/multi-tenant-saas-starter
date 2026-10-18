using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Tenant;
using ControlPlane.Application;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Ports;
using ControlPlane.Application.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me;

/// <summary>The invitations the signed-in user accepts.</summary>
public static class Invitations
{
    /// <summary>
    /// The rate limit policy of accepting an invitation, which the host defines: a guessed code costs a call to the identity provider
    /// before it is looked up, so the endpoint has a stricter limit than the others (API4).
    /// </summary>
    public const string AcceptRateLimit = "invitation-accept";

    // The preview costs a call to the identity provider for a guessed code too, so it shares the stricter limit of accepting.
    public static void Map(RouteGroupBuilder me)
    {
        me.MapPost("/invitations/accept", AcceptInvitation).RequireRateLimiting(AcceptRateLimit);
        me.MapPost("/invitations/preview", GetInvitationPreview).RequireRateLimiting(AcceptRateLimit);
    }

    // Accepting starts outside any tenant. The invitation code names the tenant, which is declared before the invitation is looked up
    // by its secret, so a wrong tenant, a wrong secret and a malformed code all answer the same 404; a request without a code, or with
    // an empty or blank one, is a bad request. The identity provider is asked first, so its call never runs while the acceptance holds
    // the invitation locked. A person the catalog does not know gives their first and last name; the email address is the invited one.
    private static async Task<Result<MyTenantResponse>> AcceptInvitation(
        AcceptInvitationRequest request,
        ClaimsPrincipal user,
        IIdentityProvider identity,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Errors.InvitationCodeRequired;
        }

        if (InvitationCode.Parse(request.Code) is not { } code)
        {
            return Errors.InvitationNotFound;
        }

        var verifiedEmails = await identity.FindVerifiedEmailsAsync(user.Id(), cancellationToken);
        var accepted = await bus.InvokeForTenantAsync<Result<TenantSummary>>(
            code.TenantId.ToString(),
            new AcceptInvitation(code.Secret, user.Id(), verifiedEmails, request.FirstName, request.LastName),
            cancellationToken);
        return accepted.Map(MyTenants.ToResponse);
    }

    // The accept screen before the person accepts. The code is the invitation's secret, so it travels in the body, never in the URL,
    // which access logs, the browser's history and the Referer header keep; the endpoint is a POST for that reason, and it changes
    // nothing. It reads the invitation as accepting does: the code's tenant is declared, then the invitation is found by the hash of the
    // secret (W3, T2), with the same answers for a missing, an unknown and a malformed code.
    private static async Task<Result<InvitationPreviewResponse>> GetInvitationPreview(
        GetInvitationPreviewRequest request,
        ClaimsPrincipal user,
        IIdentityProvider identity,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Errors.InvitationCodeRequired;
        }

        if (InvitationCode.Parse(request.Code) is not { } code)
        {
            return Errors.InvitationNotFound;
        }

        var verifiedEmails = await identity.FindVerifiedEmailsAsync(user.Id(), cancellationToken);
        var preview = await bus.InvokeForTenantAsync<Result<InvitationPreview>>(
            code.TenantId.ToString(),
            new GetInvitationPreview(code.Secret, verifiedEmails),
            cancellationToken);
        return preview.Map(found => new InvitationPreviewResponse(
            found.TenantName,
            new InviterNameResponse(
                ResponseStatus.OfInviter(found.Inviter.Kind),
                found.Inviter.Name?.FirstName,
                found.Inviter.Name?.LastName,
                found.Inviter.Name?.Deleted),
            Members.ToResponse(found.Role),
            found.ExpiresAt,
            ResponseStatus.OfInvitation(found.State)));
    }
}

/// <summary>
/// The invitation code from the invitation link, and the names of a person the catalog does not know yet: 1 to 100 characters each,
/// trimmed. A user the catalog knows sends none.
/// </summary>
public sealed record AcceptInvitationRequest(string Code, string? FirstName = null, string? LastName = null);

/// <summary>The invitation code from the invitation link.</summary>
public sealed record GetInvitationPreviewRequest(string Code);

/// <summary>
/// What the accept screen shows the invited person before they accept: the tenant's name, who invited, the role the invitation
/// gives, when it expires, and its state at the moment of the request: <c>pending</c>, <c>expired</c>, <c>accepted</c> or
/// <c>cancelled</c>. Only a caller with the invited address gets it.
/// </summary>
public sealed record InvitationPreviewResponse(
    string TenantName,
    InviterNameResponse InviterName,
    RoleResponse Role,
    DateTimeOffset ExpiresAt,
    InvitationResponseState State);

/// <summary>
/// Who invited: a user, with the names the catalog holds now and <c>deleted</c> for a user a system admin deleted, or the platform,
/// with no names and no <c>deleted</c>. The screen names the platform from <c>kind</c>, as the audit logs do.
/// </summary>
public sealed record InviterNameResponse(InviterNameResponseKind Kind, string? FirstName, string? LastName, bool? Deleted);

/// <summary>Who invited: a user of the tenant, or the platform, for which a system admin acted.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InviterNameResponseKind>))]
public enum InviterNameResponseKind
{
    [JsonStringEnumMemberName("user")]
    User,

    [JsonStringEnumMemberName("platform")]
    Platform,
}
