using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A system admin edited a plan: its name, its default mark, its values, its prices or its meters. Published only when something changed, without a tenant. The
/// edit applies at once to every tenant on the plan.
/// </summary>
/// <param name="UpdatedBy">The user id of the system admin who edited the plan.</param>
/// <param name="PreviousDefaultPlanId">The plan that had the default mark, when the edit moved the mark to this plan; else none.</param>
/// <param name="PriceChange">Who pays the new prices, and when, for an edit that changed them (B13); none for another edit, and in a
/// message of a version before the price changes.</param>
/// <param name="Currency">The deployment's currency of the prices; none in a message of a version before the price changes.</param>
/// <param name="OldPrices">The plan's prices before the edit; none in a message of a version before step 40, and so the four below.</param>
[MessageName("plan-updated")]
public sealed record PlanUpdatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PlanId,
    Guid UpdatedBy,
    string OldName,
    string NewName,
    bool WasDefault,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueRecord> OldEntitlements,
    IReadOnlyDictionary<string, EntitlementValueRecord> NewEntitlements,
    Guid? PreviousDefaultPlanId = null,
    string? PreviousDefaultPlanName = null,
    PlanPricesRecord? OldPrices = null,
    PlanPricesRecord? NewPrices = null,
    IReadOnlyList<MeterSettingRecord>? OldMeters = null,
    IReadOnlyList<MeterSettingRecord>? NewMeters = null,
    PriceChangeRecord? PriceChange = null,
    string? Currency = null);
