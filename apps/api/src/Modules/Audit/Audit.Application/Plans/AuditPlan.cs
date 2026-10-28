namespace Audit.Application.Plans;

/// <summary>
/// A plan in the details of an audit entry, as the entry keeps it from that moment: its id and its name. The plan may be renamed since,
/// so the entry is its own record of it, as for a role (<c>AuditRoleResponse</c>; K3).
/// </summary>
public sealed record AuditPlan(Guid Id, string Name);
