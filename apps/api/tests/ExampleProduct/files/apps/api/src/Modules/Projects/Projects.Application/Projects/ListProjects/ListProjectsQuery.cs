using SharedKernel;

namespace Projects.Application.Projects.ListProjects;

/// <summary>One page of the tenant's projects, sorted and searched by name (K5).</summary>
[MessageName("list-projects")]
public sealed record ListProjectsQuery(ListRequest<ListProjectsSort> List);
