using Api.Me;
using Api.Tenant;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.IntegrationTests.Authorization;

// A9, as an architecture test over the endpoints the application really maps: no me or tenant endpoint goes around the check of the
// user's state. It needs the composed host, so it lives here.
public sealed class SuspendedUserRoutesTests(Database database)
{
    [Fact]
    public async Task CheckSuspendedUserRoutes_EveryEndpointOfTheApplication_IsChecked()
    {
        await using var api = new ApiFactory(database.ApplicationConnectionString);

        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        endpoints.OfType<RouteEndpoint>().ShouldContain(endpoint => endpoint.RoutePattern.RawText!.StartsWith(SuspendedUserRoutesCheck.MePathPrefix, StringComparison.Ordinal));
        endpoints.OfType<RouteEndpoint>().ShouldContain(endpoint => endpoint.RoutePattern.RawText!.StartsWith(TenantRoutesCheck.TenantPathPrefix, StringComparison.Ordinal));
        SuspendedUserRoutesCheck.Gaps(endpoints).ShouldBeEmpty();
    }

    [Fact]
    public void CheckSuspendedUserRoutes_AnEndpointOnAMePathOutsideTheGroup_IsAGap()
    {
        var app = WebApplication.CreateBuilder().Build();
        var v1 = app.MapV1();
        v1.MapMe().MapGet("/checked", () => Results.Ok());
        v1.MapGet("/me/stray", () => Results.Ok()).RequireAuthorization();

        var gaps = SuspendedUserRoutesCheck.Gaps(EndpointsOf(app));

        gaps.ShouldBe(["/v1/me/stray: on a me path, without the me metadata"]);
    }

    // Authorization skips an anonymous endpoint, and with it the refusal of a suspended user.
    [Fact]
    public void CheckSuspendedUserRoutes_AnAnonymousEndpointInTheGroups_IsAGap()
    {
        var app = WebApplication.CreateBuilder().Build();
        var v1 = app.MapV1();
        v1.MapMe().MapGet("/open", () => Results.Ok()).AllowAnonymous();
        v1.MapTenant().MapGet("/open", () => Results.Ok()).AllowAnonymous();

        var gaps = SuspendedUserRoutesCheck.Gaps(EndpointsOf(app));

        gaps.ShouldBe(
        [
            "/v1/me/open: lets anonymous callers skip the check",
            "/v1/tenants/{tenantId:guid}/open: lets anonymous callers skip the check",
        ]);
    }

    private static IEnumerable<Endpoint> EndpointsOf(IEndpointRouteBuilder app) => app.DataSources.SelectMany(source => source.Endpoints);
}
