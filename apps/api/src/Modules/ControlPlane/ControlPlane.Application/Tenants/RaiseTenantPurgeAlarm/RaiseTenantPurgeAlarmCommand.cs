using SharedKernel;

namespace ControlPlane.Application.Tenants.RaiseTenantPurgeAlarm;

/// <summary>The tenant purge waited too long (S10): a person looks at the steps it still waits for, which the alarm names.</summary>
[MessageName("raise-tenant-purge-alarm")]
public sealed record RaiseTenantPurgeAlarmCommand(Guid TenantId, IReadOnlyList<string> Waiting);
