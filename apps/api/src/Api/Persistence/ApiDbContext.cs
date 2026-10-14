using Api.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Tenancy;

namespace Api.Persistence;

public sealed class ApiDbContext(DbContextOptions<ApiDbContext> options) : DbContext(options)
{
    public const string Schema = "api";

    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdempotencyKey>(key =>
        {
            key.HasKey(row => new { row.Subject, row.Key });
            key.Property(row => row.Subject).HasMaxLength(255);
            key.Property(row => row.Key).HasMaxLength(255);
            key.Property(row => row.Method).HasMaxLength(16);
            key.Property(row => row.RequestHash).HasMaxLength(64);
            key.Property(row => row.Result).HasColumnType("jsonb");
            key.HasIndex(row => row.CreatedAt);
        });
    }
}

internal sealed class ApiDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ApiDbContext>
{
    public ApiDbContext CreateDbContext(string[] args) =>
        new(TenancyServiceCollectionExtensions.ModuleDbContextOptions<ApiDbContext>(ApiDbContext.Schema, "Host=design-time"));
}
