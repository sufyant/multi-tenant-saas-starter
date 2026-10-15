using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ControlPlane.Api;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Api.ArchitectureTests;

// N11: the committed OpenAPI document is the contract every client is generated from. Each operation has its endpoint's name as its
// id, each status in a response lists its values, and each error answer is the one Problem Details schema with its code.
// OpenApiDocumentFileTests checks that the committed file is the one the build writes.
public sealed partial class OpenApiContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";
    private const string ProblemDetailsSchema = "#/components/schemas/ProblemDetails";

    private readonly ComposedApplication _application = new();
    private JsonElement _document;

    public ValueTask InitializeAsync()
    {
        _document = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, DocumentFile))).RootElement;
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _application.DisposeAsync();

    [Fact]
    public void BuildOpenApiDocument_EveryOperation_HasItsEndpointNameAsId()
    {
        var endpoints = _application.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(InTheDocument)
            .Select(endpoint => $"{MethodOf(endpoint)} {DocumentPathOf(endpoint)} {endpoint.Metadata.OfType<MethodInfo>().Single().Name}");

        var operations = Operations().Select(operation => $"{operation.Method} {operation.Path} {OperationIdOf(operation.Value)}");

        operations.ShouldNotBeEmpty();
        operations.ShouldBe(endpoints, ignoreOrder: true);
    }

    [Fact]
    public void BuildOpenApiDocument_StatusOfAResponse_ListsTheResponseStatusValues()
    {
        var statuses = Schemas()
            .Where(schema => schema.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject())
            .Where(property => property.Name == "status")
            .Select(property => $"{EnumOf(property.Value)}");

        statuses.ShouldNotBeEmpty();
        statuses.ShouldAllBe(values => values == string.Join(", ", ControlPlaneModule.ResponseStatuses));
    }

    [Fact]
    public void BuildOpenApiDocument_EveryErrorResponse_IsTheProblemDetailsSchema()
    {
        var errors = Operations()
            .SelectMany(operation => operation.Value.GetProperty("responses").EnumerateObject()
                .Where(response => int.Parse(response.Name, CultureInfo.InvariantCulture) >= 400)
                .Select(response => (Name: $"{operation.Method} {operation.Path} {response.Name}", Schema: ProblemSchemaOf(response.Value))));

        errors.ShouldNotBeEmpty();
        errors.Where(error => error.Schema != ProblemDetailsSchema).Select(error => $"{error.Name}: {error.Schema}").ShouldBeEmpty();
    }

    [Fact]
    public void BuildOpenApiDocument_ProblemDetailsSchema_DeclaresTheErrorCode()
    {
        var properties = Schemas().Single(schema => schema.Name == "ProblemDetails").Value.GetProperty("properties");

        properties.GetProperty("code").GetProperty("type").GetString().ShouldBe("string");
    }

    // The webhook group is left out of the document (N5), and so are the endpoints outside /v1, such as the document itself.
    private static bool InTheDocument(RouteEndpoint endpoint) =>
        endpoint.RoutePattern.RawText!.StartsWith("/v1/", StringComparison.Ordinal)
        && endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is not { ExcludeFromDescription: true };

    private static string MethodOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Single().ToLowerInvariant();

    // The document leaves the route constraints out of a path: {tenantId:guid} is {tenantId}.
    private static string DocumentPathOf(RouteEndpoint endpoint) => RouteConstraint().Replace(endpoint.RoutePattern.RawText!, "{$1}");

    private static string OperationIdOf(JsonElement operation) =>
        operation.TryGetProperty("operationId", out var id) ? id.GetString()! : "<none>";

    private static string EnumOf(JsonElement property) =>
        property.TryGetProperty("enum", out var values) ? string.Join(", ", values.EnumerateArray().Select(value => value.GetString())) : "<none>";

    private static string ProblemSchemaOf(JsonElement response) =>
        response.TryGetProperty("content", out var content) && content.TryGetProperty("application/problem+json", out var problem)
            ? problem.GetProperty("schema").GetProperty("$ref").GetString()!
            : "<no application/problem+json>";

    private IEnumerable<(string Method, string Path, JsonElement Value)> Operations() =>
        _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (operation.Name, path.Name, operation.Value)));

    private JsonElement.ObjectEnumerator Schemas() => _document.GetProperty("components").GetProperty("schemas").EnumerateObject();

    [GeneratedRegex(@"\{(\w+):[^}]+\}")]
    private static partial Regex RouteConstraint();
}
