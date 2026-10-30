using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;
using Notifications.Domain.Notifications;
using Tenancy;

namespace Notifications.Infrastructure.Persistence.Notifications;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> notification)
    {
        notification.ToTable("notifications");
        notification.HasKey(n => n.Id);
        notification.Property(n => n.Id).ValueGeneratedNever();
        notification.Property(n => n.Type).HasMaxLength(100);
        notification.Property(n => n.Bell).HasConversion<string>().HasMaxLength(20);
        notification.Property(n => n.Values)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<string>(), JsonColumn.Comparer<string>());

        // U7: a bell reads one user's rows newest first, by the moment and the id (K5); the unread count reads the unread ones only.
        notification.HasIndex(n => new { n.UserId, n.CreatedAt, n.Id });
        notification.HasIndex(n => new { n.UserId, n.Bell, n.TenantId })
            .HasFilter("read_at IS NULL")
            .HasDatabaseName("ix_notifications_unread");

        // U8: the tenant purge deletes by the tenant, and the retention by the moment.
        notification.HasIndex(n => n.TenantId);
        notification.HasIndex(n => n.CreatedAt);

        // R15: a type and a bell of the catalog, and a tenant notification about a tenant, a personal one about none.
        notification.ToTable(table =>
        {
            table.HasCheckConstraint("ck_notifications_type", CheckConstraints.OneOf("type", NotificationCatalog.Kinds.Keys.Order(StringComparer.Ordinal)));
            table.HasCheckConstraint("ck_notifications_bell", CheckConstraints.OneOf<NotificationBell>("bell"));
            table.HasCheckConstraint(
                "ck_notifications_tenant",
                $"(bell <> '{nameof(NotificationBell.Tenant)}' OR tenant_id IS NOT NULL) AND (bell <> '{nameof(NotificationBell.Personal)}' OR tenant_id IS NULL)");
            table.HasCheckConstraint("ck_notifications_values", "jsonb_typeof(\"values\") = 'object'");
        });
    }
}
