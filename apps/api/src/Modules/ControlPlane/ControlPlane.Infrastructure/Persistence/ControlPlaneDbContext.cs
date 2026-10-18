using System.Text.Json;
using ControlPlane.Application.Tenants.TenantCreation;
using ControlPlane.Domain.Invitations;
using ControlPlane.Domain.Roles;
using ControlPlane.Domain.SystemAdmins;
using ControlPlane.Domain.Tenants;
using ControlPlane.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SharedKernel;
using Tenancy;
using Wolverine;

namespace ControlPlane.Infrastructure.Persistence;

/// <summary>
/// The <c>control_plane</c> schema: the control plane's data above tenants (tenants, users, roles, system admins), and the memberships,
/// invitations and tenant creation sagas, which belong to a tenant and are under row level security.
/// </summary>
/// <remarks>Public only because Wolverine's generated code creates it for the handlers (W9); its sets are internal to the module.</remarks>
public sealed class ControlPlaneDbContext(DbContextOptions<ControlPlaneDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "control_plane";

    // RFC 5646, section 4.4.1: a buffer of 35 characters holds any language tag a program is likely to meet.
    private const int LanguageTagMaxLength = 35;

    // A language is kept as its tag, the value it has in JSON ("en"), so a new member of the enum moves no stored value.
    private static readonly ValueConverter<Language, string> LanguageTag = new(
        language => JsonSerializer.Serialize(language, JsonSerializerOptions.Default).Trim('"'),
        tag => JsonSerializer.Deserialize<Language>($"\"{tag}\"", JsonSerializerOptions.Default));

    internal DbSet<Tenant> Tenants => Set<Tenant>();

    internal DbSet<User> Users => Set<User>();

    internal DbSet<Membership> Memberships => Set<Membership>();

    internal DbSet<Role> Roles => Set<Role>();

    internal DbSet<Invitation> Invitations => Set<Invitation>();

    internal DbSet<SystemAdmin> SystemAdmins => Set<SystemAdmin>();

    internal DbSet<TenantCreationSaga> TenantCreationSagas => Set<TenantCreationSaga>();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        // K5: every field a list searches has a trigram index, which serves a "contains" match; every field it sorts by has a B-tree
        // index. pg_trgm is a trusted extension, so the migration account creates it with the database's CREATE privilege.
        modelBuilder.HasPostgresExtension("pg_trgm");

        modelBuilder.Entity<Tenant>(tenant =>
        {
            tenant.Property(t => t.Id).ValueGeneratedNever();
            tenant.Property(t => t.Name).HasMaxLength(Tenant.NameMaxLength);
            tenant.Property(t => t.Slug).HasMaxLength(63);
            tenant.HasIndex(t => t.Slug).IsUnique();
            tenant.HasIndex(t => t.Name);
            tenant.HasIndex(t => t.Name, "ix_tenants_name_trgm").HasDatabaseName("ix_tenants_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            tenant.HasIndex(t => t.Slug, "ix_tenants_slug_trgm").HasDatabaseName("ix_tenants_slug_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            tenant.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);
            tenant.Property(t => t.CancellationReason).HasMaxLength(Tenant.CancellationReasonMaxLength);
            tenant.Property(t => t.SuspensionReason).HasMaxLength(Tenant.SuspensionReasonMaxLength);

            // R12: the summary the platform's tenant list reads across tenants, where row level security hides the memberships and the
            // invitations. Tenant.Summarize writes it under the tenant's lock. TenantCatalog inserts a new tenant with SQL, without the
            // summary, which the defaults make empty.
            tenant.Property(t => t.MemberCount).HasDefaultValue(0);
            tenant.Property(t => t.OwnerCount).HasDefaultValue(0);
            tenant.HasOne<User>().WithMany().HasForeignKey(t => t.FirstOwnerId).OnDelete(DeleteBehavior.Restrict);
            tenant.Property(t => t.OwnerInvitationEmail).HasMaxLength(Invitation.EmailMaxLength);
            tenant.Property(t => t.OwnerInvitationStatus).HasConversion<string>().HasMaxLength(20);
        });

        modelBuilder.Entity<User>(user =>
        {
            user.Property(u => u.Id).ValueGeneratedNever();
            user.Property(u => u.ExternalId).HasMaxLength(255);
            user.HasIndex(u => u.ExternalId).IsUnique();
            user.HasIndex(u => u.Email);
            user.HasIndex(u => u.LastName);
            user.HasIndex(u => u.Email, "ix_users_email_trgm").HasDatabaseName("ix_users_email_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            user.HasIndex(u => u.FirstName, "ix_users_first_name_trgm").HasDatabaseName("ix_users_first_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            user.HasIndex(u => u.LastName, "ix_users_last_name_trgm").HasDatabaseName("ix_users_last_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            user.Property(u => u.Language).HasConversion(LanguageTag).HasMaxLength(LanguageTagMaxLength);
            user.Property(u => u.TimeZone).HasMaxLength(User.TimeZoneMaxLength);
            user.HasOne<User>().WithMany().HasForeignKey(u => u.SuspendedBy).OnDelete(DeleteBehavior.Restrict);

            // SystemAdminDirectory inserts the first system admin with SQL, without these columns: at the moment of the insert, with no
            // membership (R13).
            user.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
            user.Property(u => u.MembershipCount).HasDefaultValue(0);
        });

        modelBuilder.Entity<Role>(role =>
        {
            role.Property(r => r.Id).ValueGeneratedNever();
            role.Property(r => r.Name).HasMaxLength(Role.NameMaxLength);
            role.Property(r => r.BuiltIn).HasConversion<string>().HasMaxLength(20);
            role.Ignore(r => r.Permissions);

            // Built-in roles are shared by every tenant; their permissions come from code, so their rows hold none.
            role.HasData(BuiltInRoles.All.Select(builtIn => new { builtIn.Id, builtIn.Name, builtIn.BuiltIn }));
        });

        modelBuilder.Entity<Membership>(membership =>
        {
            membership.HasKey(m => new { m.TenantId, m.UserId });
            membership.HasOne<Tenant>().WithMany().HasForeignKey(m => m.TenantId);
            membership.HasOne<User>().WithMany().HasForeignKey(m => m.UserId);
            membership.HasOne<Role>().WithMany().HasForeignKey(m => m.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(invitation =>
        {
            invitation.Property(i => i.Id).ValueGeneratedNever();
            invitation.Property(i => i.Email).HasMaxLength(Invitation.EmailMaxLength);
            // The column keeps its first name until the maintainer decides its rename, an expand and contract change (deployment.md,
            // section 8).
            invitation.Property(i => i.SecretHash).HasMaxLength(64).HasColumnName("token_hash");
            invitation.Property(i => i.SignUpInvitationId).HasMaxLength(255);
            invitation.HasIndex(i => i.SecretHash).IsUnique().HasDatabaseName("ix_invitations_token_hash");
            // The partial unique index ix_invitations_pending_email keeps one pending invitation for each tenant and lower(email) (K2).
            // It is on an expression, which EF Core does not model, so the migration AddInvitationIndexes creates it in SQL.
            invitation.HasIndex(i => i.ExpiresAt);
            invitation.HasIndex(i => i.Email);
            invitation.HasIndex(i => i.Email, "ix_invitations_email_trgm").HasDatabaseName("ix_invitations_email_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            invitation.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
            invitation.Property(i => i.Language).HasConversion(LanguageTag).HasMaxLength(LanguageTagMaxLength);
            invitation.HasOne<Tenant>().WithMany().HasForeignKey(i => i.TenantId);
            invitation.HasOne<Role>().WithMany().HasForeignKey(i => i.RoleId).OnDelete(DeleteBehavior.Cascade);
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedBy).OnDelete(DeleteBehavior.Restrict);
            invitation.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedBy).OnDelete(DeleteBehavior.Restrict);
        });

        // The tenant creation saga's record (W6). Wolverine raises its version on each change; as the concurrency token the version is
        // in the update's WHERE clause, so of two messages handled at once the second fails and is tried again (S8).
        modelBuilder.Entity<TenantCreationSaga>(saga =>
        {
            saga.Property(o => o.Id).ValueGeneratedNever();
            saga.Property(o => o.State).HasConversion<string>().HasMaxLength(20);
            saga.Property(o => o.Version).IsConcurrencyToken();
        });

        modelBuilder.Entity<SystemAdmin>(admin =>
        {
            admin.HasKey(a => a.UserId);
            admin.HasOne<User>().WithOne().HasForeignKey<SystemAdmin>(a => a.UserId);
        });
    }
}
