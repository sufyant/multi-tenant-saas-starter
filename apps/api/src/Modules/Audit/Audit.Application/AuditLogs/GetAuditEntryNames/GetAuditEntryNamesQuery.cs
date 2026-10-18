using SharedKernel;

namespace Audit.Application.AuditLogs.GetAuditEntryNames;

/// <summary>A page of an audit log with each user, actor or target, named now by ControlPlane, in one call for the page (K3).</summary>
[MessageName("get-audit-entry-names")]
public sealed record GetAuditEntryNamesQuery(CursorPage<AuditEntryView> Page);
