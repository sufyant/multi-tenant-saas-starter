namespace Audit.Application.Plans;

/// <summary>
/// One key's value as an entry keeps it: a feature's state in <c>enabled</c>, or a limit's number in <c>maximum</c>, or an unlimited
/// limit with <c>unlimited</c> true. Audit's own type, as the entry keeps it from that moment.
/// </summary>
public sealed record AuditEntitlementValue(bool? Enabled, int? Maximum, bool Unlimited)
{
    internal static AuditEntitlementValue Of(ControlPlane.Contracts.Plans.EntitlementValueRecord value) => new(value.Enabled, value.Maximum, value.Unlimited);

    internal static IReadOnlyDictionary<string, AuditEntitlementValue> Of(IReadOnlyDictionary<string, ControlPlane.Contracts.Plans.EntitlementValueRecord> values) =>
        values.OrderBy(value => value.Key, StringComparer.Ordinal).ToDictionary(value => value.Key, value => Of(value.Value), StringComparer.Ordinal);
}
