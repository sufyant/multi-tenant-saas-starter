using Microsoft.EntityFrameworkCore;
using SharedKernel;

namespace Notifications.Infrastructure.Persistence.Notifications;

/// <summary>
/// Notifications' part of a person's data export (docs/flows/personal-data-export.md): every notification the person has, in every bell, by
/// its type, its tenant, its values as stored and its read state. It reads in a transaction of its own that declares the user, so the
/// own_notifications policy shows only the person's own (R11, U3).
/// </summary>
/// <remarks>Public because Wolverine resolves it from the message's scope (W9).</remarks>
public sealed class NotificationDataExporter(NotificationsDbContext notifications) : IPersonalDataExporter
{
    public async Task<IReadOnlyList<PersonalDataPart>> ExportAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var transaction = await notifications.Database.BeginTransactionAsync(cancellationToken);
        await notifications.DeclareUserAsync(userId, cancellationToken);
        var found = await notifications.Notifications
            .AsNoTracking()
            .Where(notification => notification.UserId == userId)
            .OrderBy(notification => notification.CreatedAt)
            .ThenBy(notification => notification.Id)
            .ToListAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return
        [
            new PersonalDataPart(
                "notifications",
                found.Select(notification => new { notification.Type, notification.TenantId, notification.Values, notification.CreatedAt, notification.ReadAt }).ToList()),
        ];
    }
}
