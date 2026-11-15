using SharedKernel;

namespace ControlPlane.Domain.Plans;

/// <summary>
/// A plan's setting of one meter of the code (<see cref="MeterCatalog"/>), a row of <c>plan_meters</c>: the quota of each period, what
/// happens at the quota, and the price of the extra, <see cref="UnitPrice"/> minor units for each <see cref="UnitSize"/> units. A meter
/// the plan has no row for is not on the plan.
/// </summary>
internal sealed class PlanMeter
{
    /// <summary>The most a quota or a unit size may be.</summary>
    public const long Maximum = 1_000_000_000_000;

    public PlanMeter(Guid planId, string key, MeterSetting setting)
    {
        PlanId = planId;
        Key = key;
        Set(setting);
    }

    // EF Core reads a row through this constructor.
    private PlanMeter(Guid planId, string key)
    {
        PlanId = planId;
        Key = key;
    }

    public Guid PlanId { get; private init; }

    public string Key { get; private init; }

    public long Quota { get; private set; }

    public MeterOverage Overage { get; private set; }

    public long UnitPrice { get; private set; }

    public long UnitSize { get; private set; }

    public MeterSetting Setting => new(Quota, Overage, UnitPrice, UnitSize);

    /// <summary>
    /// Whether the plan may set the meter so: the code defines the key, the quota is 0 to <see cref="Maximum"/>, the unit price 0 to
    /// <see cref="PlanPrices.Maximum"/>, and the unit size 1 to <see cref="Maximum"/>; else 400 <c>plan.meter_invalid</c>, with the key.
    /// </summary>
    public static Result Check(string key, MeterSetting setting, MeterCatalog catalog) =>
        catalog.Defines(key)
        && setting.Quota is >= 0 and <= Maximum
        && setting.UnitPrice is >= 0 and <= PlanPrices.Maximum
        && setting.UnitSize is >= 1 and <= Maximum
        && Enum.IsDefined(setting.Overage)
            ? Result.Success()
            : Error.Validation(ControlPlaneErrorCodes.PlanMeterInvalid, $"The meter {key} is not one the code defines, or its setting is out of range.");

    public void Set(MeterSetting setting) => (Quota, Overage, UnitPrice, UnitSize) = (setting.Quota, setting.Overage, setting.UnitPrice, setting.UnitSize);
}
