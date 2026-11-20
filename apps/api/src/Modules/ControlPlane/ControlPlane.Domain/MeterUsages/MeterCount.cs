namespace ControlPlane.Domain.MeterUsages;

/// <summary>What a use did to the month's count: stopped, counted, or counted and first took the count to 80% or to all of the quota.</summary>
internal enum MeterCount
{
    Stopped,
    Counted,
    NearlyUsed,
    UsedUp,
}
