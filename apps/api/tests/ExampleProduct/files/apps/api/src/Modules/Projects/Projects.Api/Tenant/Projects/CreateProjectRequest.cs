namespace Projects.Api.Tenant.Projects;

/// <param name="Name">The project's name, kept without the spaces around it.</param>
public sealed record CreateProjectRequest(string Name);
