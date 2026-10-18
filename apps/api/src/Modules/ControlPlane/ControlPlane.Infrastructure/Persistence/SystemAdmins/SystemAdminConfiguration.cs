using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.SystemAdmins;

internal sealed class SystemAdminConfiguration : IEntityTypeConfiguration<SystemAdmin>
{
    public void Configure(EntityTypeBuilder<SystemAdmin> admin)
    {
        admin.HasKey(a => a.UserId);
        admin.HasOne<User>().WithOne().HasForeignKey<SystemAdmin>(a => a.UserId);
    }
}
