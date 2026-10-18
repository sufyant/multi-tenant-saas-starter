namespace ControlPlane.Api.Platform.Tenants;

/// <summary>
/// A tenant and its status, with the reason of its suspension while it is suspended, or closed after a suspension, and the moment of its
/// closure while it is closed. Only a system admin sees this shape.
/// </summary>
public sealed record TenantResponse(Guid Id, string Name, string Slug, TenantResponseStatus Status, string? SuspensionReason, DateTimeOffset? ClosedAt);
