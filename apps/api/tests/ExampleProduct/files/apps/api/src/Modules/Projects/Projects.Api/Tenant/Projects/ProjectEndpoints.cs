using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Projects.Api.Authentication;
using Projects.Application.Projects;
using Projects.Application.Projects.CreateProject;
using Projects.Application.Projects.GetMember;
using Projects.Application.Projects.GetProjectLimit;
using Projects.Application.Projects.ListProjects;
using SharedKernel;
using Wolverine;

namespace Projects.Api.Tenant.Projects;

/// <summary>The projects of the tenant in the route, the example product's endpoints (X4): a product endpoint names a product permission and a product feature as a template endpoint does (X1).</summary>
public static class ProjectEndpoints
{
    // RFC 9110, section 15.3.2: the API has no GET for one project, so a new project's 201 sends no Location.
    private const string NoLocation =
        "Answers 201 with the project and without Location: the API has no GET for one project, so the request's target URI identifies it (RFC 9110, section 15.3.2).";

    public static void Map(RouteGroupBuilder tenant)
    {
        tenant.MapGet("/projects", ListProjects).RequireAuthorization(ProjectsCatalog.TenantProjectsRead).RequireAuthorization(ProjectsCatalog.ProjectsAccess);
        tenant.MapPost("/projects", CreateProject)
            .WithMetadata(new IdempotencyKeyRequired())
            .WithDescription(NoLocation)
            .RequireAuthorization(ProjectsCatalog.TenantProjectsCreate)
            .RequireAuthorization(ProjectsCatalog.ProjectsAccess)
            .Produces<ProjectResponse>(StatusCodes.Status201Created);
    }

    // The tenant comes from the route: the host has declared it, and row level security keeps the list to it.
    private static async Task<Result<ListPage<ProjectResponse>>> ListProjects(
        int? page,
        int? pageSize,
        ListSort<ListProjectsSort>? sort,
        string? q,
        IMessageBus bus,
        CancellationToken cancellationToken)
    {
        var list = ListRequest.Create(page, pageSize, sort, q);
        if (!list.IsSuccess)
        {
            return list.Error;
        }

        var projects = await bus.InvokeAsync<ListPage<ProjectView>>(new ListProjectsQuery(list.Value), cancellationToken);
        return projects.Map(ToResponse);
    }

    // The project is created inside the tenant of the route, under the request's idempotency key (I1). The limit is read before the
    // command, from the database (X3).
    private static async Task<Result<ProjectResponse>> CreateProject(
        Guid tenantId,
        CreateProjectRequest request,
        ClaimsPrincipal user,
        IMessageBus bus,
        IIdempotencyExecutor idempotency,
        CancellationToken cancellationToken)
    {
        var member = await bus.InvokeAsync<Guid>(new GetMemberQuery(user.Id()), cancellationToken);
        var limit = await bus.InvokeAsync<int?>(new GetProjectLimitQuery(tenantId), cancellationToken);
        var created = await idempotency.ExecuteAsync<ProjectView>(
            new CreateProjectCommand(tenantId, request.Name?.Trim() ?? string.Empty, member, limit), tenantId.ToString(), cancellationToken);
        return created.Map(ToResponse);
    }

    private static ProjectResponse ToResponse(ProjectView project) => new(project.Id, project.Name, project.CreatedBy, project.CreatedAt);
}
