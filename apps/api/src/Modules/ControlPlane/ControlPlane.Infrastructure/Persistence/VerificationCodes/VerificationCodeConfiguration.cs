using ControlPlane.Domain.EmailAddresses;
using ControlPlane.Domain.Users;
using ControlPlane.Domain.VerificationCodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tenancy;

namespace ControlPlane.Infrastructure.Persistence.VerificationCodes;

internal sealed class VerificationCodeConfiguration : IEntityTypeConfiguration<VerificationCode>
{
    public void Configure(EntityTypeBuilder<VerificationCode> code)
    {
        code.Property(c => c.Id).ValueGeneratedNever();
        code.Property(c => c.Purpose).HasConversion<string>().HasMaxLength(30);
        code.Property(c => c.CodeHash).HasMaxLength(32);
        code.Property(c => c.NewEmail).HasMaxLength(EmailAddress.MaxLength);
        code.HasIndex(c => new { c.UserId, c.Purpose, c.CreatedAt });
        code.HasOne<User>().WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);

        // R15: a known purpose, a change of address names its address and a deletion none, and the tries never fall below zero.
        code.ToTable(table => table.HasCheckConstraint("ck_verification_codes_purpose", CheckConstraints.OneOf<VerificationCodePurpose>("purpose")));
        code.ToTable(table => table.HasCheckConstraint(
            "ck_verification_codes_new_email",
            "(purpose = 'EmailChange') = (new_email IS NOT NULL)"));
        code.ToTable(table => table.HasCheckConstraint("ck_verification_codes_attempts", "attempts >= 0"));
    }
}
