namespace Audit.Application.AuditLogs;

/// <summary>
/// The details an audit entry keeps: one type for each operation that has some (K3). A handler writes one as JSON, and the read side
/// reads it back by the entry's operation (<see cref="AuditDetailsTypes"/>). A stored entry never changes, so the JSON of each type stays
/// the one its handler writes.
/// </summary>
public abstract record AuditDetails;
