namespace SharedKernel;

/// <summary>
/// What a change to a state did: a command that sets a state the entity already has changes nothing (I1), and its caller then writes
/// and announces nothing.
/// </summary>
public enum StateChange
{
    Unchanged,
    Changed,
}
