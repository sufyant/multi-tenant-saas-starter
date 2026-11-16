using SharedKernel;

namespace ControlPlane.Contracts.Plans;

/// <summary>
/// A system admin put a tenant on another plan, or a member paid for one through billing (<paramref name="ByMember"/>, step 40). Published through the outbox; a subscriber handles it in the tenant's transaction, as the
/// tenant's id travels in the message's envelope. It names both plans as they were at that moment.
/// </summary>
/// <param name="ChangedBy">The user id of the system admin who changed the plan, or of the member who paid for it.</param>
/// <param name="RetentionShortened">The Activity log period that got shorter with the plan, none when it did not (K14). None in an event of the
/// version before step 37c.</param>
/// <param name="ByMember">Whether a member of the tenant changed the plan through billing, not a system admin. False in an event of the
/// version before step 40.</param>
/// <param name="Unpaid">Whether billing moved the tenant to the default plan because a payment failed and its grace ended (B11): nobody
/// acted. False in an event of the version before the dunning.</param>
/// <param name="UnpaidAmountDue">With <paramref name="Unpaid"/>, the open invoice without tax, in the currency's minor unit, which pays the
/// plan back (13.20); none when billing held no failed payment, or in an event of the version before part 10 of step 40.</param>
/// <param name="UnpaidCurrency">The open invoice's currency, with <paramref name="UnpaidAmountDue"/>.</param>
[MessageName("tenant-plan-changed")]
public sealed record TenantPlanChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ChangedBy,
    Guid OldPlanId,
    string OldPlanName,
    Guid NewPlanId,
    string NewPlanName,
    AuditRetentionShortening? RetentionShortened = null,
    bool ByMember = false,
    bool Unpaid = false,
    long? UnpaidAmountDue = null,
    string? UnpaidCurrency = null);
