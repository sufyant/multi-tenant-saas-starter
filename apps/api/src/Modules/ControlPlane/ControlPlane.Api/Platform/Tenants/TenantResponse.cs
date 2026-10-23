namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A tenant and its status, with the reason of its suspension while it is suspended, or closed after a suspension, the moment of its
/// closure while it is closed or purging, and the moment of its purge once it is purged. Only a system admin sees this shape.
/// </summary>
public sealed record TenantResponse(
    Guid Id, string Name, string Slug, TenantResponseStatus Status, string? SuspensionReason, DateTimeOffset? ClosedAt, DateTimeOffset? PurgedAt);
