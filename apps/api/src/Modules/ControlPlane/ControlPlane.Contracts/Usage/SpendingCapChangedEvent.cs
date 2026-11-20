using SharedKernel;

namespace ControlPlane.Contracts.Usage;

/// <summary>
/// A member who manages billing set, changed or removed the tenant's spending cap (step 40c; 11.03), which the tenant's Activity log records
/// by that member (12.12). Published through the outbox with the tenant's name of that moment (K3). The tenant's id travels in the envelope
/// too (W3).
/// </summary>
/// <param name="EventId">Chosen by the publisher, so a subscriber that receives the event twice can tell.</param>
/// <param name="ChangedBy">The user id of the member.</param>
/// <param name="OldAmount">The cap before, in minor units excl. VAT; none when there was none.</param>
/// <param name="NewAmount">The cap now; none when the member removed it.</param>
/// <param name="Currency">The deployment's currency, an ISO 4217 code.</param>
[MessageName("spending-cap-changed")]
public sealed record SpendingCapChangedEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    string TenantName,
    Guid ChangedBy,
    long? OldAmount,
    long? NewAmount,
    string Currency);
