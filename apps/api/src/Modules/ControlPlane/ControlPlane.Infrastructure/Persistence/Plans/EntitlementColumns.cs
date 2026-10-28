using ControlPlane.Domain.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Plans;

// R15: a row of a value holds a key of the code's list and one value of the key's kind: a feature's state, or a limit's number of 0 or
// more, or unlimited, which is a column of its own, never a number. The lists come from the code, so a new key changes the model, and
// PendingModelChangesTests asks for its migration. A plan's values and an override share the columns and the rules.
internal static class EntitlementColumns
{
    public const int KeyMaxLength = 100;

    public static void Configure<T>(EntityTypeBuilder<T> row, string table)
        where T : class
    {
        row.Property<string>("Key").HasMaxLength(KeyMaxLength);
        row.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_key", CheckConstraints.OneOf("key", EntitlementKeys.All.Keys.Order(StringComparer.Ordinal))));
        row.ToTable(builder => builder.HasCheckConstraint(
            $"ck_{table}_value_of_its_kind",
            $"({CheckConstraints.OneOf("key", EntitlementKeys.Features)} AND enabled IS NOT NULL AND maximum IS NULL AND NOT unlimited) "
            + $"OR ({CheckConstraints.OneOf("key", EntitlementKeys.Limits)} AND enabled IS NULL AND (maximum IS NOT NULL) <> unlimited)"));
        row.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_maximum", "maximum >= 0"));
    }
}
