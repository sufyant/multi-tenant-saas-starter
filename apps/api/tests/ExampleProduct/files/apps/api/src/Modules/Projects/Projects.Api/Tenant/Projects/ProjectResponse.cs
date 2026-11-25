namespace Projects.Api.Tenant.Projects;

/// <param name="CreatedBy">The user id of the member who created the project.</param>
public sealed record ProjectResponse(Guid Id, string Name, Guid CreatedBy, DateTimeOffset CreatedAt);
