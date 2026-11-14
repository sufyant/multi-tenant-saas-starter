namespace ControlPlane.Domain.FeatureFlags;

/// <summary>What one line of a flag's history tells: a deploy added or removed it, or a system admin changed one of its rules.</summary>
internal enum FeatureFlagChangeKind
{
    /// <summary>The first deploy whose code defines the flag stored it.</summary>
    AddedToCode,

    /// <summary>The first deploy whose code retires the flag archived it.</summary>
    RemovedFromCode,

    /// <summary>Rule 1: a system admin listed an organisation.</summary>
    Listed,

    /// <summary>Rule 1: a system admin removed a listed organisation.</summary>
    Unlisted,

    /// <summary>Rule 2: a system admin changed the percentage.</summary>
    PercentageChanged,

    /// <summary>Rule 3: a system admin turned the flag on or off for everyone else.</summary>
    EveryoneElseChanged,
}
