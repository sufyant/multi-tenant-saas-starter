using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CloseTenant;
using ControlPlane.Application.Tenants.CreateTenant;
using ControlPlane.Application.Tenants.GetTenant;
using ControlPlane.Application.Tenants.ListTenants;
using ControlPlane.Application.Tenants.ReactivateTenant;
using ControlPlane.Application.Tenants.RenameTenant;
using ControlPlane.Application.Tenants.ReopenTenant;
using ControlPlane.Application.Tenants.SuspendTenant;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform.Tenants;

/// <summary>The tenants a system admin works with.</summary>
public static class TenantEndpoints
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapPost("/tenants", CreateTenant)
            .WithMetadata(new IdempotencyKeyRequired(), CreatedLocation.Of<TenantResponse>(nameof(GetTenant), tenant => new { tenantId = tenant.Id }))
            .RequireAuthorization(Permissions.PlatformTenantsCreate)
            .Produces<TenantResponse>(StatusCodes.Status201Created);
        platform.MapGet("/tenants", ListTenants).RequireAuthorization(Permissions.PlatformTenantsRead);
        platform.MapGet("/tenants/{tenantId:guid}", GetTenant).WithName(nameof(GetTenant)).RequireAuthorization(Permissions.PlatformTenantsRead);
        platform.MapPatch("/tenants/{tenantId:guid}", RenameTenant).RequireAuthorization(Permissions.PlatformTenantsUpdate);
        platform.MapPost("/tenants/{tenantId:guid}/suspend", SuspendTenant).RequireAuthorization(Permissions.PlatformTenantsSuspend);
        platform.MapPost("/tenants/{tenantId:guid}/reactivate", ReactivateTenant).RequireAuthorization(Permissions.PlatformTenantsReactivate);
        platform.MapPost("/tenants/{tenantId:guid}/close", CloseTenant).RequireAuthorization(Permissions.PlatformTenantsClose);
        platform.MapPost("/tenants/{tenantId:guid}/reopen", ReopenTenant).RequireAuthorization(Permissions.PlatformTenantsReopen);
    }

    // Tenant creation runs inside the tenant it creates. The tenant's id is chosen here, by the server, never by the client. The client
    // gives each new tenant an idempotency key; the same key with the same request answers with the tenant the first one created. The
    // endpoint declares 201, so the host answers a new tenant with 201 and its address, where GetTenant reads it, and a replayed request
    // maps its saved result the same way (I5).
    private static async Task<Result<TenantResponse>> CreateTenant(
        CreateTenantRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<TenantView>(
            new CreateTenantCommand(user.Id(), request.Name, request.Slug, request.OwnerEmail, request.Language),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken);
        return created.Map(ToResponse);
    }

    // The list reads across tenants, so it runs without one. Each item comes from the tenant's row, with its summary (R12), and the user
    // of its first Owner: one query for the page.
    private static async Task<Result<ListPage<TenantListItemResponse>>> ListTenants(
        int? page,
        int? pageSize,
        ListSort<ListTenantsSort>? sort,
        string? q,
        ListFilter<TenantResponseStatus>? status,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var filter = ListFilter.Read(status, nameof(status));
        if (!filter.IsSuccess)
        {
            return filter.Error;
        }

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantListItemView>>>(
            new ListTenantsQuery(list.Value, filter.Value is { } chosen ? ResponseStatus.OfTenant(chosen) : null),
            cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }

    // One tenant is read inside the tenant the route names, as suspension does (W3); an id no tenant has is not found.
    private static async Task<Result<TenantDetailResponse>> GetTenant(Guid tenantId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetailView>>(tenantId.ToString(), new GetTenantQuery(), cancellationToken)).Map(ToResponse);

    // A change of a tenant's name or status runs inside the tenant the route names (W3). Each sets a state, so it takes no idempotency
    // key (I1).
    private static async Task<Result<TenantResponse>> RenameTenant(
        Guid tenantId,
        RenameTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantView>>(tenantId.ToString(), new RenameTenantCommand(user.Id(), request.Name), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> SuspendTenant(
        Guid tenantId,
        SuspendTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantView>>(tenantId.ToString(), new SuspendTenantCommand(user.Id(), request.Reason), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReactivateTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantView>>(tenantId.ToString(), new ReactivateTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> CloseTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantView>>(tenantId.ToString(), new CloseTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReopenTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantView>>(tenantId.ToString(), new ReopenTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static TenantResponse ToResponse(TenantView tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfTenant(tenant.Status), tenant.SuspensionReason, tenant.ClosedAt, tenant.PurgedAt);

    private static TenantListItemResponse ToResponse(TenantListItemView item) => new(
        item.Tenant.Id,
        item.Tenant.Name,
        item.Tenant.Slug,
        ResponseStatus.OfTenant(item.Tenant.Status),
        item.Tenant.SuspensionReason,
        item.Tenant.ClosedAt,
        item.Tenant.PurgedAt,
        item.ReopenableUntil,
        item.MemberCount,
        item.OwnerCount,
        item.OwnerInvitation is { } invitation ? ToResponse(invitation) : null);

    private static TenantDetailResponse ToResponse(TenantDetailView tenant) => new(
        tenant.Tenant.Id,
        tenant.Tenant.Name,
        tenant.Tenant.Slug,
        ResponseStatus.OfTenant(tenant.Tenant.Status),
        tenant.Tenant.SuspensionReason,
        tenant.Tenant.ClosedAt,
        tenant.Tenant.PurgedAt,
        tenant.ReopenableUntil,
        tenant.MemberCount,
        tenant.OwnerCount,
        [.. tenant.Owners.Select(ToResponse)],
        tenant.OwnerInvitation is { } invitation ? ToResponse(invitation) : null);

    private static TenantOwnerResponse ToResponse(TenantOwnerView owner) => new(owner.UserId, owner.FirstName, owner.LastName, owner.Email);

    private static OwnerInvitationResponse ToResponse(OwnerInvitationView invitation) =>
        new(invitation.Email, invitation.ExpiresAt, ResponseStatus.OfInvitation(invitation.State), invitation.Language);
}
