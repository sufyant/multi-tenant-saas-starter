using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// R15: a row of a value holds one value: a feature's state, or a limit's number of 0 or more, or unlimited, which is a column of its own,
// never a number. The key and its kind are the code's (EntitlementKeys), and EntitlementValue.For checks them before a row is written.
// No constraint lists the keys, so a product adds a key to its lists without a migration of the template's tables (X3), as a meter
// (PlanMeterConfiguration). A plan's values and an override share the columns and the rules. The Activity log period is unlimited or at
// least its floor (E1, R19).
internal static class EntitlementColumns
{
    public const int KeyMaxLength = 100;

    public static void Configure<T>(EntityTypeBuilder<T> row, string table)
        where T : class
    {
        row.Property<string>("Key").HasMaxLength(KeyMaxLength);
        row.ToTable(builder => builder.HasCheckConstraint(
            $"ck_{table}_one_value",
            "(enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) OR (enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)"));
        row.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_maximum", "maximum >= 0"));
        row.ToTable(builder => builder.HasCheckConstraint(
            $"ck_{table}_audit_retention_floor",
            $"key <> '{LimitKeys.AuditRetentionDays}' OR unlimited OR maximum >= {AuditRetention.Floor}"));
    }
}
