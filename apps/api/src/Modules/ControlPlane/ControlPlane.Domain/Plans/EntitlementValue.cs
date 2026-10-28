using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// The value of one key: a feature is on or off; a limit is a number of 0 or more, or unlimited. Unlimited is a value of its own, never a
/// number such as -1. A plan's value and an override hold one; the effective value is one too (<see cref="EffectiveEntitlements"/>).
/// </summary>
internal sealed record EntitlementValue
{
    public static readonly EntitlementValue On = new(true, null, false);

    public static readonly EntitlementValue Off = new(false, null, false);

    public static readonly EntitlementValue Unlimited = new(null, null, true);

    private EntitlementValue(bool? enabled, int? maximum, bool isUnlimited)
    {
        Enabled = enabled;
        Maximum = maximum;
        IsUnlimited = isUnlimited;
    }

    /// <summary>A feature's state; none for a limit.</summary>
    public bool? Enabled { get; }

    /// <summary>A limit's number; none for a feature and for an unlimited limit.</summary>
    public int? Maximum { get; }

    /// <summary>Whether a limit has no number.</summary>
    public bool IsUnlimited { get; }

    public EntitlementKind Kind => Enabled is null ? EntitlementKind.Limit : EntitlementKind.Feature;

    /// <summary>Whether a feature is on. A limit is never on.</summary>
    public bool IsOn => Enabled == true;

    public static EntitlementValue Feature(bool on) => on ? On : Off;

    public static EntitlementValue Limit(int maximum) =>
        maximum >= 0 ? new(null, maximum, false) : throw new ArgumentOutOfRangeException(nameof(maximum), maximum, "A limit is 0 or more.");

    /// <summary>The value of a key that has none: closed. A feature is off and a limit is 0, so a new key opens nothing by itself.</summary>
    public static EntitlementValue ClosedOf(EntitlementKind kind) => kind == EntitlementKind.Feature ? Off : Limit(0);

    /// <summary>The value of the stored columns, which the database's CHECK keeps to one value of one kind (R15).</summary>
    public static EntitlementValue Read(bool? enabled, int? maximum, bool unlimited) =>
        (enabled, maximum, unlimited) switch
        {
            ({ } on, null, false) => Feature(on),
            (null, { } number, false) => Limit(number),
            (null, null, true) => Unlimited,
            _ => throw new InvalidOperationException("A stored value is a feature's state, a limit's number or unlimited."),
        };

    /// <summary>
    /// The value for the key, when the key is on the code's list and the value has the key's kind; else 400
    /// <c>plan.entitlement_invalid</c>, with the key.
    /// </summary>
    public static Result<EntitlementValue> For(string key, EntitlementValue value) =>
        EntitlementKeys.KindOf(key) == value.Kind
            ? value
            : Error.Validation(ControlPlaneErrorCodes.PlanEntitlementInvalid, "The key is not on the list, or the value is not of the key's kind.") with
            {
                Entitlement = key,
            };

    /// <summary>Whether a limit lets the tenant add one more thing it counts: an unlimited limit always does, a number while the count is under it.</summary>
    public bool Allows(int count) => IsUnlimited || count < Maximum;

    /// <summary>How far the count is over the limit; 0 when it is not over, and for an unlimited limit.</summary>
    public int ExcessOf(int count) => IsUnlimited || Maximum is not { } maximum ? 0 : Math.Max(0, count - maximum);
}
