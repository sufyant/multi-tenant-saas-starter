using Audit.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;
using Wolverine;

namespace Audit.Infrastructure.Persistence;

/// <summary>
/// The <c>audit</c> schema: each tenant's log, a table with a tenant column under row level security, and the platform's log, a table
/// without one, which only a transaction that declares neither a tenant nor a user may read (K3, R6, R11).
/// </summary>
/// <remarks>Public only because Wolverine's generated code creates it for the handlers (W9); its sets are internal to the module.</remarks>
public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "audit";

    internal DbSet<AuditEntry> Entries => Set<AuditEntry>();

    internal DbSet<PlatformAuditEntry> PlatformEntries => Set<PlatformAuditEntry>();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.Entity<AuditEntry>(entry =>
        {
            entry.ToTable("entries");
            MapRecord(entry);

            // The cursor of the tenant's log reads newest first inside the tenant that row level security keeps (K5). "TenantId" is
            // the tenant column that TenantDbContext gives every tenant entity.
            entry.Property<Guid>("TenantId");
            entry.HasIndex("TenantId", nameof(AuditRecord.OccurredAt), nameof(AuditRecord.Id));
        });

        modelBuilder.Entity<PlatformAuditEntry>(entry =>
        {
            entry.ToTable("platform_entries");
            MapRecord(entry);
            entry.Property(e => e.ActorKind).IsRequired();
            entry.Property(e => e.TargetKind).IsRequired();
            entry.Property(e => e.TargetId).IsRequired();
            entry.HasIndex(e => new { e.OccurredAt, e.Id });
        });
    }

    private static void MapRecord<TRecord>(EntityTypeBuilder<TRecord> entry)
        where TRecord : AuditRecord
    {
        entry.Property(e => e.Id).ValueGeneratedNever();
        entry.Property(e => e.ActorKind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.Kind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.Operation).HasMaxLength(AuditRecord.OperationMaxLength);
        entry.Property(e => e.TargetKind).HasConversion<string>().HasMaxLength(20);
        entry.Property(e => e.TargetName).HasMaxLength(AuditRecord.TargetNameMaxLength);
        entry.Property(e => e.Details).HasColumnType("jsonb");
    }
}
