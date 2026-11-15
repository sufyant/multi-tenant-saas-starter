namespace ControlPlane.Domain.Plans;

/// <summary>What happens when a tenant's use of a meter reaches the plan's quota in a period.</summary>
internal enum MeterOverage
{
    /// <summary>The use stops until the next period.</summary>
    Stop,

    /// <summary>The use goes on, and the extra is charged at the meter's unit price.</summary>
    Charge,
}
