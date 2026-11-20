using SharedKernel;

namespace Notifications.Application.Usage.SendUsageNotice;

/// <summary>
/// The second step of a notice of a meter's use (E10): its type, its values without the tenant's name, and the billing email that the first
/// step read in Billing's transaction. It keeps the tenant of the event's envelope (W3).
/// </summary>
[MessageName("send-usage-notice")]
public sealed record SendUsageNoticeCommand(
    string Type,
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    IReadOnlyDictionary<string, string> Values,
    string? BillingEmail,
    Guid? BillingEmailSetBy);
