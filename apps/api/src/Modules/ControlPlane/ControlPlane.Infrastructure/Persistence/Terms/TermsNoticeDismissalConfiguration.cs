using ControlPlane.Domain.Terms;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ControlPlane.Infrastructure.Persistence.Terms;

// A user closed the notice of a terms version. It belongs to a user, above any tenant, as an acceptance does (R6).
internal sealed class TermsNoticeDismissalConfiguration : IEntityTypeConfiguration<TermsNoticeDismissal>
{
    public void Configure(EntityTypeBuilder<TermsNoticeDismissal> dismissal)
    {
        dismissal.HasKey(d => new { d.VersionId, d.UserId });
        dismissal.HasOne<TermsVersion>().WithMany().HasForeignKey(d => d.VersionId).OnDelete(DeleteBehavior.Restrict);
        dismissal.HasOne<User>().WithMany().HasForeignKey(d => d.UserId).OnDelete(DeleteBehavior.Restrict);
        dismissal.HasIndex(d => d.UserId);
    }
}
