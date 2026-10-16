using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ControlPlane.Api;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;

namespace Api.ArchitectureTests;

// N11: the committed OpenAPI document is the contract every client is generated from. Each operation has its endpoint's name as its
// id, each status in a response lists exactly the values of its own enum (N7), each path parameter is declared, and each error
// answer is the one Problem Details schema with its code. OpenApiDocumentFileTests checks that the committed file is the one the
// build writes.
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
        var endpoints = DocumentedEndpoints()
            .Select(endpoint => $"{MethodOf(endpoint)} {DocumentPathOf(endpoint)} {endpoint.Metadata.OfType<MethodInfo>().Single().Name}");

        var operations = Operations().Select(operation => $"{operation.Method} {operation.Path} {OperationIdOf(operation.Value)}");

        operations.ShouldNotBeEmpty();
        operations.ShouldBe(endpoints, ignoreOrder: true);
    }

    // N7: an endpoint is a named method. A lambda's method has a compiler-generated name, such as <Map>b__0_0, which is no name.
    [Fact]
    public void BuildOpenApiDocument_EveryOperationId_IsAPascalCaseName()
    {
        var ids = Operations().Select(operation => OperationIdOf(operation.Value));

        ids.ShouldNotBeEmpty();
        ids.Where(id => !PascalCaseName().IsMatch(id)).ShouldBeEmpty();
    }

    // N7: the Status of each response is its own enum, with only the values that response can carry.
    [Fact]
    public void BuildOpenApiDocument_StatusOfAResponse_ListsExactlyTheValuesOfItsEnum()
    {
        var expected = typeof(ControlPlaneModule).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal) && type.GetProperty("Status") is not null)
            .Select(type => $"{type.Name}: {JsonValuesOf(type.GetProperty("Status")!.PropertyType)}");

        var documented = Schemas()
            .Where(schema => schema.Name.EndsWith("Response", StringComparison.Ordinal))
            .Where(schema => schema.Value.GetProperty("properties").TryGetProperty("status", out _))
            .Select(schema => $"{schema.Name}: {EnumOf(Resolve(schema.Value.GetProperty("properties").GetProperty("status")))}");

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
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

    // OpenAPI 3.1, "Path Templating": each parameter in a path is a required path parameter. The tenant group, not the endpoint,
    // names {tenantId}, so the endpoint alone would leave it out. A {name:guid} parameter is a uuid, as on the admin routes.
    [Fact]
    public void BuildOpenApiDocument_ParameterInAPath_IsARequiredPathParameterWithItsFormat()
    {
        var inRoutes = DocumentedEndpoints()
            .SelectMany(endpoint => endpoint.RoutePattern.Parameters
                .Select(parameter => $"{MethodOf(endpoint)} {DocumentPathOf(endpoint)} {parameter.Name} {FormatOf(parameter)}"));

        var declared = Operations()
            .SelectMany(operation => DeclaredPathParametersOf(operation.Value).Select(parameter => $"{operation.Method} {operation.Path} {parameter}"));

        inRoutes.ShouldNotBeEmpty();
        declared.ShouldBe(inRoutes, ignoreOrder: true);
    }

    // The webhook group is left out of the document (N5), and so are the endpoints outside /v1, such as the document itself.
    private IEnumerable<RouteEndpoint> DocumentedEndpoints() =>
        _application.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/v1/", StringComparison.Ordinal))
            .Where(endpoint => endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is not { ExcludeFromDescription: true });

    private static string MethodOf(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods.Single().ToLowerInvariant();

    // The document leaves the route constraints out of a path: {tenantId:guid} is {tenantId}. A group's own endpoint, such as GET /v1/me,
    // keeps the group's trailing slash in its raw route; the document and the router leave it out.
    private static string DocumentPathOf(RouteEndpoint endpoint) => RouteConstraint().Replace(endpoint.RoutePattern.RawText!, "{$1}").TrimEnd('/');

    private static string FormatOf(RoutePatternParameterPart parameter) =>
        parameter.ParameterPolicies.Any(policy => policy.Content == "guid") ? "uuid" : "-";

    private static string OperationIdOf(JsonElement operation) =>
        operation.TryGetProperty("operationId", out var id) ? id.GetString()! : "<none>";

    private static string EnumOf(JsonElement schema) =>
        schema.TryGetProperty("enum", out var values) ? string.Join(", ", values.EnumerateArray().Select(value => value.GetString())) : "<no enum>";

    // The values an enum has in JSON: the name its member declares, else the member's own name.
    private static string JsonValuesOf(Type type) =>
        type.IsEnum
            ? string.Join(", ", type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(member => member.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? member.Name))
            : $"<{type.Name}, not an enum>";

    private static string ProblemSchemaOf(JsonElement response) =>
        response.TryGetProperty("content", out var content) && content.TryGetProperty("application/problem+json", out var problem)
            ? problem.GetProperty("schema").GetProperty("$ref").GetString()!
            : "<no application/problem+json>";

    private static IEnumerable<string> DeclaredPathParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray()
                .Where(parameter => parameter.GetProperty("in").GetString() == "path" && parameter.GetProperty("required").GetBoolean())
                .Select(parameter => $"{parameter.GetProperty("name").GetString()} {FormatOf(parameter.GetProperty("schema"))}")
            : [];

    private static string FormatOf(JsonElement schema) => schema.TryGetProperty("format", out var format) ? format.GetString()! : "-";

    // A schema the document names, such as an enum, is a reference into components/schemas.
    private JsonElement Resolve(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;

    private IEnumerable<(string Method, string Path, JsonElement Value)> Operations() =>
        _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (operation.Name, path.Name, operation.Value)));

    private JsonElement.ObjectEnumerator Schemas() => _document.GetProperty("components").GetProperty("schemas").EnumerateObject();

    [GeneratedRegex("^[A-Z][A-Za-z]+$")]
    private static partial Regex PascalCaseName();

    [GeneratedRegex(@"\{(\w+):[^}]+\}")]
    private static partial Regex RouteConstraint();
}
