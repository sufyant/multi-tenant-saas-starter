namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// A plan in the details of an audit entry, as the entry keeps it from that moment: its id and its name. The plan may be renamed since,
/// so the entry is its own record of it, and Audit answers it in a shape of its own, as <see cref="AuditRoleResponse"/>.
/// </summary>
public sealed record AuditPlanResponse(Guid Id, string Name);
