using System.Security.Claims;
using System.Text.Json.Serialization;
using ControlPlane.Api.Authentication;
using ControlPlane.Api.Tenant;
using ControlPlane.Application.Invitations;
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
        platform.MapPost("/tenants/{tenantId:guid}/owner-invitation", InviteOwner)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(Invitations.NoLocation)
            .RequireAuthorization(Permissions.PlatformTenantsInviteOwner)
            .Produces<InvitationResponse>(StatusCodes.Status201Created);
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
        var created = await idempotency.ExecuteAsync<TenantDetails>(
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

        var tenants = await bus.InvokeAsync<Result<ListPage<TenantListItem>>>(
            new ListTenantsQuery(list.Value, filter.Value is { } chosen ? ResponseStatus.OfTenant(chosen) : null),
            cancellationToken);
        return tenants.Map(found => found.Map(ToResponse));
    }

    // One tenant is read inside the tenant the route names, as suspension does (W3); an id no tenant has is not found.
    private static async Task<Result<TenantDetailResponse>> GetTenant(Guid tenantId, IMessageBus bus, CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantWithOwners>>(tenantId.ToString(), new GetTenantQuery(), cancellationToken)).Map(ToResponse);

    // The platform invites an Owner into a tenant without one. It runs inside the tenant the route names, which the idempotency executor
    // declares (W3), under the request's idempotency key (I1). The server chooses the invitation's id. The endpoint declares 201, so the
    // host answers a new invitation with 201, and a replayed request maps its saved result the same way (I5).
    private static async Task<Result<InvitationResponse>> InviteOwner(
        Guid tenantId,
        InviteOwnerRequest request,
        ClaimsPrincipal user,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var invited = await idempotency.ExecuteAsync<InvitationSummary>(
            new InviteOwnerCommand(user.Id(), request.Email, request.Language), tenantId.ToString(), cancellationToken);
        return invited.Map(Invitations.ToResponse);
    }

    // A change of a tenant's name or status runs inside the tenant the route names (W3). Each sets a state, so it takes no idempotency
    // key (I1).
    private static async Task<Result<TenantResponse>> RenameTenant(
        Guid tenantId,
        RenameTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new RenameTenantCommand(user.Id(), request.Name), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> SuspendTenant(
        Guid tenantId,
        SuspendTenantRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new SuspendTenantCommand(user.Id(), request.Reason), cancellationToken))
            .Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReactivateTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new ReactivateTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> CloseTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new CloseTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static async Task<Result<TenantResponse>> ReopenTenant(
        Guid tenantId,
        ClaimsPrincipal user,
        IMessageBus bus,
        CancellationToken cancellationToken) =>
        (await bus.InvokeForTenantAsync<Result<TenantDetails>>(tenantId.ToString(), new ReopenTenantCommand(user.Id()), cancellationToken)).Map(ToResponse);

    private static TenantResponse ToResponse(TenantDetails tenant) =>
        new(tenant.Id, tenant.Name, tenant.Slug, ResponseStatus.OfTenant(tenant.Status), tenant.SuspensionReason, tenant.ClosedAt);

    private static TenantListItemResponse ToResponse(TenantListItem item) => new(
        item.Tenant.Id,
        item.Tenant.Name,
        item.Tenant.Slug,
        ResponseStatus.OfTenant(item.Tenant.Status),
        item.Tenant.SuspensionReason,
        item.Tenant.ClosedAt,
        item.ReopenableUntil,
        item.MemberCount,
        item.OwnerCount,
        item.FirstOwner is { } owner ? ToResponse(owner) : null,
        item.OwnerInvitation is { } invitation ? ToResponse(invitation) : null);

    private static TenantDetailResponse ToResponse(TenantWithOwners tenant) => new(
        tenant.Tenant.Id,
        tenant.Tenant.Name,
        tenant.Tenant.Slug,
        ResponseStatus.OfTenant(tenant.Tenant.Status),
        tenant.Tenant.SuspensionReason,
        tenant.Tenant.ClosedAt,
        tenant.ReopenableUntil,
        tenant.MemberCount,
        tenant.OwnerCount,
        [.. tenant.Owners.Select(ToResponse)],
        tenant.OwnerInvitation is { } invitation ? ToResponse(invitation) : null);

    private static TenantOwnerResponse ToResponse(OwnerDetails owner) => new(owner.UserId, owner.FirstName, owner.LastName, owner.Email);

    private static OwnerInvitationResponse ToResponse(OwnerInvitationDetails invitation) =>
        new(invitation.Email, invitation.ExpiresAt, ResponseStatus.OfInvitation(invitation.State));
}

/// <summary>
/// A new tenant, the email address of its first owner, who is invited once the tenant is ready, and the language of the owner's
/// invitation email: without one, the system admin's own. The API trims the name before it counts its length.
/// </summary>
public sealed record CreateTenantRequest(string Name, string Slug, string OwnerEmail, Language? Language = null);

/// <summary>
/// The address of the Owner the platform invites into a tenant without one, and the language of the invitation's email: without one, the
/// system admin's own.
/// </summary>
public sealed record InviteOwnerRequest(string Email, Language? Language = null);

/// <summary>A tenant's new name. The API trims it before it counts its length. The slug never changes.</summary>
public sealed record RenameTenantRequest(string Name);

/// <summary>
/// Why the system admin suspends the tenant, within the API's limit. The API trims the reason before it counts its length. A member
/// never sees it.
/// </summary>
public sealed record SuspendTenantRequest(string Reason);

/// <summary>
/// A tenant and its status, with the reason of its suspension while it is suspended, or closed after a suspension, and the moment of its
/// closure while it is closed. Only a system admin sees this shape.
/// </summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, TenantResponseStatus Status, string? SuspensionReason, DateTimeOffset? ClosedAt);

/// <summary>
/// A tenant in the platform's list: as <see cref="TenantResponse"/>, with until when a closed tenant can be reopened, its members and Owners
/// counted, the Owner who joined first, and, while it has no Owner, its latest Owner invitation. Each is null when it does not apply.
/// </summary>
public sealed record TenantListItemResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    TenantOwnerResponse? FirstOwner,
    OwnerInvitationResponse? OwnerInvitation);

/// <summary>One tenant for the platform's detail screen: as an item of the list, with every Owner, in the order they joined, in place of the first.</summary>
public sealed record TenantDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    TenantResponseStatus Status,
    string? SuspensionReason,
    DateTimeOffset? ClosedAt,
    DateTimeOffset? ReopenableUntil,
    int MemberCount,
    int OwnerCount,
    IReadOnlyList<TenantOwnerResponse> Owners,
    OwnerInvitationResponse? OwnerInvitation);

/// <summary>An Owner of a tenant: the catalog user's id, names and address.</summary>
public sealed record TenantOwnerResponse(Guid UserId, string? FirstName, string? LastName, string? Email);

/// <summary>
/// The latest Owner invitation of a tenant that has no Owner: its address, its expiry, and its state at the moment of the request:
/// <c>pending</c>, <c>expired</c> once its expiry has passed, or <c>cancelled</c>. It is never <c>accepted</c>, because it is shown only
/// while the tenant has no Owner.
/// </summary>
public sealed record OwnerInvitationResponse(string Email, DateTimeOffset ExpiresAt, InvitationResponseState State);

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
