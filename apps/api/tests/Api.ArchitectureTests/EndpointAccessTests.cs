using System.Reflection;
using Api.Authorization;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SharedKernel;

namespace Api.ArchitectureTests;

// A5: every endpoint of the composed application carries exactly one access state, public, signed-in only, a permission or a checked
// signature. Every endpoint on the tenant path requires a permission. Every endpoint on the webhooks path has no user and declares
// that it checks its sender's signature, and only those endpoints do.
public sealed class EndpointAccessTests : IAsyncLifetime
{
    private const string Public = "public";
    private const string SignedIn = "signed-in";
    private const string Permission = "permission";
    private const string Signature = "signature";

    private const string TenantPath = "/v1/tenants/";
    private const string WebhooksPath = "/v1/webhooks/";

    // Every endpoint anyone may call without signing in. A new one is added here on purpose. The health checks are not endpoints:
    // they answer on the management port alone, before routing (section 8).
    private static readonly string[] PublicEndpoints =
    [
        "GET /openapi/{documentName}.json",
        "GET /scalar/{documentName?}",
        "GET /scalar/scalar.js",
        "GET /scalar/scalar.aspnetcore.js",
        "GET /scalar/favicon.svg",
    ];

    // The permission catalogue: an endpoint asks for one of these by name.
    private static readonly string[] PermissionCatalogue =
    [
        .. typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
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
    public void StateAccess_EveryEndpoint_HasExactlyOneState()
    {
        var withoutOneState = _endpoints
            .Where(endpoint => StatesOf(endpoint).Length != 1)
            .Select(endpoint => $"{Describe(endpoint)}: [{string.Join(", ", StatesOf(endpoint))}]");

        withoutOneState.ShouldBeEmpty();
    }

    [Fact]
    public void StateAccess_EndpointOnTheTenantPath_RequiresAPermission()
    {
        var tenantEndpoints = _endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(TenantPath, StringComparison.Ordinal));

        var withoutPermission = tenantEndpoints
            .Where(endpoint => !StatesOf(endpoint).SequenceEqual([Permission]))
            .Select(Describe);

        tenantEndpoints.ShouldNotBeEmpty();
        withoutPermission.ShouldBeEmpty();
    }

    [Fact]
    public void StateAccess_EndpointOnTheWebhooksPath_ChecksASignatureWithoutAUser()
    {
        var webhookEndpoints = _endpoints.Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(WebhooksPath, StringComparison.Ordinal));

        webhookEndpoints.ShouldNotBeEmpty();
        WithoutASignatureCheck(webhookEndpoints).ShouldBeEmpty();
    }

    // The check above fails for a webhook endpoint that does not declare a signature check, such as one the group alone opens.
    [Fact]
    public void StateAccess_WebhookEndpointWithoutASignatureCheck_IsFound()
    {
        var opened = new RouteEndpointBuilder(_ => Task.CompletedTask, RoutePatternFactory.Parse($"{WebhooksPath}other"), 0)
        {
            Metadata = { new AllowAnonymousAttribute(), new HttpMethodMetadata(["POST"]) },
        }.Build();

        WithoutASignatureCheck([(RouteEndpoint)opened]).ShouldBe([$"POST {WebhooksPath}other"]);
    }

    [Fact]
    public void StateAccess_EndpointThatChecksASignature_IsOnTheWebhooksPath()
    {
        var elsewhere = _endpoints
            .Where(endpoint => StatesOf(endpoint).Contains(Signature) && !endpoint.RoutePattern.RawText!.StartsWith(WebhooksPath, StringComparison.Ordinal))
            .Select(Describe);

        elsewhere.ShouldBeEmpty();
    }

    [Fact]
    public void StateAccess_PublicEndpoint_IsOnTheList()
    {
        var publicEndpoints = _endpoints.Where(endpoint => StatesOf(endpoint).Contains(Public)).Select(Describe);

        publicEndpoints.ShouldBe(PublicEndpoints, ignoreOrder: true);
    }

    // A webhook endpoint states a checked signature, and needs no user for it: it is open to anyone who can sign.
    private static IEnumerable<string> WithoutASignatureCheck(IEnumerable<RouteEndpoint> endpoints) =>
        endpoints
            .Where(endpoint => !StatesOf(endpoint).SequenceEqual([Signature]) || endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null)
            .Select(Describe);

    private static string[] StatesOf(RouteEndpoint endpoint) =>
    [
        .. endpoint.Metadata.GetMetadata<IAllowAnonymous>() is null || endpoint.Metadata.GetMetadata<SignatureChecked>() is not null
            ? Array.Empty<string>()
            : [Public],
        .. endpoint.Metadata.GetMetadata<SignatureChecked>() is null ? Array.Empty<string>() : [Signature],
        .. endpoint.Metadata.GetMetadata<SignedInEndpoint>() is null ? Array.Empty<string>() : [SignedIn],
        .. endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Any(data => PermissionCatalogue.Contains(data.Policy))
            ? [Permission]
            : Array.Empty<string>(),
    ];

    private static string Describe(RouteEndpoint endpoint) =>
        $"{string.Join(",", endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["*"])} {endpoint.RoutePattern.RawText}";

    // The application as Program composes it, in development, where the API documents are mapped too, in the role that serves the
    // whole API. The host's own start-up checks need a database and its settings; the endpoints do not.
    private sealed class ComposedApplication : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(Environments.Development);
            builder.UseSetting("Host:Role", "all");
            builder.ConfigureTestServices(services =>
            {
                services
                    .Where(service => service.ServiceType == typeof(IHostedService) && service.ImplementationType?.Assembly == typeof(Program).Assembly)
                    .ToList()
                    .ForEach(check => services.Remove(check));
                services.RemoveAll<IStartupValidator>();
            });
        }
    }
}
