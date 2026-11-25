namespace Projects.Api.Tenant.Projects;

/// <summary>A new project. The name is kept without the spaces around it.</summary>
public sealed record CreateProjectRequest(string Name);
