namespace Audit.Api.Tenant.AuditLogs;

/// <summary>
/// A role in the details of an audit entry, as the entry keeps it from that moment: its id, its name and whether it is built in. The
/// role may be gone or renamed since, so the entry is its own record of it (K3), and Audit answers it in a shape of its own (the
/// maintainer's decision in step r4b). An entry written before step r4b keeps the name only: its id is null, and it is built in, because
/// every role was then.
/// </summary>
public sealed record AuditRoleResponse(Guid? Id, string Name, bool BuiltIn);
