using System.Security.Cryptography;
using System.Text;
using ControlPlane.Contracts.Users;
using JasperFx;
using Notifications.Application.Notifications.DeliverNotification;
using Wolverine;

namespace Notifications.Application.Notifications;

/// <summary>
/// The fan-out on write of one event (U3; Kleppmann, Chapter 1): one notification for each recipient, written when the event arrives, so each
/// has their own read state and someone who becomes a recipient later sees no older one. Nobody is told of their own action, and a deleted
/// or suspended user is told nothing. A handler names the type, the recipients and the values; it never names a channel (U4).
/// </summary>
public static class NotificationFanOut
{
    // The delivery declares the recipient, never a tenant (R4): a message keeps the tenant of the envelope it came from otherwise (W3).
    private static readonly DeliveryOptions WithoutTenant = new() { TenantId = StorageConstants.DefaultTenantId };

    public static OutgoingMessages To(
        string type,
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid? tenantId,
        IReadOnlyDictionary<string, string> values,
        IEnumerable<UserContact> recipients,
        Guid? actorId)
    {
        OutgoingMessages messages = [];
        foreach (var recipient in recipients.Where(recipient => recipient.Active && recipient.UserId != actorId).DistinctBy(recipient => recipient.UserId))
        {
            messages.Add(new DeliverNotificationCommand(
                    IdOf(eventId, type, recipient.UserId), type, recipient.UserId, tenantId, values, occurredAt, recipient.Email, recipient.Language)
                .WithDeliveryOptions(WithoutTenant));
        }

        return messages;
    }

    /// <summary>The name of a person in a text: the full name, else the address; the two are what a screen shows of a user.</summary>
    public static string NameOf(UserContact person) => person.DisplayName ?? person.Email ?? "";

    // The same event, type and recipient always give the same id: the first 16 bytes of a SHA-256, marked as a version 8 UUID (RFC 9562,
    // section 5.8).
    private static Guid IdOf(Guid eventId, string type, Guid userId)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{eventId:N}/{type}/{userId:N}"), hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}
