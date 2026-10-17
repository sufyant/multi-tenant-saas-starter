using System.Net;
using System.Net.Http.Json;
using Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Tenancy;

namespace Api.IntegrationTests.Platform;

// The system admin's endpoints for tenants and users: each needs its own permission behind the system door (A5, A6), and none takes
// an idempotency key, because each reads or sets a state (I1).
public sealed class PlatformAccessTests(Database database) : IAsyncLifetime
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
        { "GET", "/v1/platform/tenants", "platform.tenants.read" },
        { "GET", "/v1/platform/tenants/{tenantId:guid}", "platform.tenants.read" },
        { "PATCH", "/v1/platform/tenants/{tenantId:guid}", "platform.tenants.update" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/suspend", "platform.tenants.suspend" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/reactivate", "platform.tenants.reactivate" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/close", "platform.tenants.close" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/reopen", "platform.tenants.reopen" },
        { "GET", "/v1/platform/users", "platform.users.read" },
        { "POST", "/v1/platform/users/{userId:guid}/suspend", "platform.users.suspend" },
        { "POST", "/v1/platform/users/{userId:guid}/reactivate", "platform.users.reactivate" },
    };

    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/v1/platform/tenants" },
        { "GET", "/v1/platform/tenants/{tenantId:guid}" },
        { "PATCH", "/v1/platform/tenants/{tenantId:guid}" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/suspend" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/reactivate" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/close" },
        { "POST", "/v1/platform/tenants/{tenantId:guid}/reopen" },
        { "GET", "/v1/platform/users" },
        { "POST", "/v1/platform/users/{userId:guid}/suspend" },
        { "POST", "/v1/platform/users/{userId:guid}/reactivate" },
    };

    // The provider's app is the platform, so the old door is gone: no route answers under /v1/admin, also for a system admin.
    public static TheoryData<string, string> OldRoutes => new()
    {
        { "POST", "/v1/admin/tenants" },
        { "GET", "/v1/admin/tenants" },
        { "GET", "/v1/admin/tenants/{tenantId:guid}" },
        { "PATCH", "/v1/admin/tenants/{tenantId:guid}" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/suspend" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/reactivate" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/close" },
        { "POST", "/v1/admin/tenants/{tenantId:guid}/reopen" },
        { "GET", "/v1/admin/users" },
        { "PATCH", "/v1/admin/users/{userId:guid}" },
        { "DELETE", "/v1/admin/users/{userId:guid}" },
        { "POST", "/v1/admin/users/{userId:guid}/suspend" },
        { "POST", "/v1/admin/users/{userId:guid}/reactivate" },
        { "GET", "/v1/admin/audit-log" },
    };

    [Theory]
    [MemberData(nameof(OldRoutes))]
    public async Task CallOldAdminRoute_AsASystemAdmin_IsNotFound(string method, string route)
    {
        var client = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Every endpoint behind the system door is on the platform path, and no endpoint is on the old one.
    [Fact]
    public void MapPlatformEndpoints_BehindTheSystemDoor_AreAllOnThePlatformPath()
    {
        var routes = _api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => data.Policy == AccessPolicies.SystemAdmin))
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        routes.ShouldNotBeEmpty();
        routes.ShouldAllBe(route => route.StartsWith("/v1/platform/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public void MapPlatformEndpoint_Each_RequiresItsPermissionAndNoIdempotencyKey(string method, string route, string permission)
    {
        var endpoint = EndpointOf(method, route);

        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(data => data.Policy).ShouldContain(permission);
        endpoint.Metadata.GetMetadata<IdempotencyKeyRequired>().ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallPlatformEndpoint_WithoutAnIdempotencyKey_IsCarriedOut(string method, string route)
    {
        var client = _api.CreateClient(await _catalog.AddSystemAdminAsync(), secondFactor: true);

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallPlatformEndpoint_AsATenantMember_IsForbidden(string method, string route)
    {
        var tenant = await _catalog.AddTenantAsync();
        var client = _api.CreateClient(await _catalog.AddMemberAsync(tenant.Id, role: "Owner"), secondFactor: true);

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public async Task CallPlatformEndpoint_SystemAdminWithoutASecondFactor_IsForbidden(string method, string route)
    {
        var client = _api.CreateClient(await _catalog.AddSystemAdminAsync());

        var response = await client.SendAsync(await RequestAsync(method, route), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private RouteEndpoint EndpointOf(string method, string route) =>
        _api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Single(endpoint =>
            endpoint.RoutePattern.RawText == route
            && endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Contains(method));

    // The route, with an active tenant and a user who is not a system admin in its place. Every request carries a body that renaming
    // and suspending read; the other endpoints read none.
    private async Task<HttpRequestMessage> RequestAsync(string method, string route)
    {
        var tenant = await _catalog.AddTenantAsync();
        var user = await _catalog.UserIdOfAsync(await _catalog.AddUserAsync());
        var path = route.Replace("{tenantId:guid}", tenant.Id.ToString(), StringComparison.Ordinal)
            .Replace("{userId:guid}", user.ToString(), StringComparison.Ordinal);

        return new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(new { name = "Globex Corp", reason = "Unpaid invoices" }) };
    }
}
