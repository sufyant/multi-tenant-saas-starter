using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using ControlPlane.Api;
using ControlPlane.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.ArchitectureTests;

// N11: the committed OpenAPI document is the contract every client is generated from. Each operation has its endpoint's name as its
// id, each status in a response lists exactly the values of its own enum (N7), each path parameter is declared, and each error
// answer is the one Problem Details schema, whose code is the one enum of error codes (K5). OpenApiDocumentFileTests checks that the committed file is the one the
// build writes.
public sealed partial class OpenApiContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";
    private const string ProblemDetailsSchema = "#/components/schemas/ProblemDetails";
    private const string LanguageSchema = "#/components/schemas/Language";
    private const string PermissionSchema = "#/components/schemas/Permission";
    private const string InvitationStateSchema = "#/components/schemas/InvitationResponseState";

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

    // Step 20k: a built-in role is one lower-case word, as every other enum of the document.
    [Fact]
    public void BuildOpenApiDocument_MemberRole_ListsTheLowerCaseRoles()
    {
        var documented = EnumOf(Resolve(Schemas().Single(found => found.Name == "MemberRole").Value));

        documented.ShouldBe("owner, admin, member");
    }

    // Decision 1 of step 20j: one list of languages, the schema Language, with the tags of the enum. Every field that holds a language
    // refers to it, so a generated client has one type for it, and no schema copies the list.
    [Fact]
    public void BuildOpenApiDocument_EveryFieldThatHoldsALanguage_IsTheOneLanguageEnum()
    {
        var expected = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .SelectMany(type => type.GetProperties().Where(HoldsALanguage).Select(property => $"{type.Name}.{JsonNamingPolicy.CamelCase.ConvertName(property.Name)}"));

        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Contains(LanguageSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));
        var language = Schemas().Single(schema => schema.Name == "Language").Value;
        var copies = Schemas().Where(schema => schema.Name != "Language" && EnumOf(schema.Value) == EnumOf(language)).Select(schema => schema.Name);

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
        EnumOf(language).ShouldBe(JsonValuesOf(typeof(Language)));
        copies.ShouldBeEmpty();
    }

    // N11 and K5: the permission catalogue is one enum in the document, so a generated client knows each name and a typo does not compile.
    [Fact]
    public void BuildOpenApiDocument_Permission_ListsEveryPermissionOfTheCatalogue()
    {
        var catalogue = typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var documented = EnumOf(Schemas().Single(schema => schema.Name == "Permission").Value);

        catalogue.ShouldNotBeEmpty();
        documented.ShouldBe(string.Join(", ", catalogue));
    }

    // N11 and K5: every response field that holds permission names is an array of the one schema Permission, so the web compares each
    // name with the enum. A field is found by its name, as the response types keep the names as strings (MyTenantDetailResponse).
    [Fact]
    public void BuildOpenApiDocument_EveryResponseFieldNamedPermissions_IsAnArrayOfPermission()
    {
        var expected = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal) && type.GetProperty("Permissions") is not null)
            .Select(type => $"{type.Name}.permissions");

        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => IsAnArrayOf(property.Value, PermissionSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // The maintainer's decision in audit fix 08: one enum of an invitation's state, InvitationResponseState. The preview, the Owner
    // invitation of a tenant and the invitation of the tenant's list refer to it, so a generated client has one type for it, and no other
    // schema lists the states of an invitation.
    [Fact]
    public void BuildOpenApiDocument_EveryStateOfAnInvitation_IsTheOneInvitationStateEnum()
    {
        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Contains(InvitationStateSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));
        var stateEnums = Schemas().Where(schema => schema.Name.EndsWith("State", StringComparison.Ordinal)).Select(schema => schema.Name);

        documented.ShouldBe(["InvitationPreviewResponse.state", "InvitationResponse.state", "OwnerInvitationResponse.state"], ignoreOrder: true);
        stateEnums.ShouldBe(["InvitationResponseState"]);
        EnumOf(Resolve(Schemas().Single(schema => schema.Name == "InvitationResponseState").Value)).ShouldBe("pending, expired, accepted, cancelled");
    }

    [Fact]
    public void BuildOpenApiDocument_EveryErrorResponse_IsTheProblemDetailsSchema()
    {
        var errors = Operations()
            .SelectMany(operation => operation.Value.GetProperty("responses").EnumerateObject()
                .Where(response => response.Name == "default" || int.Parse(response.Name, CultureInfo.InvariantCulture) >= 400)
                .Select(response => (Name: $"{operation.Method} {operation.Path} {response.Name}", Schema: ProblemSchemaOf(response.Value))));

        errors.ShouldNotBeEmpty();
        errors.Where(error => error.Schema != ProblemDetailsSchema).Select(error => $"{error.Name}: {error.Schema}").ShouldBeEmpty();
    }

    // K5: the codes are one enum in the document, every code an owner declares, so a generated client knows each code an error can carry.
    // Every error answer has one, so the member is required.
    [Fact]
    public void BuildOpenApiDocument_CodeOfProblemDetails_IsTheRequiredErrorCodeEnum()
    {
        var problem = Schemas().Single(schema => schema.Name == "ProblemDetails").Value;
        var code = problem.GetProperty("properties").GetProperty("code");

        problem.GetProperty("required").EnumerateArray().Select(member => member.GetString()).ShouldContain("code");

        code.TryGetProperty("$ref", out var reference).ShouldBeTrue();
        reference.GetString().ShouldBe("#/components/schemas/ErrorCode");
        EnumOf(Resolve(code)).ShouldBe(string.Join(", ", ErrorCodeDeclarations.All.Select(declared => declared.Value).Distinct().Order(StringComparer.Ordinal)));
    }

    // A validation answer lists codes for each field, never messages, and the field codes are an enum too.
    [Fact]
    public void BuildOpenApiDocument_FieldsOfAValidationProblem_AreTheFieldErrorCodeEnum()
    {
        var errors = Schemas().Single(schema => schema.Name == "ProblemDetails").Value.GetProperty("properties").GetProperty("errors");

        var codes = errors.GetProperty("additionalProperties").GetProperty("items");

        codes.TryGetProperty("$ref", out var reference).ShouldBeTrue();
        reference.GetString().ShouldBe("#/components/schemas/FieldErrorCode");
        EnumOf(Resolve(codes)).ShouldBe(JsonValuesOf(typeof(FieldErrorCode)));
    }

    // K5: every code is an area and a reason, in lower snake case: tenant.not_found.
    [Fact]
    public void DeclareErrorCodes_EveryCode_IsAnAreaAndAReason()
    {
        var codes = ErrorCodeDeclarations.All.Select(declared => declared.Value);

        codes.ShouldNotBeEmpty();
        codes.Where(code => !AreaAndReason().IsMatch(code)).ShouldBeEmpty();
    }

    // K5: every /v1 route can answer 401 and 429, so every operation declares both.
    [Fact]
    public void BuildOpenApiDocument_EveryOperation_DeclaresUnauthorizedAndTooManyRequests()
    {
        string[] statuses = ["401", "429"];

        var missing = Operations()
            .SelectMany(operation => statuses
                .Where(status => !operation.Value.GetProperty("responses").TryGetProperty(status, out _))
                .Select(status => $"{operation.Method} {operation.Path} {status}"));

        Operations().ShouldNotBeEmpty();
        missing.ShouldBeEmpty();
    }

    // K5 and I1: an endpoint that takes Idempotency-Key creates a resource, so it answers 201.
    [Fact]
    public void BuildOpenApiDocument_OperationThatTakesAnIdempotencyKey_DeclaresCreated()
    {
        var creating = Operations().Where(operation => HeaderParametersOf(operation.Value).Contains("Idempotency-Key")).ToList();

        var without = creating
            .Where(operation => !operation.Value.GetProperty("responses").TryGetProperty("201", out _))
            .Select(operation => $"{operation.Method} {operation.Path}");

        creating.ShouldNotBeEmpty();
        without.ShouldBeEmpty();
    }

    // A field that a response always carries is required, so a generated client never checks for it. Only a field that may be null may be
    // left out. This covers every schema an answer refers to, the error answers too.
    [Fact]
    public void BuildOpenApiDocument_EveryPropertyOfAResponseSchema_IsRequiredUnlessNullable()
    {
        var optional = ResponseSchemas()
            .SelectMany(schema => PropertiesOf(schema.Value)
                .Where(property => !RequiredOf(schema.Value).Contains(property.Name) && !IsNullable(property.Value))
                .Select(property => $"{schema.Name}.{property.Name}"));

        ResponseSchemas().ShouldNotBeEmpty();
        optional.ShouldBeEmpty();
    }

    // The API reads and writes a number as a JSON number only (JsonNumberHandling.Strict), so no schema says it may be a string.
    [Fact]
    public void BuildOpenApiDocument_NoSchema_IsAnIntegerAndStringUnion()
    {
        var integers = ObjectsIn(_document, "#").Where(found => TypesOf(found.Value).Contains("integer")).ToList();

        var unions = integers.Where(found => TypesOf(found.Value).Contains("string")).Select(found => found.Path);

        integers.ShouldNotBeEmpty();
        unions.ShouldBeEmpty();
    }

    // Every /v1 route needs a bearer token (A5), so the document declares the scheme once and requires it for every operation. The
    // anonymous routes, the document and the API reference, are outside /v1 and so outside the document.
    [Fact]
    public void BuildOpenApiDocument_EveryOperation_RequiresTheBearerScheme()
    {
        var anonymous = DocumentedEndpoints()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(endpoint => $"{MethodOf(endpoint)} {DocumentPathOf(endpoint)}");

        var overriding = Operations().Where(operation => operation.Value.TryGetProperty("security", out _)).Select(operation => $"{operation.Method} {operation.Path}");

        SecuritySchemeOf("bearer").ShouldBe("http bearer");
        SecurityRequirementsOf(_document).ShouldBe("bearer");
        anonymous.ShouldBeEmpty();
        overriding.ShouldBeEmpty();
    }

    // An operation can answer statuses it does not list, such as 405, 408, 413, 415 or 500. Each is Problem Details too (K5).
    [Fact]
    public void BuildOpenApiDocument_EveryOperation_DeclaresProblemDetailsAsTheDefaultAnswer()
    {
        var without = Operations()
            .Where(operation => DefaultSchemaOf(operation.Value) != ProblemDetailsSchema)
            .Select(operation => $"{operation.Method} {operation.Path}: {DefaultSchemaOf(operation.Value)}");

        Operations().ShouldNotBeEmpty();
        without.ShouldBeEmpty();
    }

    // I5: a replayed answer carries Idempotent-Replayed. A replay gives a saved result: the success, or a business rejection. A request
    // that failed validation (400) or authentication (401) saves nothing, and neither does a server failure.
    [Fact]
    public void BuildOpenApiDocument_OperationThatTakesAnIdempotencyKey_DeclaresIdempotentReplayedOnEveryAnswerItReplays()
    {
        string[] neverReplayed = ["400", "401", "default"];
        var creating = Operations().Where(operation => HeaderParametersOf(operation.Value).Contains("Idempotency-Key")).ToList();

        var without = creating
            .SelectMany(operation => operation.Value.GetProperty("responses").EnumerateObject()
                .Where(response => !neverReplayed.Contains(response.Name) && !ResponseHeadersOf(response.Value).Contains("Idempotent-Replayed"))
                .Select(response => $"{operation.Method} {operation.Path} {response.Name}"));

        creating.ShouldNotBeEmpty();
        without.ShouldBeEmpty();
    }

    // RFC 9110, section 15.3.2: a 201 names the new resource in Location where a GET reads it. CreateTenant has GetTenant. An invitation
    // has no GET of its own, so its 201 sends no Location and its description says so.
    [Fact]
    public void BuildOpenApiDocument_CreatedAnswer_DeclaresLocationOnlyWhereAGetReadsTheResource()
    {
        var created = Operations().Where(operation => operation.Value.GetProperty("responses").TryGetProperty("201", out _)).ToList();

        var located = created.Where(operation => ResponseHeadersOf(operation.Value.GetProperty("responses").GetProperty("201")).Contains("Location"));
        var unexplained = created.Except(located).Where(operation => !DescriptionOf(operation.Value).Contains("Location", StringComparison.Ordinal));

        located.Select(operation => OperationIdOf(operation.Value)).ShouldBe(["CreateTenant"]);
        unexplained.Select(operation => OperationIdOf(operation.Value)).ShouldBeEmpty();
    }

    // OpenAPI 3.1, "Path Templating": each parameter in a path is a required path parameter. The tenant group, not the endpoint,
    // names {tenantId}, so the endpoint alone would leave it out. A {name:guid} parameter is a uuid, as on the platform routes.
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
        type.IsEnum ? string.Join(", ", JsonNamesOf(type)) : $"<{type.Name}, not an enum>";

    private static IEnumerable<string> JsonNamesOf(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(member => member.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name ?? member.Name);

    private static IEnumerable<string> HeaderParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray().Where(parameter => parameter.GetProperty("in").GetString() == "header").Select(parameter => parameter.GetProperty("name").GetString()!)
            : [];

    private static IEnumerable<string> ResponseHeadersOf(JsonElement response) =>
        response.TryGetProperty("headers", out var headers) ? headers.EnumerateObject().Select(header => header.Name) : [];

    private static string DescriptionOf(JsonElement operation) =>
        operation.TryGetProperty("description", out var description) ? description.GetString()! : string.Empty;

    private static string DefaultSchemaOf(JsonElement operation) =>
        operation.GetProperty("responses").TryGetProperty("default", out var answer) ? ProblemSchemaOf(answer) : "<no default>";

    private string SecuritySchemeOf(string name) =>
        _document.GetProperty("components").TryGetProperty("securitySchemes", out var schemes) && schemes.TryGetProperty(name, out var scheme)
            ? $"{scheme.GetProperty("type").GetString()} {scheme.GetProperty("scheme").GetString()}"
            : "<no scheme>";

    private static string SecurityRequirementsOf(JsonElement element) =>
        element.TryGetProperty("security", out var requirements)
            ? string.Join(", ", requirements.EnumerateArray().SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name)))
            : "<no security>";

    private static List<JsonProperty> PropertiesOf(JsonElement schema) =>
        schema.TryGetProperty("properties", out var properties) ? [.. properties.EnumerateObject()] : [];

    private static HashSet<string> RequiredOf(JsonElement schema) =>
        schema.TryGetProperty("required", out var required) ? [.. required.EnumerateArray().Select(name => name.GetString()!)] : [];

    // A schema may be null when its type lists null, or when one of its options is null, as a nullable reference to an enum is.
    private static bool IsNullable(JsonElement schema) =>
        TypesOf(schema).Contains("null")
        || (schema.TryGetProperty("oneOf", out var options) && options.EnumerateArray().Any(option => TypesOf(option).Contains("null")));

    // A member named type is the schema's type only when it is a name or a list of names; in properties it may be a property's schema.
    private static IEnumerable<string> TypesOf(JsonElement schema) =>
        !schema.TryGetProperty("type", out var type) ? [] : type.ValueKind switch
        {
            JsonValueKind.Array => type.EnumerateArray().Select(name => name.GetString()!),
            JsonValueKind.String => [type.GetString()!],
            _ => [],
        };

    // Every object in the document, by its JSON pointer.
    private static IEnumerable<(string Path, JsonElement Value)> ObjectsIn(JsonElement element, string path) => element.ValueKind switch
    {
        JsonValueKind.Object => element.EnumerateObject()
            .SelectMany(member => ObjectsIn(member.Value, $"{path}/{member.Name}"))
            .Prepend((path, element)),
        JsonValueKind.Array => element.EnumerateArray().SelectMany((item, index) => ObjectsIn(item, $"{path}/{index}")),
        _ => [],
    };

    // The schemas an answer refers to, and the schemas they refer to in turn.
    private List<(string Name, JsonElement Value)> ResponseSchemas()
    {
        var pending = new Queue<string>(Operations().SelectMany(operation => ReferencesIn(operation.Value.GetProperty("responses"))));
        var found = new SortedSet<string>(StringComparer.Ordinal);
        while (pending.TryDequeue(out var reference))
        {
            if (found.Add(reference))
            {
                foreach (var next in ReferencesIn(SchemaAt(reference)))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return [.. found.Select(reference => (reference.Split('/')[^1], SchemaAt(reference)))];
    }

    private JsonElement SchemaAt(string reference) =>
        _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.Split('/')[^1]);

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

    private static bool HoldsALanguage(PropertyInfo property) =>
        (Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType) == typeof(Language);

    // Every schema a schema refers to, inside a oneOf of a nullable value too.
    private static IEnumerable<string> ReferencesIn(JsonElement schema) => schema.ValueKind switch
    {
        JsonValueKind.Object => schema.EnumerateObject()
            .SelectMany(member => member.Name == "$ref" ? [member.Value.GetString()!] : ReferencesIn(member.Value)),
        JsonValueKind.Array => schema.EnumerateArray().SelectMany(ReferencesIn),
        _ => [],
    };

    private static string FormatOf(JsonElement schema) => schema.TryGetProperty("format", out var format) ? format.GetString()! : "-";

    private static bool IsAnArrayOf(JsonElement schema, string items) =>
        schema.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "array"
        && schema.TryGetProperty("items", out var item) && item.TryGetProperty("$ref", out var reference) && reference.GetString() == items;

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

    [GeneratedRegex("^[a-z]+(_[a-z]+)*\\.[a-z]+(_[a-z]+)*$")]
    private static partial Regex AreaAndReason();

    [GeneratedRegex(@"\{(\w+):[^}]+\}")]
    private static partial Regex RouteConstraint();
}
