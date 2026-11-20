using System.Security.Claims;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Statuses;
using ControlPlane.Api.Tenant.Members;
using ControlPlane.Application.Tenants;
using ControlPlane.Application.Tenants.CreateMyTenant;
using ControlPlane.Application.Tenants.ListMyTenants;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using SharedKernel;
using Wolverine;
using ControlPlane.Api.Tenant.Roles;

namespace ControlPlane.Api.Me.Tenants;

/// <summary>The tenants the signed-in user belongs to.</summary>
public static class MyTenantEndpoints
{
    internal const string NoLocation =
        "Answers 201 with the tenant and without Location: the API reads the user's tenants as a list, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder me)
    {
        me.MapGet("/tenants", ListMyTenants);
        me.MapPost("/tenants", CreateMyTenant)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .Produces<MyTenantResponse>(StatusCodes.Status201Created);
    }

    // Accepting an invitation answers the tenant it joined in the same shape (N7g: a response that two audiences answer stays in the
    // first one's file).
    internal static MyTenantResponse ToResponse(MyTenantView tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfMyTenant(tenant.Status), RoleEndpoints.ToResponse(tenant.Role));

    // Step 40d: tenant creation runs inside the tenant it creates, whose id the server chooses, as the platform's CreateTenant does. The
    // client gives each new tenant an idempotency key (I1); the endpoint declares 201, so the host answers a new tenant with 201 (I5).
    private static async Task<Result<MyTenantResponse>> CreateMyTenant(
        CreateMyTenantRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        TimeProvider time,
        CancellationToken cancellationToken) =>
        (await idempotency.ExecuteAsync<MyTenantView>(
            new CreateMyTenantCommand(user.Id(), request.Name, request.Slug),
            Guid.CreateVersion7(time.GetUtcNow()).ToString(),
            cancellationToken)).Map(ToResponse);

    // T4: the user's own active and suspended tenants, read without a tenant.
    private static async Task<Result<ListPage<MyTenantResponse>>> ListMyTenants(
        int? page,
        int? pageSize,
        ListSort<ListMyTenantsSort>? sort,
        string? q,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var tenants = await bus.InvokeAsync<Result<ListPage<MyTenantView>>>(new ListMyTenantsQuery(user.Id(), list.Value), cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }
}
