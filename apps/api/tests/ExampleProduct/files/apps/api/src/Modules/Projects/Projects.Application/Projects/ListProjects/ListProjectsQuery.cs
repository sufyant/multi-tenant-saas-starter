using SharedKernel;

namespace Projects.Application.Projects.ListProjects;

/// <summary>One page of the tenant's projects.</summary>
[MessageName("list-projects")]
public sealed record ListProjectsQuery(PageRequest Page);
