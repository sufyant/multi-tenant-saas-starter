namespace Projects.Api.Tenant.Projects;

/// <summary>A project of the tenant. <c>createdBy</c> is the user id of the member who created it.</summary>
public sealed record ProjectResponse(Guid Id, string Name, Guid CreatedBy, DateTimeOffset CreatedAt);
