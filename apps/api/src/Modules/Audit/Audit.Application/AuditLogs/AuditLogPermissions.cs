using ControlPlane.Contracts;

namespace Audit.Application.AuditLogs;

/// <summary>
/// The permissions of the two audit logs, from the permission list. Audit's Api project does not reference ControlPlane's
/// Contracts (section 2), so it reads the names here.
/// </summary>
public static class AuditLogPermissions
{
    public const string TenantAuditRead = Permissions.TenantAuditRead;

    public const string PlatformAuditRead = Permissions.PlatformAuditRead;
}
