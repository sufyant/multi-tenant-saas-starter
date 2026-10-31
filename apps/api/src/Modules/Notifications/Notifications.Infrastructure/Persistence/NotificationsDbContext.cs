using Microsoft.EntityFrameworkCore;
using Notifications.Application.Notifications;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;
using Notifications.Infrastructure.Persistence.NotificationPreferences;
using Notifications.Infrastructure.Persistence.Notifications;
using Tenancy;
using Wolverine;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// The <c>notifications</c> schema: each user's notifications and email preferences (U1 to U8). Both tables belong to a user, not to a
/// tenant (R6): row level security, forced, shows a transaction only the rows of the user it declares (R4, R11). The tenant purge and the
/// retention delete across users through two narrow functions (U8).
/// </summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "notifications";

    // The bells whose notifications the open transaction changed, which the live channel signals after its commit (U6).
    private readonly HashSet<(Guid UserId, Inbox Inbox)> _signals = [];

    // The user the open transaction declared (R4).
    private Guid? _userId;

    internal DbSet<Notification> Notifications => Set<Notification>();

    internal DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Notification>().Where(entry => entry.State is EntityState.Added or EntityState.Modified))
        {
            var notification = entry.Entity;
            _signals.Add((notification.UserId, new Inbox(notification.Bell, notification.Bell == NotificationBell.Platform ? null : notification.TenantId)));
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Declares the user whose rows the rest of the transaction reads and writes (R4), and remembers them for the live signal.</summary>
    internal async Task DeclareRecipientAsync(Guid userId, CancellationToken cancellationToken)
    {
        await DeclareUserAsync(userId, cancellationToken);
        _userId = userId;
    }

    /// <summary>Signals the declared user's bell after the commit, for a write that the change tracker does not see (U6).</summary>
    internal void SignalAfterCommit(Inbox inbox) =>
        _signals.Add((_userId ?? throw new InvalidOperationException("A bell is signalled for the user the transaction declares."), inbox));

    internal IReadOnlyCollection<(Guid UserId, Inbox Inbox)> TakeSignals()
    {
        (Guid, Inbox)[] signals = [.. _signals];
        _signals.Clear();
        return signals;
    }

    internal void DiscardSignals() => _signals.Clear();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfiguration(new NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new NotificationPreferenceConfiguration());
    }
}
