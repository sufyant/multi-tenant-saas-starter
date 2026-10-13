using System.Security.Claims;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;

namespace ControlPlane.Api.Admin;

/// <summary>The tenants a system admin works with.</summary>
public static class Tenants
{
    private const string IdempotencyKeyHeader = "Idempotency-Key";

    private static readonly Error IdempotencyKeyInvalid = Error.Validation(
        "idempotency_key_invalid", $"The {IdempotencyKeyHeader} header must hold 1 to 255 visible ASCII characters.");

    public static void Map(RouteGroupBuilder admin) =>
        admin.MapPost("/tenants", CreateTenant).RequireAuthorization(Permissions.AdminTenantsCreate);

    // Tenant creation runs inside the tenant it creates. The tenant's id is chosen here, by the server, never by the client. The client
    // gives each new tenant an idempotency key; the same key with the same request answers with the tenant the first one created.
    private static async Task<Result<TenantResponse>> CreateTenant(
        CreateTenantRequest request,
        [FromHeader(Name = IdempotencyKeyHeader)] string? idempotencyKey,
        ClaimsPrincipal user,
        IMessageBus bus,
        TimeProvider time,
        CancellationToken cancellationToken)
    {
        if (!Application.Tenants.TenantCreation.CreateTenant.IsIdempotencyKey(idempotencyKey))
        {
            return IdempotencyKeyInvalid;
        }

        var created = await bus.InvokeForTenantAsync<Result<TenantDetails>>(
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            new CreateTenant(user.Id(), request.Name, request.Slug, request.OwnerEmail, idempotencyKey!),
            cancellationToken);
        return created.Map(tenant => new TenantResponse(tenant.Id, tenant.Name, tenant.Slug, tenant.Status));
    }
}

/// <summary>A new tenant, and the email address of its first owner, who is invited once the tenant is ready.</summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail);

/// <summary>A tenant and where its creation stands: provisioning, active or cancelled.</summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, string Status);
