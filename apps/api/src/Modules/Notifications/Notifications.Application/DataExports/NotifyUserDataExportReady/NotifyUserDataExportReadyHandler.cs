using System.Globalization;
using ControlPlane.Contracts.DataExports;
using ControlPlane.Contracts.Users;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Wolverine;

namespace Notifications.Application.DataExports.NotifyUserDataExportReady;

// "Your data file is ready", to the person who asked for it, in the personal bell and by email, with a link to Account and security, never
// to the file: the download needs the signed-in person. The worker finished the export, so the person is told although they asked for
// it (U3 tells nobody of their own action; the export's completion is no action of theirs). The id of the notification follows the export,
// so a copy of the event tells the person once.
public static class NotifyUserDataExportReadyHandler
{
    public const string Type = NotificationCatalog.UserDataExportReady;

    public static async Task<OutgoingMessages> HandleAsync(DataExportReadyEvent ready, IUserDirectory users, CancellationToken cancellationToken) =>
        await NotificationFanOut.ToAsync(
            Type,
            ready.ExportId,
            ready.OccurredAt,
            tenantId: null,
            new Dictionary<string, string> { ["expiresAt"] = ready.ExpiresAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) },
            await users.FindContactsAsync([ready.UserId], cancellationToken),
            actorId: null,
            users,
            cancellationToken);
}
