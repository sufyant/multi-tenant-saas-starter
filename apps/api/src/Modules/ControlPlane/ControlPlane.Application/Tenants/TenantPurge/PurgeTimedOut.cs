using SharedKernel;
using Wolverine;

namespace ControlPlane.Application.Tenants.TenantPurge;

/// <summary>The purge waited too long for a step (S10): the alarm names what it still waits for, and it keeps waiting.</summary>
[MessageName("purge-timed-out")]
public sealed record PurgeTimedOut(Guid TenantId, TimeSpan DelayTime) : TimeoutMessage(DelayTime);
