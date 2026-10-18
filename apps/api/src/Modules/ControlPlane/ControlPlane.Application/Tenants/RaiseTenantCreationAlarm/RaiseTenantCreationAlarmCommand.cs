namespace ControlPlane.Application.Tenants.RaiseTenantCreationAlarm;

/// <summary>Tells a person that a tenant creation needs them (S9).</summary>
public sealed record RaiseTenantCreationAlarmCommand(Guid TenantId);
