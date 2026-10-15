using System.Security.Claims;
using ControlPlane.Api.Authentication;
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

    private static readonly Error InvitationNotFound = Error.NotFound("invitation.not_found", "The invitation was not found.");

    private static readonly Error InvitationCodeRequired = Error.Validation("invitation.code_required", "The request has no invitation code.");

    public static void Map(RouteGroupBuilder me) =>
        me.MapPost("/invitations/accept", AcceptInvitation).RequireRateLimiting(AcceptRateLimit);

    // Accepting starts outside any tenant. The invitation code names the tenant, which is declared before the invitation is looked up
    // by its secret, so a wrong tenant, a wrong secret and a malformed code all answer the same 404; a request without a code, or with
    // an empty or blank one, is a bad request. The identity provider is asked first, so its call never runs while the acceptance holds
    // the invitation locked.
    private static async Task<Result<MyTenantResponse>> AcceptInvitation(
        AcceptInvitationRequest request,
        ClaimsPrincipal user,
        IIdentityProvider identity,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return InvitationCodeRequired;
        }

        if (InvitationCode.Parse(request.Code) is not { } code)
        {
            return InvitationNotFound;
        }

        var account = await identity.FindAccountAsync(user.Id(), cancellationToken);
        var accepted = await bus.InvokeForTenantAsync<Result<TenantSummary>>(
            code.TenantId.ToString(), new AcceptInvitation(code.Secret, user.Id(), account.VerifiedEmails, account.Profile), cancellationToken);
        return accepted.Map(tenant => new MyTenantResponse(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status)));
    }
}

/// <summary>The invitation code from the invitation link.</summary>
public sealed record AcceptInvitationRequest(string Code);
