namespace ControlPlane.Domain.FeatureFlags;

/// <summary>Why a flag is on for an organisation: the first of its three rules that turns it on, in their order.</summary>
internal enum FeatureFlagReason
{
    /// <summary>Rule 1: the organisation is listed.</summary>
    Listed,

    /// <summary>Rule 2: the organisation's bucket is under the percentage.</summary>
    Percentage,

    /// <summary>Rule 3: the flag is on for everyone else.</summary>
    Everyone,
}
