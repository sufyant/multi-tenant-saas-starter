namespace ControlPlane.Domain.Plans;

/// <summary>A plan's value for one key, a row of <c>plan_entitlements</c>. A key the plan has no row for is closed.</summary>
internal sealed class PlanEntitlement
{
    public PlanEntitlement(Guid planId, string key, EntitlementValue value)
    {
        PlanId = planId;
        Key = key;
        Set(value);
    }

    // EF Core reads a row through this constructor.
    private PlanEntitlement(Guid planId, string key)
    {
        PlanId = planId;
        Key = key;
    }

    public Guid PlanId { get; private init; }

    public string Key { get; private init; }

    public bool? Enabled { get; private set; }

    public int? Maximum { get; private set; }

    public bool Unlimited { get; private set; }

    public EntitlementValue Value => EntitlementValue.Read(Enabled, Maximum, Unlimited);

    public void Set(EntitlementValue value) => (Enabled, Maximum, Unlimited) = (value.Enabled, value.Maximum, value.IsUnlimited);
}
