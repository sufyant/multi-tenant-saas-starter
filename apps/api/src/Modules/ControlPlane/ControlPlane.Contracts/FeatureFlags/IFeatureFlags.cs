namespace ControlPlane.Contracts.FeatureFlags;

/// <summary>
/// The one evaluation of a feature flag for an organisation, which any module asks (docs/flows/feature-flags.md). It reads the flag's rules
/// through the shared cache, which every change of a rule refreshes (C6). When the rules cannot be read, it answers the flag's default value:
/// off for a release flag, on for a kill switch (12.05).
/// </summary>
public interface IFeatureFlags
{
    /// <summary>Whether the flag that the code defines under this key is on for the organisation.</summary>
    Task<bool> IsOnAsync(string flag, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether the flag is on for work that belongs to no organisation, such as an email to a person: rule 3, everyone else, decides, as
    /// no organisation is listed or in the percentage.
    /// </summary>
    Task<bool> IsOnForEveryoneElseAsync(string flag, CancellationToken cancellationToken);

    /// <summary>Every flag that the code defines, with whether it is on for the organisation.</summary>
    Task<IReadOnlyDictionary<string, bool>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
}
