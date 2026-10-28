using System.Reflection;
using ControlPlane.Contracts.Plans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.ArchitectureTests;

// Every tenant endpoint states the feature of the tenant's plan it needs, as an authorization policy, or that it needs none
// (NotPlanGated). An endpoint without either is a forgotten line, and an endpoint with both says two things.
public sealed class PlanGateTests : IAsyncLifetime
{
    private const string TenantPath = "/v1/tenants/";

    // The feature keys: an endpoint asks for one of these by name, as it asks for a permission.
    private static readonly string[] FeatureList =
    [
        .. typeof(Features).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!),
    ];

    private readonly ComposedApplication _application = new();
    private IReadOnlyList<RouteEndpoint> _endpoints = [];

    public ValueTask InitializeAsync()
    {
        _endpoints = [.. _application.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void GatePlan_EveryTenantEndpoint_StatesAFeatureOrNone()
    {
        var tenantEndpoints = _endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(TenantPath, StringComparison.Ordinal));

        var unstated = tenantEndpoints.Where(endpoint => !StatesOneGate(endpoint)).Select(Describe);

        tenantEndpoints.ShouldNotBeEmpty();
        unstated.ShouldBeEmpty();
    }

    // The rule finds a planted tenant endpoint that states neither, and one that states both.
    [Fact]
    public void GatePlan_AnEndpointWithNeitherOrBoth_IsFound()
    {
        RouteEndpoint[] planted =
        [
            Planted("neither"),
            Planted("both", new NotPlanGated(), new AuthorizeAttribute(Features.RolesCustom)),
            Planted("feature", new AuthorizeAttribute(Features.RolesCustom)),
            Planted("none", new NotPlanGated()),
        ];

        var unstated = planted.Where(endpoint => !StatesOneGate(endpoint)).Select(endpoint => endpoint.DisplayName);

        unstated.ShouldBe(["neither", "both"]);
    }

    // Exactly one of the two: the mark that it needs no feature, or a feature as a policy.
    private static bool StatesOneGate(RouteEndpoint endpoint) =>
        (endpoint.Metadata.GetMetadata<NotPlanGated>() is not null)
        ^ endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => FeatureList.Contains(data.Policy));

    private static RouteEndpoint Planted(string name, params object[] metadata) =>
        new(_ => Task.CompletedTask, RoutePatternFactory.Parse($"{TenantPath}{{tenantId}}/{name}"), 0, new EndpointMetadataCollection(metadata), name);

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join(",", endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])} {endpoint.RoutePattern.RawText}";
}
