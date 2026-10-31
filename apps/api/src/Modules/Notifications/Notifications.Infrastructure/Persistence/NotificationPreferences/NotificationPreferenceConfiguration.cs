using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;
using Notifications.Domain.NotificationPreferences;
using Notifications.Domain.Notifications;

namespace Notifications.Infrastructure.Persistence.NotificationPreferences;

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> preference)
    {
        preference.ToTable("notification_preferences");
        preference.HasKey(p => p.UserId);
        preference.Property(p => p.UserId).ValueGeneratedNever();
        preference.Property(p => p.Channels)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.DocumentConverter<Dictionary<NotificationChannel, bool>>(), JsonColumn.DocumentComparer<Dictionary<NotificationChannel, bool>>());
        preference.Property(p => p.Categories)
            .HasColumnType("jsonb")
            .HasConversion(
                JsonColumn.DocumentConverter<Dictionary<string, Dictionary<NotificationChannel, bool>>>(),
                JsonColumn.DocumentComparer<Dictionary<string, Dictionary<NotificationChannel, bool>>>());
        preference.Ignore(p => p.IsDefault);

        // R15, U5: only the choices that differ from the defaults are stored: a main switch of a channel of the catalog, and a choice of a
        // category of the catalog that is not required.
        var channels = string.Join(", ", Enum.GetNames<NotificationChannel>().Select(channel => $"'{channel}'"));
        var categories = string.Join(", ", NotificationCatalog.CategoryKinds.Where(category => !category.Required).Select(category => $"'{category.Name}'"));
        preference.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_notification_preferences_channels",
                $"jsonb_typeof(channels) = 'object' AND (channels - ARRAY[{channels}]::text[]) = '{{}}'::jsonb");
            table.HasCheckConstraint(
                "ck_notification_preferences_categories",
                $"jsonb_typeof(categories) = 'object' AND (categories - ARRAY[{categories}]::text[]) = '{{}}'::jsonb");
            table.HasCheckConstraint("ck_notification_preferences_not_default", "NOT (channels = '{}'::jsonb AND categories = '{}'::jsonb)");
        });
    }
}
