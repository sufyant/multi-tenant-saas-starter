using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Admin;

/// <summary>The tenants a system admin works with.</summary>
public static class Tenants
{
    public static void Map(RouteGroupBuilder admin)
    {
        admin.MapPost("/tenants", CreateTenant).WithMetadata(new IdempotencyKeyRequired()).RequireAuthorization(Permissions.AdminTenantsCreate);
        admin.MapGet("/tenants", ListTenants).RequireAuthorization(Permissions.AdminTenantsRead);
        admin.MapPost("/tenants/{tenantId:guid}/suspend", SuspendTenant).RequireAuthorization(Permissions.AdminTenantsSuspend);
        admin.MapPost("/tenants/{tenantId:guid}/reactivate", ReactivateTenant).RequireAuthorization(Permissions.AdminTenantsReactivate);
    }

    // Tenant creation runs inside the tenant it creates. The tenant's id is chosen here, by the server, never by the client. The client
    // gives each new tenant an idempotency key; the same key with the same request answers with the tenant the first one created.
    private static async Task<Result<TenantResponse>> CreateTenant(
        CreateTenantRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        var created = await idempotency.ExecuteAsync<TenantDetails>(
            new CreateTenant(user.Id(), request.Name, request.Slug, request.OwnerEmail),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken);
        return created.Map(ToResponse);
    }

    // The list reads across tenants, so it runs without one.
    private static async Task<Result<ListPage<TenantResponse>>> ListTenants(
        int? page,
        int? pageSize,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var paging = PageRequest.Create(page, pageSize);
        if (!paging.IsSuccess)
        {
            return paging.Error;
        }

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantDetails>>>(new ListTenants(paging.Value), cancellationToken);
        return tenants.Map(list => list.Map(ToResponse));
    }

    // Suspension and reactivation run inside the tenant the route names (W3). They set a state, so they take no idempotency key (I1).
    private static async Task<Result<TenantResponse>> SuspendTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new SuspendTenant(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReactivateTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new ReactivateTenant(user.Id()), cancellationToken)).Map(ToResponse);

    private static TenantResponse ToResponse(TenantDetails tenant) => new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfTenant(tenant.Status));
}

/// <summary>A new tenant, and the email address of its first owner, who is invited once the tenant is ready.</summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail);

/// <summary>A tenant and its status.</summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, TenantResponseStatus Status);

/// <summary>The status of a tenant: provisioning until its creation ends, then active, suspended or cancelled.</summary>
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
}
