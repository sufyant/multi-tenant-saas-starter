using Audit.Domain.AuditEntries;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;
using Wolverine;

namespace Audit.Infrastructure.Persistence;

/// <summary>
/// The <c>audit</c> schema: each tenant's log, a table with a tenant column under row level security, and the platform's log, a table
/// without one, which only a transaction that declares neither a tenant nor a user may read (K3, R6, R11).
/// </summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "audit";

    internal DbSet<TenantAuditEntry> Entries => Set<TenantAuditEntry>();

    internal DbSet<PlatformAuditEntry> PlatformEntries => Set<PlatformAuditEntry>();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<TenantAuditEntry>(entry =>
        {
            entry.ToTable("entries");
            MapEntry(entry);

            // The cursor of the tenant's log reads newest first inside the tenant that row level security keeps (K5). "TenantId" is
            // the tenant column that TenantDbContext gives every tenant entity.
            entry.Property<Guid>("TenantId");
            entry.HasIndex("TenantId", nameof(AuditEntry.OccurredAt), nameof(AuditEntry.Id));
        });

        modelBuilder.Entity<PlatformAuditEntry>(entry =>
        {
            entry.ToTable("platform_entries");
            MapEntry(entry);
            // K5: the tenant filter of the platform's log reads the entries about one target, in the order of the log.
            entry.HasIndex(e => new { e.TargetKind, e.TargetId, e.OccurredAt, e.Id });
        });
    }

    private static void MapEntry<TEntry>(EntityTypeBuilder<TEntry> entry)
        where TEntry : AuditEntry
    {
        // R18, K3: the key leads with the moment, so a later retention can partition each log by it. The key's index also serves the
        // platform's cursor, which reads by the moment and the id.
        entry.HasKey(e => new { e.OccurredAt, e.Id });
        entry.Property(e => e.Id).ValueGeneratedNever();
        entry.Property(e => e.ActorKind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.Operation).HasMaxLength(AuditEntry.OperationMaxLength);
        entry.Property(e => e.TargetKind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.TargetName).HasMaxLength(AuditEntry.TargetNameMaxLength);
        entry.Property(e => e.Details).HasColumnType("jsonb");

        // R15: an actor, a kind and a target of the kinds the code knows. The caller named the table first.
        var table = entry.Metadata.GetTableName();
        entry.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_actor_kind", CheckConstraints.OneOf<AuditActorKind>("actor_kind")));
        entry.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_kind", CheckConstraints.OneOf<AuditEntryKind>("kind")));
        entry.ToTable(builder => builder.HasCheckConstraint($"ck_{table}_target_kind", CheckConstraints.OneOf<AuditTargetKind>("target_kind")));
    }
}
