using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Invitations;
using ControlPlane.Application.FeatureFlags;
using ControlPlane.Application.FeatureFlags.AddFeatureFlagTenant;
using ControlPlane.Application.FeatureFlags.GetFeatureFlag;
using ControlPlane.Application.FeatureFlags.GetFeatureFlagChangePreview;
using ControlPlane.Application.FeatureFlags.GetFeatureFlags;
using ControlPlane.Application.FeatureFlags.GetTenantFeatureFlags;
using ControlPlane.Application.FeatureFlags.RemoveFeatureFlagTenant;
using ControlPlane.Application.FeatureFlags.SetFeatureFlagEveryoneElse;
using ControlPlane.Application.FeatureFlags.SetFeatureFlagPercentage;
using ControlPlane.Application.Users;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.FeatureFlags;

/// <summary>
/// The feature flags of this environment, which a system admin reads and whose rules they change (docs/flows/feature-flags.md). A flag
/// belongs to no tenant, so every read and command runs without one, but the flags of one organisation, which run in it (W3). Each change
/// sets a state, so it takes no idempotency key (I1).
/// </summary>
public static class FeatureFlagEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapGet("/feature-flags", GetFeatureFlags).RequireAuthorization(Permissions.PlatformFlagsRead);
        platform.MapGet("/feature-flags/{key}", GetFeatureFlag).RequireAuthorization(Permissions.PlatformFlagsRead);
        platform.MapPut("/feature-flags/{key}/tenants/{tenantId:guid}", AddFeatureFlagTenant).RequireAuthorization(Permissions.PlatformFlagsUpdate);
        platform.MapDelete("/feature-flags/{key}/tenants/{tenantId:guid}", RemoveFeatureFlagTenant).RequireAuthorization(Permissions.PlatformFlagsUpdate);
        platform.MapPut("/feature-flags/{key}/percentage", SetFeatureFlagPercentage).RequireAuthorization(Permissions.PlatformFlagsUpdate);
        platform.MapPut("/feature-flags/{key}/everyone-else", SetFeatureFlagEveryoneElse).RequireAuthorization(Permissions.PlatformFlagsUpdate);
        platform.MapPost("/feature-flags/{key}/preview", GetFeatureFlagChangePreview).RequireAuthorization(Permissions.PlatformFlagsUpdate);
        platform.MapGet("/tenants/{tenantId:guid}/feature-flags", GetTenantFeatureFlags).RequireAuthorization(Permissions.PlatformFlagsRead);
    }

    private static async Task<Result<FeatureFlagsResponse>> GetFeatureFlags(IMessageBus bus, CancellationToken cancellationToken)
    {
        var flags = await bus.InvokeAsync<IReadOnlyList<FeatureFlagListItemView>>(new GetFeatureFlagsQuery(), cancellationToken);
        var live = flags.Where(flag => flag.RemovedAt is null).ToList();
        return new FeatureFlagsResponse(
            [.. flags.Select(ToResponse)],
            new FeatureFlagCountsResponse(
                live.Count,
                live.Count(flag => flag.Cleanup == "ReadyToRemove"),
                live.Count(flag => flag.Cleanup == "Unused"),
                flags.Count - live.Count));
    }

    private static async Task<Result<FeatureFlagDetailResponse>> GetFeatureFlag(string key, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<FeatureFlagDetailView>>(new GetFeatureFlagQuery(key), cancellationToken)).Map(ToResponse);

    private static async Task<Result<FeatureFlagDetailResponse>> AddFeatureFlagTenant(
        string key,
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<FeatureFlagDetailView>>(new AddFeatureFlagTenantCommand(user.Id(), key, tenantId), cancellationToken)).Map(ToResponse);

    private static async Task<Result<FeatureFlagDetailResponse>> RemoveFeatureFlagTenant(
        string key,
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<FeatureFlagDetailView>>(new RemoveFeatureFlagTenantCommand(user.Id(), key, tenantId), cancellationToken)).Map(ToResponse);

    private static async Task<Result<FeatureFlagDetailResponse>> SetFeatureFlagPercentage(
        string key,
        SetFeatureFlagPercentageRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<FeatureFlagDetailView>>(new SetFeatureFlagPercentageCommand(user.Id(), key, request.Percentage), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<FeatureFlagDetailResponse>> SetFeatureFlagEveryoneElse(
        string key,
        SetFeatureFlagEveryoneElseRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<FeatureFlagDetailView>>(new SetFeatureFlagEveryoneElseCommand(user.Id(), key, request.On), cancellationToken))
            .Map(ToResponse);

    // A preview only reads, so it takes no idempotency key (I1). It reads across tenants, so it runs without one.
    private static async Task<Result<FeatureFlagChangePreviewResponse>> GetFeatureFlagChangePreview(
        string key,
        GetFeatureFlagChangePreviewRequest request,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeAsync<Result<int>>(
            new GetFeatureFlagChangePreviewQuery(key, request.TenantToAdd, request.TenantToRemove, request.Percentage, request.EveryoneElse), cancellationToken))
            .Map(count => new FeatureFlagChangePreviewResponse(count));

    private static async Task<Result<IReadOnlyList<TenantFeatureFlagResponse>>> GetTenantFeatureFlags(Guid tenantId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantFeatureFlagView[]>>(tenantId.ToString(), new GetTenantFeatureFlagsQuery(), cancellationToken))
            .Map(flags => (IReadOnlyList<TenantFeatureFlagResponse>)[.. flags.Select(flag => new TenantFeatureFlagResponse(
                flag.Key, ResponseStatus.OfFeatureFlagType(flag.Type), ResponseStatus.OfFeatureFlagReason(flag.Reason), flag.Percentage))]);

    private static UserNameResponse? ToResponse(UserNameView? name) => name is null ? null : new UserNameResponse(name.Name, name.Deleted);

    private static FeatureFlagResponse ToResponse(FeatureFlagListItemView flag) => new(
        flag.Key,
        ResponseStatus.OfFeatureFlagType(flag.Type),
        flag.ListedCount,
        flag.Percentage,
        flag.EveryoneElse,
        ResponseStatus.OfFeatureFlagCleanup(flag.Cleanup),
        flag.CleanupSince,
        flag.RemovedAt,
        new FeatureFlagLastChangeResponse(flag.LastChangedAt, ToResponse(flag.LastChangedBy)));

    private static FeatureFlagDetailResponse ToResponse(FeatureFlagDetailView flag) => new(
        ToResponse(flag.Flag),
        flag.Description,
        flag.DefaultValue,
        flag.AddedAt,
        flag.AddedRevision,
        flag.RemovedRevision,
        [.. flag.Listed.Select(listed => new FeatureFlagTenantResponse(listed.TenantId, listed.Name, listed.ListedAt))],
        [.. flag.History.Select(change => new FeatureFlagChangeResponse(
            ResponseStatus.OfFeatureFlagChange(change.Kind),
            change.From,
            change.To,
            change.Tenant is { } tenant ? new FeatureFlagChangeTenantResponse(tenant.TenantId, tenant.Name) : null,
            change.Revision,
            change.At,
            ToResponse(change.Actor)))]);
}
