using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.TermsVersions;
using ControlPlane.Application.TermsVersions.CancelTermsVersion;
using ControlPlane.Application.TermsVersions.CreateTermsVersion;
using ControlPlane.Application.TermsVersions.GetTermsOfUse;
using ControlPlane.Application.TermsVersions.GetTermsVersion;
using ControlPlane.Application.TermsVersions.ListTermsVersionPeople;
using ControlPlane.Application.TermsVersions.UpdateTermsVersion;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.TermsVersions;

/// <summary>
/// The terms of use a system admin manages: the versions, the scheduled one's edit and cancellation, and each version's people
/// (docs/flows/terms-of-use.md). A version belongs to no tenant, so every read and command runs without one.
/// </summary>
public static class TermsEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/terms", GetTermsOfUse).RequireAuthorization(Permissions.PlatformTermsRead);
        platform.MapGet("/terms/versions/{versionId:guid}", GetTermsVersion).WithName(nameof(GetTermsVersion)).RequireAuthorization(Permissions.PlatformTermsRead);
        platform.MapGet("/terms/versions/{versionId:guid}/people", ListTermsVersionPeople).RequireAuthorization(Permissions.PlatformTermsRead);
        platform.MapPost("/terms/versions", CreateTermsVersion)
            .WithMetadata(new IdempotencyKeyRequired(), CreatedLocation.Of<TermsVersionResponse>(nameof(GetTermsVersion), version => new { versionId = version.Id }))
            .RequireAuthorization(Permissions.PlatformTermsCreate)
            .Produces<TermsVersionResponse>(StatusCodes.Status201Created);
        platform.MapPatch("/terms/versions/{versionId:guid}", UpdateTermsVersion).RequireAuthorization(Permissions.PlatformTermsUpdate);
        platform.MapPost("/terms/versions/{versionId:guid}/cancel", CancelTermsVersion).RequireAuthorization(Permissions.PlatformTermsCancel);
    }

    private static async Task<Result<TermsOfUseResponse>> GetTermsOfUse(IMessageBus bus, CancellationToken cancellationToken)
    {
        var terms = await bus.InvokeAsync<TermsOfUseView>(new GetTermsOfUseQuery(), cancellationToken);
        return new TermsOfUseResponse([.. terms.Versions.Select(ToResponse)], terms.ActiveUserCount);
    }

    private static async Task<Result<TermsVersionDetailResponse>> GetTermsVersion(Guid versionId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<TermsVersionDetailView>>(new GetTermsVersionQuery(versionId), cancellationToken))
            .Map(found => new TermsVersionDetailResponse(ToResponse(found.Version), found.ActiveUserCount));

    // K5's exception: a cursor, as the frames page the people with Load more. The status is required: the frames show the accepted people
    // and the others on two tabs, and a cursor holds a place in one of them.
    private static async Task<Result<CursorPage<TermsPersonResponse>>> ListTermsVersionPeople(
        Guid versionId,
        ListFilter<TermsPersonResponseStatus>? status,
        string? cursor,
        int? limit,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var page = CursorRequest.Create(cursor, limit);
        if (!page.IsSuccess)
        {
            return page.Error;
        }

        var filter = ListFilter.Read(status, nameof(status));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        if (filter.Value is not { } wanted)
        {
            return Error.Validation(PagingErrorCodes.FilterInvalid, $"The filter {nameof(status)} is required: one of {string.Join(", ", ListFilter.ValuesOf<TermsPersonResponseStatus>())}.");
        }

        var people = await bus.InvokeAsync<Result<CursorPage<TermsPersonView>>>(
            new ListTermsVersionPeopleQuery(versionId, wanted == TermsPersonResponseStatus.Accepted ? TermsPersonStatus.Accepted : TermsPersonStatus.NotYet, page.Value),
            cancellationToken);
        return people.Map(found => found.Map(person => new TermsPersonResponse(
            person.UserId,
            person.Name,
            person.Email,
            person.AcceptedAt is null ? TermsPersonResponseStatus.NotYet : TermsPersonResponseStatus.Accepted,
            person.AcceptedAt)));
    }

    // The client gives each new version an idempotency key (I1); the endpoint declares 201, with the version's address, where
    // GetTermsVersion reads it.
    private static async Task<Result<TermsVersionResponse>> CreateTermsVersion(
        CreateTermsVersionRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<TermsVersionView>(
            new CreateTermsVersionCommand(user.Id(), request.EffectiveDate, request.Url, request.Summary, request.RequiresAcceptance),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken);
        return created.Map(ToResponse);
    }

    // An edit sets a state, so it takes no idempotency key (I1).
    private static async Task<Result<TermsVersionResponse>> UpdateTermsVersion(
        Guid versionId,
        UpdateTermsVersionRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<TermsVersionView>>(
            new UpdateTermsVersionCommand(user.Id(), versionId, request.EffectiveDate, request.Url, request.Summary, request.RequiresAcceptance),
            cancellationToken)).Map(ToResponse);

    // A cancellation sets a state, so it takes no idempotency key (I1): a cancelled version answers as it is.
    private static async Task<Result<TermsVersionResponse>> CancelTermsVersion(
        Guid versionId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<TermsVersionView>>(new CancelTermsVersionCommand(user.Id(), versionId), cancellationToken)).Map(ToResponse);

    private static TermsVersionResponse ToResponse(TermsVersionView version) => new(
        version.Id,
        version.EffectiveDate,
        version.Url,
        version.Summary,
        version.RequiresAcceptance,
        ResponseStatus.OfTermsVersion(version.State),
        version.AcceptedCount,
        version.Locked,
        version.CreatedAt,
        version.CancelledAt);
}
