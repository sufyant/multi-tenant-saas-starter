using ControlPlane.Domain.Plans;

namespace ControlPlane.Domain.Tenants;

/// <summary>
/// A system admin's value of one key for one tenant, over the plan's value, a row of <c>entitlement_overrides</c>. It may expire: it then
/// stops applying at once, and stays stored, shown as expired, until a system admin removes it. It keeps why and by whom it was set. The
/// tenant's commands change it under the tenant's lock (M1, K6a).
/// </summary>
internal sealed class EntitlementOverride
{
    public const int ReasonMaxLength = 500;

    public EntitlementOverride(Guid tenantId, string key, EntitlementValue value, DateTimeOffset? expiresAt, string reason, Guid setBy, DateTimeOffset setAt)
    {
        TenantId = tenantId;
        Key = key;
        Reason = string.Empty;
        Set(value, expiresAt, reason, setBy, setAt);
    }

    // EF Core reads a row through this constructor.
    private EntitlementOverride(Guid tenantId, string key)
    {
        TenantId = tenantId;
        Key = key;
        Reason = string.Empty;
    }

    public Guid TenantId { get; private init; }

    public string Key { get; private init; }

    public bool? Enabled { get; private set; }

    public int? Maximum { get; private set; }

    public bool Unlimited { get; private set; }

    /// <summary>When the override stops applying; none for one that never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Why the system admin set it, in their words, without the spaces around it. Only the platform reads it.</summary>
    public string Reason { get; private set; }

    /// <summary>The user id of the system admin who set it last.</summary>
    public Guid SetBy { get; private set; }

    public DateTimeOffset SetAt { get; private set; }

    public EntitlementValue Value => EntitlementValue.Read(Enabled, Maximum, Unlimited);

    public OverrideValue ToValue() => new(Key, Value, ExpiresAt);

    public void Set(EntitlementValue value, DateTimeOffset? expiresAt, string reason, Guid setBy, DateTimeOffset setAt) =>
        (Enabled, Maximum, Unlimited, ExpiresAt, Reason, SetBy, SetAt) = (value.Enabled, value.Maximum, value.IsUnlimited, expiresAt, reason.Trim(), setBy, setAt);
}
