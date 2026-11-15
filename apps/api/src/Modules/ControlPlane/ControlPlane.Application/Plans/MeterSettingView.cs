using ControlPlane.Contracts.Plans;
using ControlPlane.Domain;
using ControlPlane.Domain.Plans;
using SharedKernel;

namespace ControlPlane.Application.Plans;

/// <summary>
/// A plan's setting of one meter as the platform reads and writes it: the quota of each period, <see cref="Overage"/> <c>Stop</c> or
/// <c>Charge</c> at the quota, and the price of the extra, <see cref="UnitPrice"/> minor units for each <see cref="UnitSize"/> units.
/// </summary>
public sealed record MeterSettingView(string Key, long Quota, string Overage, long UnitPrice, long UnitSize)
{
    internal static IReadOnlyList<MeterSettingView> Of(IReadOnlyDictionary<string, MeterSetting> meters) =>
        [.. meters.OrderBy(meter => meter.Key, StringComparer.Ordinal)
            .Select(meter => new MeterSettingView(meter.Key, meter.Value.Quota, meter.Value.Overage.ToString(), meter.Value.UnitPrice, meter.Value.UnitSize))];

    internal static IReadOnlyList<MeterSettingRecord> RecordsOf(IReadOnlyDictionary<string, MeterSetting> meters) =>
        [.. Of(meters).Select(meter => new MeterSettingRecord(meter.Key, meter.Quota, meter.Overage, meter.UnitPrice, meter.UnitSize))];

    /// <summary>The meters of a request, each key once; a key sent twice answers 400 <c>plan.meter_invalid</c>.</summary>
    internal static Result<IReadOnlyDictionary<string, MeterSetting>> Read(IReadOnlyList<MeterSettingView> meters)
    {
        var read = new Dictionary<string, MeterSetting>(StringComparer.Ordinal);
        foreach (var meter in meters)
        {
            if (!Enum.TryParse<MeterOverage>(meter.Overage, out var overage) || !read.TryAdd(meter.Key, new MeterSetting(meter.Quota, overage, meter.UnitPrice, meter.UnitSize)))
            {
                return Error.Validation(ControlPlaneErrorCodes.PlanMeterInvalid, $"The meter {meter.Key} is sent twice, or its overage is neither stop nor charge.");
            }
        }

        return read;
    }
}
