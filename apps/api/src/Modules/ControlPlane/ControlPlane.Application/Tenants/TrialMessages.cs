using System.Security.Cryptography;
using System.Text;
using ControlPlane.Application.Tenants.EndTenantTrial;
using ControlPlane.Application.Tenants.WarnTenantTrialEnding;
using Wolverine;

namespace ControlPlane.Application.Tenants;

/// <summary>
/// The messages a trial's end schedules (step 40d): the end itself, and the reminder <see cref="WarningDays"/> days before it (13.16). A
/// trial that ends sooner than that gets no reminder.
/// </summary>
public static class TrialMessages
{
    public const int WarningDays = 3;

    public static IEnumerable<object> Of(Guid tenantId, DateTimeOffset endsAt, DateTimeOffset now)
    {
        yield return new EndTenantTrialCommand(tenantId, endsAt).ScheduledAt(endsAt);
        var warnAt = endsAt - TimeSpan.FromDays(WarningDays);
        if (warnAt > now)
        {
            yield return new WarnTenantTrialEndingCommand(tenantId, endsAt).ScheduledAt(warnAt);
        }
    }

    /// <summary>
    /// The id of the reminder's event, the same for the same tenant and end, so a copy of the reminder tells nobody twice: the first 16 bytes
    /// of a SHA-256, marked as a version 8 UUID (RFC 9562, section 5.8), as a notification's id is.
    /// </summary>
    public static Guid WarningIdOf(Guid tenantId, DateTimeOffset endsAt)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes($"{tenantId:N}/trial-ending/{endsAt.ToUnixTimeMilliseconds()}"), hash);
        hash[6] = (byte)((hash[6] & 0x0F) | 0x80);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        return new Guid(hash[..16], bigEndian: true);
    }
}
