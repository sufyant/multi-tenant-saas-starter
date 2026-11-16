using System.Text.Json.Serialization;

namespace ControlPlane.Api.Platform.Plans;

/// <summary>Who pays a plan's new price, and when (6.14, B13).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanPriceChangeRequestWho>))]
public enum PlanPriceChangeRequestWho
{
    [JsonStringEnumMemberName("everyone_after_notice")]
    EveryoneAfterNotice,

    [JsonStringEnumMemberName("everyone_from")]
    EveryoneFrom,

    [JsonStringEnumMemberName("new_customers_only")]
    NewCustomersOnly,
}
