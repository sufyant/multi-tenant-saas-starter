using ControlPlane.Application.Tenants.TenantCreation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// The tenant creation saga's record (W6). Wolverine raises its version on each change; as the concurrency token the version is in the
// update's WHERE clause, so of two messages handled at once the second fails and is tried again (S8).
internal sealed class TenantCreationSagaConfiguration : IEntityTypeConfiguration<TenantCreationSaga>
{
    public void Configure(EntityTypeBuilder<TenantCreationSaga> saga)
    {
        saga.Property(o => o.Id).ValueGeneratedNever();
        saga.Property(o => o.State).HasConversion<string>().HasMaxLength(20);
        saga.Property(o => o.Version).IsConcurrencyToken();
    }
}
