using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.TermsVersions.AcceptTermsVersion;
using ControlPlane.Application.TermsVersions.DismissTermsNotice;
using ControlPlane.Application.TermsVersions.GetMyTerms;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>The terms the signed-in person must accept or read, after sign-in (docs/flows/terms-of-use.md). They run without a tenant.</summary>
public static class MyTermsEndpoints
{
    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet("/terms", GetMyTerms);
        me.MapPost("/terms/versions/{versionId:guid}/accept", AcceptTermsVersion);
        me.MapPost("/terms/versions/{versionId:guid}/dismiss", DismissTermsNotice);
    }

    // A signed-in person who is not a user yet must do nothing: they accept the version in force with their invitation.
    private static async Task<Result<MyTermsResponse>> GetMyTerms(ClaimsPrincipal user, IMessageBus bus, CancellationToken cancellationToken) =>
        ToResponse(await bus.InvokeAsync<MyTermsView>(new GetMyTermsQuery(user.Id()), cancellationToken));

    // An acceptance sets a state, so it takes no idempotency key (I1): a second one answers as the first. It answers what the person must
    // do next.
    private static async Task<Result<MyTermsResponse>> AcceptTermsVersion(
        Guid versionId,
        AcceptTermsVersionRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MyTermsView>>(new AcceptTermsVersionCommand(user.Id(), versionId, request.Url), cancellationToken)).Map(ToResponse);

    // A dismissal sets a state, so it takes no idempotency key (I1). It answers what the person must do next.
    private static async Task<Result<MyTermsResponse>> DismissTermsNotice(
        Guid versionId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<MyTermsView>>(new DismissTermsNoticeCommand(user.Id(), versionId), cancellationToken)).Map(ToResponse);

    private static MyTermsResponse ToResponse(MyTermsView terms) => new(
        ResponseStatus.OfMyTerms(terms.Action),
        terms.Version is { } version
            ? new MyTermsVersionResponse(version.Id, version.EffectiveDate, version.Url, version.Summary, version.RequiresAcceptance)
            : null);
}
