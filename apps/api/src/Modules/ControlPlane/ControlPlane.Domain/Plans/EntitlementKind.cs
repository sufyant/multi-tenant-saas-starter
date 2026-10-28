namespace ControlPlane.Domain.Plans;

/// <summary>What a key's value is: a feature is on or off, a limit is a number or unlimited.</summary>
internal enum EntitlementKind
{
    Feature,
    Limit,
}
