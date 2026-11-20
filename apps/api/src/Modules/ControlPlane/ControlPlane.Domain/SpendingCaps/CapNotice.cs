namespace ControlPlane.Domain.SpendingCaps;

/// <summary>What a use first did to the cap in the month: nothing, took the extra to 80% of it, or reached it (E10).</summary>
internal enum CapNotice
{
    None,
    NearlyReached,
    Reached,
}
