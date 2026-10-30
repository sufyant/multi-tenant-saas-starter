using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;
using Notifications.Domain.EmailPreferences;

namespace Notifications.Infrastructure.Persistence.EmailPreferences;

internal sealed class EmailPreferenceConfiguration : IEntityTypeConfiguration<EmailPreference>
{
    public void Configure(EntityTypeBuilder<EmailPreference> preference)
    {
        preference.ToTable("email_preferences");
        preference.HasKey(p => p.UserId);
        preference.Property(p => p.UserId).ValueGeneratedNever();
        preference.Property(p => p.Categories)
            .HasColumnType("jsonb")
            .HasConversion(JsonColumn.Converter<bool>(), JsonColumn.Comparer<bool>());
        preference.Ignore(p => p.IsDefault);

        // R15, U5: only the choices that differ from the defaults are stored, each of a category of the catalog.
        var categories = string.Join(", ", NotificationCatalog.Categories.Select(category => $"'{category}'"));
        preference.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_email_preferences_categories",
                $"jsonb_typeof(categories) = 'object' AND (categories - ARRAY[{categories}]::text[]) = '{{}}'::jsonb");
            table.HasCheckConstraint("ck_email_preferences_not_default", "NOT (enabled AND categories = '{}'::jsonb)");
        });
    }
}
