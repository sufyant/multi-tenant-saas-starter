namespace ControlPlane.Contracts.Usage;

/// <summary>The answer to <see cref="UseMeterCommand"/>.</summary>
public enum MeterUse
{
    /// <summary>The tenant may use the meter, and the use is counted.</summary>
    Used,

    /// <summary>
    /// The tenant may not: the meter is not on its plan, or it is a stop meter that this use would take over its quota. Nothing is counted.
    /// </summary>
    Stopped,
}
