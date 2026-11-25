using Microsoft.EntityFrameworkCore;
using Projects.Domain.Projects;
using Tenancy;
using Wolverine;

namespace Projects.Infrastructure.Persistence;

/// <summary>The <c>projects</c> schema: each tenant's projects, under row level security (R1).</summary>
/// <remarks>Public (W9); its sets are internal to the module.</remarks>
public sealed class ProjectsDbContext(DbContextOptions<ProjectsDbContext> options, IMessageContext? messaging = null)
    : TenantDbContext(options, messaging)
{
    public const string Schema = "projects";

    internal DbSet<Project> Projects => Set<Project>();

    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Project>(project =>
        {
            project.ToTable("projects");
            project.HasKey(p => p.Id);
            project.Property<Guid>("TenantId");
            project.Property(p => p.Name).HasMaxLength(Project.NameMaxLength);
            project.HasIndex("TenantId", nameof(Project.CreatedAt));
        });
    }
}
