namespace ControlPlane.Domain.Plans;

/// <summary>What a plan had before an edit (<see cref="Plan.Edit"/>): its name and its values, for the audit entry and the cache.</summary>
internal sealed record PlanEdit(string OldName, IReadOnlyDictionary<string, EntitlementValue> OldValues);
