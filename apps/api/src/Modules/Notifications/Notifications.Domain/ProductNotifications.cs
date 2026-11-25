using Notifications.Domain.Notifications;

namespace Notifications.Domain;

/// <summary>
/// The product's notification types (X7), with the rules of the template's types (U1 to U5, U9). The template keeps this list empty. A type
/// has the name of its event's audit operation in <c>ProductAuditOperations</c>, or is named apart with its reason. Its texts are in
/// <c>packages/emails/messages/product</c>. A product module sends it with <c>SendNotificationCommand</c>, so it needs no handler in this
/// module.
/// </summary>
public static class ProductNotifications
{
    /// <summary>The product's types, in the order of the catalog.</summary>
    public static IReadOnlyList<NotificationKind> Kinds { get; } = [];

    /// <summary>The product's types whose name is no audit operation, each with its reason (U9).</summary>
    public static IReadOnlyDictionary<string, string> NamedApart { get; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
