using ControlPlane.Application.Tenants.TenantPurge;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// The tenant purge's record (W6). Wolverine raises its version on each change; as the concurrency token the version is in the update's
// WHERE clause, so of two messages handled at once the second fails and is tried again (S8). The steps it waits for are a text array.
internal sealed class TenantPurgeSagaConfiguration : IEntityTypeConfiguration<TenantPurgeSaga>
{
    public void Configure(EntityTypeBuilder<TenantPurgeSaga> saga)
    {
        saga.Property(o => o.Id).ValueGeneratedNever();
        saga.Property(o => o.State).HasConversion<string>().HasMaxLength(20);
        saga.Property(o => o.Version).IsConcurrencyToken();
        saga.ToTable(table => table.HasCheckConstraint("ck_tenant_purge_sagas_state", CheckConstraints.OneOf<TenantPurgeSagaState>("state")));
    }
}
