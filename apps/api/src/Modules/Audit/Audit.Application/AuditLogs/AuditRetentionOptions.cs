using ControlPlane.Contracts.Plans;

namespace Audit.Application.AuditLogs;

// How long the platform's log keeps an entry (section Audit:Retention, K15), validated at startup by the module's infrastructure. The
// floor is the tenant's (R19): the deletion refuses a moment of the last 30 days.
public sealed class AuditRetentionOptions
{
    public const string Section = "Audit:Retention";

    public int Days { get; set; } = 365;

    public bool IsValid => Days >= AuditRetention.Floor;
}
