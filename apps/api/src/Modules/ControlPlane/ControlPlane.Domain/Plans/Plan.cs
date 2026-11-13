using System.Collections.Frozen;
using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// A plan: a name, and a value for some keys of the code's list (<see cref="EntitlementKeys"/>). A key without a value is closed. Every
/// tenant has one plan; a system admin creates and edits plans from the platform, with no deploy (Stripe, "Entitlements"; Hodgson,
/// "Feature Toggles", permissioning toggles). An edit applies at once to every tenant on the plan: there are no plan versions.
/// </summary>
internal sealed class Plan
{
    public const int NameMaxLength = 100;

    public static readonly Error NotFound = Error.NotFound(ControlPlaneErrorCodes.PlanNotFound, "The plan was not found.");

    private readonly List<PlanEntitlement> _values = [];

    private Plan(Guid id, string name, DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private init; }

    /// <summary>The plan's name, without the spaces around it, unique without regard to case.</summary>
    public string Name { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>The plan's rows of <c>plan_entitlements</c>, one for each key it has a value for.</summary>
    public IReadOnlyCollection<PlanEntitlement> Values => _values;

    /// <summary>The plan's value of each key it has one for.</summary>
    public IReadOnlyDictionary<string, EntitlementValue> ValueMap =>
        _values.ToFrozenDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal);

    /// <summary>
    /// A new plan with the name, kept without the spaces around it, and the values. The name is 1 to <see cref="NameMaxLength"/>
    /// characters and no other plan has it; each key is on the code's list, with a value of its kind. Every rejection comes first.
    /// </summary>
    public static Result<Plan> Create(Guid id, string name, IReadOnlyDictionary<string, EntitlementValue> values, bool nameTaken, DateTimeOffset createdAt)
    {
        var trimmed = name.Trim();
        var allowed = Check(trimmed, new Dictionary<string, EntitlementValue>(), values, nameTaken);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var plan = new Plan(id, trimmed, createdAt);
        plan._values.AddRange(values.Select(value => new PlanEntitlement(id, value.Key, value.Value)));
        return plan;
    }

    /// <summary>
    /// Gives the plan the name and exactly these values, under the rules of <see cref="Create"/>: a key left out loses its value, and is
    /// then closed. Returns what the plan had before; the name and the values it has change nothing (I1). Every rejection comes first.
    /// </summary>
    public Result<PlanEdit> Edit(string name, IReadOnlyDictionary<string, EntitlementValue> values, bool nameTaken)
    {
        var trimmed = name.Trim();
        var allowed = Check(trimmed, ValueMap, values, nameTaken);
        if (!allowed.IsSuccess)
        {
            return allowed.Error;
        }

        var before = new PlanEdit(Name, ValueMap);
        Name = trimmed;
        _values.RemoveAll(value => !values.ContainsKey(value.Key));
        foreach (var (key, value) in values)
        {
            if (_values.Find(existing => existing.Key == key) is { } existing)
            {
                existing.Set(value);
            }
            else
            {
                _values.Add(new PlanEntitlement(Id, key, value));
            }
        }

        return before;
    }

    private static Result Check(
        string trimmedName,
        IReadOnlyDictionary<string, EntitlementValue> before,
        IReadOnlyDictionary<string, EntitlementValue> values,
        bool nameTaken)
    {
        if (trimmedName.Length is < 1 or > NameMaxLength)
        {
            return Error.Validation(ControlPlaneErrorCodes.PlanNameInvalid, $"A plan's name is 1 to {NameMaxLength} characters, not counting the spaces around it.");
        }

        if (nameTaken)
        {
            return Error.Conflict(ControlPlaneErrorCodes.PlanNameTaken, "Another plan has this name.");
        }

        foreach (var (key, value) in values)
        {
            var valid = EntitlementValue.For(key, value);
            if (!valid.IsSuccess)
            {
                return valid.Error;
            }
        }

        return CheckDependentLimits(before, values);
    }

    // E1: a dependent limit that gets another value, or a first one, needs the plan's feature on after the edit. A limit left out loses its
    // value, which only closes it, so the deletion stays allowed; a feature turned off keeps its limit's value.
    private static Result CheckDependentLimits(IReadOnlyDictionary<string, EntitlementValue> before, IReadOnlyDictionary<string, EntitlementValue> values)
    {
        foreach (var (limit, feature) in EntitlementKeys.Dependencies)
        {
            if (values.GetValueOrDefault(limit) is { } value && value != before.GetValueOrDefault(limit) && values.GetValueOrDefault(feature)?.IsOn != true)
            {
                return EntitlementValue.FeatureRequired(limit);
            }
        }

        return Result.Success();
    }
}
