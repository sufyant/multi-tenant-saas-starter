using SharedKernel;

namespace Projects.Domain.Projects;

/// <summary>
/// A project of the tenant, the example product's one business record (X4). It belongs to its tenant, under the tenant's row level
/// security (R1), and keeps the member who created it.
/// </summary>
internal sealed class Project(Guid id, string name, Guid createdBy, DateTimeOffset createdAt) : ITenantEntity
{
    public const int NameMaxLength = 100;

    public Guid Id { get; private set; } = id;

    public string Name { get; private set; } = name;

    /// <summary>The user id of the member who created the project.</summary>
    public Guid CreatedBy { get; private set; } = createdBy;

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;
}
