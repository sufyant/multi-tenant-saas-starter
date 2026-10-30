using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Notifications.Application.Notifications;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using SharedKernel;

namespace Notifications.Infrastructure.Persistence.Notifications;

/// <summary>The reads of a bell on the module's DbContext, without tracking (Q2).</summary>
/// <remarks>Public because Wolverine's generated code builds it (W1, W9).</remarks>
public sealed class NotificationQueries(NotificationsDbContext notifications) : INotificationQueries
{
    Task INotificationQueries.DeclareUserAsync(Guid userId, CancellationToken cancellationToken) =>
        notifications.DeclareRecipientAsync(userId, cancellationToken);

    // Newest first, by the moment and then the id. The cursor's place is compared as one row value, which the index on the user, the
    // moment and the id serves (K5).
    async Task<IReadOnlyList<NotificationView>> INotificationQueries.ListAsync(Inbox inbox, CursorRequest page, Language language, CancellationToken cancellationToken)
    {
        var bell = notifications.Notifications.Where(Shows(inbox));
        var after = page.After is { } place
            ? bell.Where(n => EF.Functions.LessThan(ValueTuple.Create(n.CreatedAt, n.Id), ValueTuple.Create(place.At, place.Id)))
            : bell;

        var rows = await after
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(page.Limit + 1)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return [.. rows.Select(n => ViewOf(n, language))];
    }

    Task<int> INotificationQueries.CountUnreadAsync(Inbox inbox, CancellationToken cancellationToken) =>
        notifications.Notifications.Where(Shows(inbox)).CountAsync(n => n.ReadAt == null, cancellationToken);

    // What one bell shows (U7). Row level security already keeps the rows to the declared user.
    internal static Expression<Func<Notification, bool>> Shows(Inbox inbox) => inbox.Bell switch
    {
        NotificationBell.Tenant => n => (n.Bell == NotificationBell.Tenant && n.TenantId == inbox.TenantId) || n.Bell == NotificationBell.Personal,
        NotificationBell.Personal => n => n.Bell == NotificationBell.Personal,
        _ => n => n.Bell == NotificationBell.Platform,
    };

    // The texts are rendered at the read, in the reader's language, from the type and the stored values (U2).
    private static NotificationView ViewOf(Notification notification, Language language)
    {
        var kind = NotificationCatalog.Of(notification.Type);
        var text = NotificationText.Render(notification.Type, notification.Values, language);
        return new(
            notification.Id, notification.Type, kind.Category, text.Title, text.Body, kind.Link.ToString(), notification.TenantId, notification.CreatedAt, notification.ReadAt);
    }
}
