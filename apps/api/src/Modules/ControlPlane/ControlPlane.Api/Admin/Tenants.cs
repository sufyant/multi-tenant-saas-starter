using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using SharedKernel;

namespace ControlPlane.Api.Admin;

/// <summary>The tenants a system admin works with.</summary>
public static class Tenants
{
    public static void Map(RouteGroupBuilder admin) =>
        admin.MapPost("/tenants", CreateTenant).WithMetadata(new IdempotencyKeyRequired()).RequireAuthorization(Permissions.AdminTenantsCreate);

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
        return created.Map(tenant => new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.Status));
    }
}

/// <summary>A new tenant, and the email address of its first owner, who is invited once the tenant is ready.</summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail);

/// <summary>A tenant and where its creation stands: provisioning, active or cancelled.</summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, string Status);
