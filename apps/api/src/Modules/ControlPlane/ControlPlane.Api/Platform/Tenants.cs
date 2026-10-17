using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Platform;

/// <summary>The tenants a system admin works with.</summary>
public static class Tenants
{
    public static void Map(RouteGroupBuilder platform)
    {
        platform.MapPost("/tenants", CreateTenant)
            .WithMetadata(new IdempotencyKeyRequired())
            .RequireAuthorization(Permissions.PlatformTenantsCreate)
            .Produces<TenantResponse>(StatusCodes.Status201Created);
        platform.MapGet("/tenants", ListTenants).RequireAuthorization(Permissions.PlatformTenantsRead);
        platform.MapGet("/tenants/{tenantId:guid}", GetTenant).RequireAuthorization(Permissions.PlatformTenantsRead);
        platform.MapPatch("/tenants/{tenantId:guid}", RenameTenant).RequireAuthorization(Permissions.PlatformTenantsUpdate);
        platform.MapPost("/tenants/{tenantId:guid}/suspend", SuspendTenant).RequireAuthorization(Permissions.PlatformTenantsSuspend);
        platform.MapPost("/tenants/{tenantId:guid}/reactivate", ReactivateTenant).RequireAuthorization(Permissions.PlatformTenantsReactivate);
        platform.MapPost("/tenants/{tenantId:guid}/close", CloseTenant).RequireAuthorization(Permissions.PlatformTenantsClose);
        platform.MapPost("/tenants/{tenantId:guid}/reopen", ReopenTenant).RequireAuthorization(Permissions.PlatformTenantsReopen);
    }

    // Tenant creation runs inside the tenant it creates. The tenant's id is chosen here, by the server, never by the client. The client
    // gives each new tenant an idempotency key; the same key with the same request answers with the tenant the first one created. The
    // endpoint declares 201, so the host answers a new tenant with 201, and a replayed request maps its saved result the same way (I5).
    private static async Task<Result<TenantResponse>> CreateTenant(
        CreateTenantRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<TenantDetails>(
            new CreateTenant(user.Id(), request.Name, request.Slug, request.OwnerEmail, request.Language),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken);
        return created.Map(ToResponse);
    }

    // The list reads across tenants, so it runs without one.
    private static async Task<Result<ListPage<TenantResponse>>> ListTenants(
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

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantDetails>>>(
            new ListTenants(list.Value, filter.Value is { } chosen ? ResponseStatus.OfTenant(chosen) : null),
            cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }

    // One tenant is read inside the tenant the route names, as suspension does (W3); an id no tenant has is not found.
    private static async Task<Result<TenantResponse>> GetTenant(Guid tenantId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new GetTenant(), cancellationToken)).Map(ToResponse);

    // A change of a tenant's name or status runs inside the tenant the route names (W3). Each sets a state, so it takes no idempotency
    // key (I1).
    private static async Task<Result<TenantResponse>> RenameTenant(
        Guid tenantId,
        RenameTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new RenameTenant(user.Id(), request.Name), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> SuspendTenant(
        Guid tenantId,
        SuspendTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new SuspendTenant(user.Id(), request.Reason), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReactivateTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new ReactivateTenant(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> CloseTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new CloseTenant(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReopenTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new ReopenTenant(user.Id()), cancellationToken)).Map(ToResponse);

    private static TenantResponse ToResponse(TenantDetails tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfTenant(tenant.Status), tenant.SuspensionReason, tenant.ClosedAt);
}

/// <summary>
/// A new tenant, the email address of its first owner, who is invited once the tenant is ready, and the language of the owner's
/// invitation email: without one, the system admin's own.
/// </summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail, Language? Language = null);

/// <summary>A tenant's new name. The slug never changes.</summary>
public sealed record RenameTenantRequest(string Name);

/// <summary>Why the system admin suspends the tenant: 1 to 500 characters, not counting the spaces around it. A member never sees it.</summary>
public sealed record SuspendTenantRequest(string Reason);

/// <summary>
/// A tenant and its status, with the reason of its suspension while it is suspended, or closed after a suspension, and the moment of its
/// closure while it is closed. Only a system admin sees this shape.
/// </summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, TenantResponseStatus Status, string? SuspensionReason, DateTimeOffset? ClosedAt);

/// <summary>The status of a tenant: provisioning until its creation ends, then active, suspended or closed, or cancelled.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantResponseStatus>))]
public enum TenantResponseStatus
{
    [JsonStringEnumMemberName("provisioning")]
    Provisioning,

    [JsonStringEnumMemberName("active")]
    Active,

    [JsonStringEnumMemberName("suspended")]
    Suspended,

    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    [JsonStringEnumMemberName("closed")]
    Closed,
}
