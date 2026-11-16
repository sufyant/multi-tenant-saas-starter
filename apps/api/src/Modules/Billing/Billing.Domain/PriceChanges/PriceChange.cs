namespace Billing.Domain.PriceChanges;

/// <summary>
/// A change of a plan's prices (B13, 6.14 to 6.16), as billing took it from the plan's edit: the prices before and after, when, by whom, and
/// when the plan's payers move to the new Prices, none while only new customers pay them. A Price never changes, so a payer keeps the
/// Price it pays until its subscription moves (B2). Only the plan's latest change moves or tells anybody: a later edit replaces an earlier
/// one. It belongs to no tenant (R6): a plan is every tenant's.
/// </summary>
internal sealed class PriceChange
{
    /// <summary>How long before the move each payer is told: 30 days (6.14).</summary>
    public static readonly TimeSpan Notice = TimeSpan.FromDays(30);

    private PriceChange()
    {
        PlanName = string.Empty;
        Currency = string.Empty;
        OldPrices = new PlanPrices(0, 0, 0, 0);
        NewPrices = OldPrices;
    }

    /// <summary>The plan edit's event id, so a second copy of the event keeps one change (I1).</summary>
    public Guid Id { get; private set; }

    public Guid PlanId { get; private set; }

    /// <summary>The plan's name at the change, which the Activity log keeps (K3).</summary>
    public string PlanName { get; private set; }

    public string Currency { get; private set; }

    public PlanPrices OldPrices { get; private set; }

    public PlanPrices NewPrices { get; private set; }

    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>The user id of the system admin who changed the prices.</summary>
    public Guid ChangedBy { get; private set; }

    /// <summary>When the payers on another price move to the new one; none while only new customers pay it.</summary>
    public DateTimeOffset? MovesAt { get; private set; }

    /// <summary>When each payer is told: the notice before the move.</summary>
    public DateTimeOffset? AnnouncesAt => MovesAt - Notice;

    /// <summary>
    /// The change of a plan edit (6.14). Every payer after 30 days' notice moves 30 days after the change; from a set day moves on that
    /// day, which comes at least 30 days after the change, as the notice comes 30 days before it; new customers only moves nobody.
    /// </summary>
    public static PriceChange Of(
        Guid id, Guid planId, string planName, string currency, PlanPrices oldPrices, PlanPrices newPrices, Guid changedBy, DateTimeOffset at, PriceChangeWho who, DateTimeOffset? from) =>
        new()
        {
            Id = id,
            PlanId = planId,
            PlanName = planName,
            Currency = currency,
            OldPrices = oldPrices,
            NewPrices = newPrices,
            ChangedBy = changedBy,
            ChangedAt = at,
            MovesAt = who switch
            {
                PriceChangeWho.EveryoneAfterNotice => at + Notice,
                PriceChangeWho.EveryoneFrom => from ?? throw new ArgumentNullException(nameof(from), "A change from a set day names the day."),
                _ => null,
            },
        };

    /// <summary>
    /// "Move to the new price" (6.16): the payers on another price move after 30 days' notice. False when a move is scheduled already, which
    /// keeps its day.
    /// </summary>
    public bool MoveAfterNotice(DateTimeOffset now)
    {
        if (MovesAt > now)
        {
            return false;
        }

        MovesAt = now + Notice;
        return true;
    }

    /// <summary>Whether a payer of this plan and interval pays a price this change no longer has: it moves, and it is told.</summary>
    public bool Moves(bool yearly, long baseAmount, long perPersonAmount) => NewPrices.Of(yearly) != (baseAmount, perPersonAmount);
}
