using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Me.Tenants;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application;
using ControlPlane.Application.Identity;
using ControlPlane.Application.Invitations;
using ControlPlane.Application.Invitations.AcceptInvitation;
using ControlPlane.Application.Invitations.GetInvitationPreview;
using ControlPlane.Application.Tenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.Invitations;

/// <summary>The invitations the signed-in user accepts.</summary>
public static class MyInvitationEndpoints
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
    // the invitation locked. A person who is not a user yet gives their first and last name; the email address is the invited one.
    private static async Task<Result<MyTenantResponse>> AcceptInvitation(
        AcceptInvitationRequest request,
        ClaimsPrincipal user,
        IIdentityProvider identity,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var read = await ReadCodeAsync(request.Code, user, identity, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var (code, verifiedEmails) = read.Value;
        var accepted = await bus.InvokeForTenantAsync<Result<MyTenantView>>(
            code.TenantId.ToString(),
            new AcceptInvitationCommand(user.Id(), code.Secret, verifiedEmails, request.FirstName, request.LastName),
            cancellationToken);
        return accepted.Map(MyTenantEndpoints.ToResponse);
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
        var read = await ReadCodeAsync(request.Code, user, identity, cancellationToken);
        if (!read.IsSuccess)
        {
            return read.Error;
        }

        var (code, verifiedEmails) = read.Value;
        var preview = await bus.InvokeForTenantAsync<Result<InvitationPreviewView>>(
            code.TenantId.ToString(),
            new GetInvitationPreviewQuery(code.Secret, verifiedEmails),
            cancellationToken);
        return preview.Map(ToResponse);
    }

    // Both endpoints read the code the same way: a missing or blank code is a bad request, a code that names no tenant is not found, and
    // the identity provider then gives the caller's verified email addresses.
    private static async Task<Result<(InvitationCode Code, IReadOnlyList<string> VerifiedEmails)>> ReadCodeAsync(
        string? code,
        ClaimsPrincipal user,
        IIdentityProvider identity,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Errors.InvitationCodeRequired;
        }

        if (InvitationCode.Parse(code) is not { } parsed)
        {
            return Errors.InvitationNotFound;
        }

        return (parsed, await identity.FindVerifiedEmailsAsync(user.Id(), cancellationToken));
    }

    private static InvitationPreviewResponse ToResponse(InvitationPreviewView preview) => new(
        preview.TenantName,
        new InviterNameResponse(
            ResponseStatus.OfInviter(preview.Inviter.Kind),
            preview.Inviter.Name?.FirstName,
            preview.Inviter.Name?.LastName,
            preview.Inviter.Name?.Deleted),
        MemberEndpoints.ToResponse(preview.Role),
        preview.ExpiresAt,
        ResponseStatus.OfInvitation(preview.State));
}
