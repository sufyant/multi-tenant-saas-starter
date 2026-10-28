using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using ControlPlane.Infrastructure.Persistence.Plans;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Tenants;

// A system admin's value of one key for one tenant. Only the platform writes it, in the tenant's transaction under the tenant's lock, and
// the platform's previews read it across tenants, as they read the tenant's summary. So it is not a tenant entity: it is outside row level
// security, as the tenant row and its summary are (R6, R12). It stays after the tenant's purge, as the tenant row does.
internal sealed class EntitlementOverrideConfiguration : IEntityTypeConfiguration<EntitlementOverride>
{
    public void Configure(EntityTypeBuilder<EntitlementOverride> entry)
    {
        entry.ToTable("entitlement_overrides");
        entry.HasKey(e => new { e.TenantId, e.Key });
        entry.Ignore(e => e.Value);
        entry.Property(e => e.Reason).HasMaxLength(EntitlementOverride.ReasonMaxLength);
        entry.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        entry.HasOne<User>().WithMany().HasForeignKey(e => e.SetBy).OnDelete(DeleteBehavior.Restrict);
        EntitlementColumns.Configure(entry, "entitlement_overrides");
    }
}
