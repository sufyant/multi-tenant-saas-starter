namespace ControlPlane.Domain.Plans;

/// <summary>
/// The numbers of the tenant's Activity log period, the limit <see cref="LimitKeys.AuditRetentionDays"/>, in days (K14, R19), declared once,
/// here (D1). <c>apps/api/scripts/generate-entitlements.mjs</c> writes <c>ControlPlane.Contracts/Plans/AuditRetention.g.cs</c> and the
/// TypeScript of <c>packages/api-client</c> from its constants.
/// </summary>
internal static class AuditRetention
{
    /// <summary>The shortest period: a value under it is refused, and the deletion refuses a moment of the last 30 days.</summary>
    public const int Floor = 30;

    /// <summary>The hold: when a period gets shorter, the entries that the period before kept stay this long, then go.</summary>
    public const int Grace = 30;

    /// <summary>
    /// How the period changes from one effective value to the next: the old period (none for no limit) and the new one, when the new one
    /// is shorter, so it deletes an entry that the old one kept; none when it is as long or longer. Unlimited keeps every entry.
    /// </summary>
    public static (int? FromDays, int ToDays)? ShorteningOf(EntitlementValue before, EntitlementValue after) =>
        after.Maximum is { } to && (before.IsUnlimited || to < before.Maximum) ? (before.Maximum, to) : null;
}
