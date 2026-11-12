using SharedKernel;

namespace Audit.Domain.Retention;

/// <summary>
/// A tenant's state of the retention of its Activity log (K14): the period it keeps its entries for, in days (none for no limit), and the
/// hold that a shorter period starts. While a hold runs, the entries that the period before kept stay: those from <see cref="HeldFrom"/>
/// on (none: every entry), until <see cref="HeldUntil"/>. A tenant's first row has no limit, the period it had before step 37c, so its
/// first shortening holds every entry.
/// </summary>
internal sealed class TenantRetention : ITenantEntity
{
    private TenantRetention()
    {
    }

    public int? Days { get; private set; }

    public DateTimeOffset? HeldFrom { get; private set; }

    public DateTimeOffset? HeldUntil { get; private set; }

    /// <summary>
    /// The period got shorter at this moment, to these days: every entry that the period before kept at the moment stays until the moment
    /// plus the grace. During a running hold, the hold keeps its older start and takes the later end, so an entry stays longer, never
    /// shorter. A period that is as long or longer changes nothing here.
    /// </summary>
    public void Shorten(int days, DateTimeOffset at, TimeSpan grace)
    {
        if (Days <= days)
        {
            return;
        }

        var keptFrom = Days is { } before ? at.AddDays(-before) : (DateTimeOffset?)null;
        var end = at + grace;
        if (HeldUntil is { } running && at < running)
        {
            keptFrom = keptFrom is { } from && HeldFrom is { } held ? Earlier(from, held) : null;
            end = Later(end, running);
        }

        (Days, HeldFrom, HeldUntil) = (days, keptFrom, end);
    }

    /// <summary>The tenant's effective period at this moment: a shorter one starts the hold, a longer one applies at once.</summary>
    public void Follow(int? days, DateTimeOffset at, TimeSpan grace)
    {
        if (days is { } shorter && !(Days <= shorter))
        {
            Shorten(shorter, at, grace);
            return;
        }

        Days = days;
    }

    /// <summary>
    /// The moment before which the entries go at this moment, none when none goes. A hold that has ended is cleared first. While a hold
    /// runs, only the entries older than both its start and the period go.
    /// </summary>
    public DateTimeOffset? ExpiresBefore(DateTimeOffset now)
    {
        if (HeldUntil <= now)
        {
            (HeldFrom, HeldUntil) = (null, null);
        }

        if (Days is not { } days)
        {
            return null;
        }

        var outside = now.AddDays(-days);
        return HeldUntil is null ? outside : HeldFrom is { } from ? Earlier(from, outside) : null;
    }

    private static DateTimeOffset Earlier(DateTimeOffset first, DateTimeOffset second) => first < second ? first : second;

    private static DateTimeOffset Later(DateTimeOffset first, DateTimeOffset second) => first > second ? first : second;
}
