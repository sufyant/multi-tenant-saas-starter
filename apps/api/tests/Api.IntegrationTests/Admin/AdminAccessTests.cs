using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Admin;

// The system admin's endpoints for tenants and users: each needs its own permission behind the system door (A5, A6), and none takes
// an idempotency key, because each reads or sets a state (I1).
public sealed class AdminAccessTests(Database database) : IAsyncLifetime
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly Catalog _catalog = new(database);
    private ApiFactory _api = null!;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(database.ConnectionStringFor(DatabaseRoles.Application));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    public static TheoryData<string, string, string> Endpoints => new()
    {
        { "GET", "/v1/admin/tenants", "admin.tenants.read" },
        { "GET", "/v1/admin/tenants/{tenantId:guid}", "admin.tenants.read" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/suspend", "admin.tenants.suspend" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/reactivate", "admin.tenants.reactivate" },
        { "GET", "/v1/admin/users", "admin.users.read" },
        { "POST", "/v1/admin/users/{userId:guid}/suspend", "admin.users.suspend" },
        { "POST", "/v1/admin/users/{userId:guid}/reactivate", "admin.users.reactivate" },
    };

    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/v1/admin/tenants" },
        { "GET", "/v1/admin/tenants/{tenantId:guid}" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/suspend" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/reactivate" },
        { "GET", "/v1/admin/users" },
        { "POST", "/v1/admin/users/{userId:guid}/suspend" },
        { "POST", "/v1/admin/users/{userId:guid}/reactivate" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void MapAdminEndpoint_Each_RequiresItsPermissionAndNoIdempotencyKey(string method, string route, string permission)
    {
        var endpoint = EndpointOf(method, route);

        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ShouldContain(permission);
        endpoint.Metadata.GetMetadata<IdempotencyKeyRequired>().ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallAdminEndpoint_WithoutAnIdempotencyKey_IsCarriedOut(string method, string route)
    {
        var client = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallAdminEndpoint_AsATenantMember_IsForbidden(string method, string route)
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"), secondFactor: true);

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallAdminEndpoint_SystemAdminWithoutASecondFactor_IsForbidden(string method, string route)
    {
        var client = _api.CreateClient(await _catalog.AddSystemAdminAsync());

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private RouteEndpoint EndpointOf(string method, string route) =>
        _api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(endpoint =>
            endpoint.RoutePattern.RawText == route
            && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));

    // The route, with an active tenant and a user who is not a system admin in its place.
    private async Task<HttpRequestMessage> RequestAsync(string method, string route)
    {
        var tenant = await _catalog.AddTenantAsync();
        var user = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync());
        var path = route.Replace("{tenantId:guid}", tenant.Id.ToString(), StringComparison.Ordinal)
            .Replace("{userId:guid}", user.ToString(), StringComparison.Ordinal);

        return new HttpRequestMessage(new HttpMethod(method), path);
    }
}
