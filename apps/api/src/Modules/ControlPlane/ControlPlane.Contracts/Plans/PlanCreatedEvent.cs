using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>A system admin created a plan. Published through the outbox, without a tenant: a plan belongs to none.</summary>
/// <param name="CreatedBy">The user id of the system admin who created the plan.</param>
/// <param name="Entitlements">The plan's value of each key it has one for.</param>
/// <param name="Prices">The plan's prices; none in a message of a version before step 40.</param>
/// <param name="Meters">The plan's setting of each meter it sets, by key; none in a message of a version before step 40.</param>
[MessageName("plan-created")]
public sealed record PlanCreatedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid PlanId,
    Guid CreatedBy,
    string Name,
    bool IsDefault,
    IReadOnlyDictionary<string, EntitlementValueRecord> Entitlements,
    PlanPricesRecord? Prices = null,
    IReadOnlyList<MeterSettingRecord>? Meters = null);
