namespace ControlPlane.Api.Platform.Plans;

/// <summary>
/// A new plan: its name, whether it takes the default mark, and its value of some keys of the list; a key left out has no value, and is
/// closed. The API trims the name before it counts its length.
/// </summary>
public sealed record CreatePlanRequest(string Name, bool IsDefault, IReadOnlyDictionary<string, EntitlementValueRequest> Entitlements);
