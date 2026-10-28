using ControlPlane.Contracts.Plans;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Plans;

/// <summary>
/// One key's value as the platform reads and writes it: a feature's state in <see cref="Enabled"/>, a limit's number in
/// <see cref="Maximum"/>, or an unlimited limit with <see cref="Unlimited"/> true. Unlimited is a value of its own, never a number.
/// </summary>
public sealed record EntitlementValueView(bool? Enabled, int? Maximum, bool Unlimited)
{
    internal static EntitlementValueView Of(EntitlementValue value) => new(value.Enabled, value.Maximum, value.IsUnlimited);

    internal static EntitlementValueRecord RecordOf(EntitlementValue value) => new(value.Enabled, value.Maximum, value.IsUnlimited);

    internal static IReadOnlyDictionary<string, EntitlementValueRecord> RecordsOf(IReadOnlyDictionary<string, EntitlementValue> values) =>
        values.ToDictionary(value => value.Key, value => RecordOf(value.Value), StringComparer.Ordinal);

    /// <summary>
    /// The values of a request, each one value of the key's kind: one field, a feature's state, a limit's number of 0 or more, or unlimited.
    /// Else 400 <c>plan.entitlement_invalid</c> with the key; the plan or the tenant then checks the key and its kind.
    /// </summary>
    internal static Result<IReadOnlyDictionary<string, EntitlementValue>> Read(IReadOnlyDictionary<string, EntitlementValueView> values)
    {
        var read = new Dictionary<string, EntitlementValue>(StringComparer.Ordinal);
        foreach (var (key, view) in values)
        {
            var value = view.ToValue(key);
            if (!value.IsSuccess)
            {
                return value.Error;
            }

            read[key] = value.Value;
        }

        return read;
    }

    internal Result<EntitlementValue> ToValue(string key) => (Enabled, Maximum, Unlimited) switch
    {
        ({ } on, null, false) => EntitlementValue.Feature(on),
        (null, >= 0 and var maximum, false) => EntitlementValue.Limit(maximum),
        (null, null, true) => EntitlementValue.Unlimited,
        _ => Error.Validation(ControlPlaneErrorCodes.PlanEntitlementInvalid, "A value is a feature's state, a limit's number of 0 or more, or unlimited.") with
        {
            Entitlement = key,
        },
    };
}
