using SharedKernel;

namespace ControlPlane.Application.Tenants.RaiseTenantCreationAlarm;

/// <summary>Tells a person that a tenant creation needs them (S9).</summary>
[MessageName("raise-tenant-creation-alarm")]
public sealed record RaiseTenantCreationAlarmCommand(Guid TenantId);
