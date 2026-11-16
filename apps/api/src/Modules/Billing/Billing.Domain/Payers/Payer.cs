namespace Billing.Domain.Payers;

/// <summary>
/// What a tenant pays by card, as billing last read it from the provider (B13): the plan, the interval, and the amounts of its base fee and
/// its fee per person. One row for each tenant whose subscription has not ended. The platform counts a plan's payers across tenants (6.14,
/// 6.15) and a price change finds the tenants to tell and to move, so the row belongs to no tenant's row level security (R6), as the
/// tenants' summaries are (R12); the tenant's own transaction writes it, with its subscription. The subscription's row stays under row
/// level security and keeps everything else.
/// </summary>
internal sealed class Payer(Guid tenantId, Guid planId, bool yearly, long baseAmount, long perPersonAmount, bool ends)
{
    public Guid TenantId { get; private init; } = tenantId;

    public Guid PlanId { get; private set; } = planId;

    public bool Yearly { get; private set; } = yearly;

    public long BaseAmount { get; private set; } = baseAmount;

    public long PerPersonAmount { get; private set; } = perPersonAmount;

    /// <summary>Whether the subscription ends at its period's end: it pays no next period, so no price change moves it or tells it.</summary>
    public bool Ends { get; private set; } = ends;

    public void Take(Guid planId, bool yearly, long baseAmount, long perPersonAmount, bool ends) =>
        (PlanId, Yearly, BaseAmount, PerPersonAmount, Ends) = (planId, yearly, baseAmount, perPersonAmount, ends);
}
