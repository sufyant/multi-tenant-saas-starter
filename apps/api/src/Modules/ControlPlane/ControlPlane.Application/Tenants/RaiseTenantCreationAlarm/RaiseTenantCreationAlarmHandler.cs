using ControlPlane.Application.Tenants.TenantCreation;

namespace ControlPlane.Application.Tenants.RaiseTenantCreationAlarm;

public static class RaiseTenantCreationAlarmHandler
{
    public static void Handle(RaiseTenantCreationAlarmCommand alarm, TenantCreationAlarm tenantCreationAlarm) => tenantCreationAlarm.Raise(alarm.TenantId);
}
