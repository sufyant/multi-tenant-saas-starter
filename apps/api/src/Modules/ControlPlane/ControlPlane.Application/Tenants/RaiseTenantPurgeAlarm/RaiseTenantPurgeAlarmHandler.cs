namespace ControlPlane.Application.Tenants.RaiseTenantPurgeAlarm;

// The saga decides and calls nothing (S12), so the alarm is a step of its own. It takes no DbContext: it writes a log entry and a metric.
public static class RaiseTenantPurgeAlarmHandler
{
    public static void Handle(RaiseTenantPurgeAlarmCommand raise, TenantPurgeAlarm alarm) => alarm.Raise(raise.TenantId, raise.Waiting);
}
