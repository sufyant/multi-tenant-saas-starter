using System.Security.Cryptography;
using System.Text;
using ControlPlane.Contracts.Users;
using JasperFx;
using Notifications.Application.Notifications.DeliverNotification;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Wolverine;

namespace Notifications.Application.Notifications;

/// <summary>
/// The fan-out on write of one event (U3; Kleppmann, Chapter 1): one notification for each recipient, written when the event arrives, so each
/// has their own read state and someone who becomes a recipient later sees no older one. Nobody is told of their own action, and a deleted
/// or suspended user is told nothing. A handler names the type, the recipients and the values; it never names a channel (U4). A value keeps a
/// person by user id; the email, which renders at its send, carries the names of this moment beside them, and the row never does (U2).
/// </summary>
public static class NotificationFanOut
{
    // The delivery declares the recipient, never a tenant (R4): a message keeps the tenant of the envelope it came from otherwise (W3).
    private static readonly DeliveryOptions WithoutTenant = new() { TenantId = StorageConstants.DefaultTenantId };

    public static async Task<OutgoingMessages> ToAsync(
        string type,
        Guid eventId,
        DateTimeOffset occurredAt,
        Guid? tenantId,
        IReadOnlyDictionary<string, string> values,
        IEnumerable<UserContact> recipients,
        Guid? actorId,
        IUserDirectory users,
        CancellationToken cancellationToken)
    {
        var told = recipients.Where(recipient => recipient.Active && recipient.UserId != actorId).DistinctBy(recipient => recipient.UserId).ToList();
        if (told.Count == 0)
        {
            return [];
        }

        var people = await users.FindNamesAsync(PeopleOf(type, values), cancellationToken);
        OutgoingMessages messages = [];
        foreach (var recipient in told)
        {
            messages.Add(new DeliverNotificationCommand(
                    IdOf(eventId, type, recipient.UserId),
                    type,
                    recipient.UserId,
                    tenantId,
                    values,
                    occurredAt,
                    recipient.Email,
                    recipient.Language,
                    recipient.TimeZone,
                    people)
                .WithDeliveryOptions(WithoutTenant));
        }

        return messages;
    }

    /// <summary>The user ids that the values of a type keep for its people.</summary>
    public static IReadOnlyCollection<Guid> PeopleOf(string type, IReadOnlyDictionary<string, string> values) =>
        [.. NotificationCatalog.Of(type).Values
            .Where(value => value.Kind == NotificationValueKind.Person && values.ContainsKey(value.Name))
            .Select(value => Guid.Parse(values[value.Name]))
            .Distinct()];

    /// <summary>A person as a value stores them: the user id, never a name (K3).</summary>
    public static string PersonOf(Guid userId) => userId.ToString();

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
