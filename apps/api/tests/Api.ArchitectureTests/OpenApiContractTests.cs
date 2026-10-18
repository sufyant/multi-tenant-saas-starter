using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Audit.Domain;
using ControlPlane.Api;
using ControlPlane.Api.Tenant;
using ControlPlane.Contracts;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;

namespace Api.ArchitectureTests;

// N11: the committed OpenAPI document is the contract every client is generated from. Each operation has its endpoint's name as its
// id, each enum in a response lists exactly the values of its own enum (N7), each path parameter is declared, and each error
// answer is the one Problem Details schema, whose code is the one enum of error codes (K5). OpenApiDocumentFileTests checks that the committed file is the one the
// build writes.
public sealed partial class OpenApiContractTests : IAsyncLifetime
{
    private const string DocumentFile = "openapi/v1.json";
    private const string ProblemDetailsSchema = "#/components/schemas/ProblemDetails";
    private const string LanguageSchema = "#/components/schemas/Language";
    private const string PermissionSchema = "#/components/schemas/Permission";
    private const string AuditOperationSchema = "#/components/schemas/AuditOperation";
    private const string InvitationStateSchema = "#/components/schemas/InvitationResponseState";
    private const string RoleSchema = "#/components/schemas/RoleResponse";
    private const string MemberRoleSchema = "#/components/schemas/MemberRole";

    // The enums the host adds to fields that are strings in C# (N11): the permissions and the audit operations.
    private static readonly string[] EnumsOfStrings = [PermissionSchema, AuditOperationSchema];

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

    // N7: every enum of a response lists exactly the values of its C# enum: a status, a state, a kind, a role or a language (A18). The
    // host adds two enums to fields that stay strings in C#, so the C# types cannot show them: their own tests read them.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumOfAResponse_ListsExactlyTheValuesOfItsEnum()
    {
        var expected = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal))
            .SelectMany(type => type.GetProperties()
                .Where(property => EnumTypeOf(property.PropertyType) is not null)
                .Select(property => $"{type.Name}.{JsonNamingPolicy.CamelCase.ConvertName(property.Name)}: {JsonValuesOf(EnumTypeOf(property.PropertyType)!)}"));

        var documented = Schemas()
            .Where(schema => schema.Name.EndsWith("Response", StringComparison.Ordinal) && schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .SelectMany(property => ReferencesIn(property.Value)
                    .Where(reference => !EnumsOfStrings.Contains(reference))
                    .Select(SchemaAt)
                    .Where(found => found.TryGetProperty("enum", out _))
                    .Select(found => $"{schema.Name}.{property.Name}: {EnumOf(found)}")));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // A19: an enum is a string on the wire, so each enum schema says so, and a generated client reads it as text.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumSchema_HasTheTypeString()
    {
        var enums = Schemas().Where(schema => schema.Value.TryGetProperty("enum", out _));

        var untyped = enums.Where(schema => !schema.Value.TryGetProperty("type", out var type) || type.GetString() != "string").Select(schema => schema.Name);

        enums.ShouldNotBeEmpty();
        untyped.ShouldBeEmpty();
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

    // N11 and K5: the permission list is one enum in the document, so a generated client knows each name and a typo does not compile.
    [Fact]
    public void BuildOpenApiDocument_Permission_ListsEveryPermissionOfThePermissionList()
    {
        var listed = typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var documented = EnumOf(Schemas().Single(schema => schema.Name == "Permission").Value);

        listed.ShouldNotBeEmpty();
        documented.ShouldBe(string.Join(", ", listed));
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

    // N7 and K5, the maintainer's decision in audit fix 04: every response field that holds a role is the one schema RoleResponse,
    // { builtIn, name }. builtIn is the enum MemberRole, or null for a custom role later, so a client compares it with an enum value
    // and never with a string it writes by hand. A field is found by its name, as the permissions are.
    [Fact]
    public void BuildOpenApiDocument_EveryResponseFieldNamedRole_RefersToTheOneRoleSchema()
    {
        var expected = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal) && type.GetProperty("Role") is not null)
            .Select(type => $"{type.Name}.role {RoleSchema}");

        var documented = ResponseSchemas()
            .SelectMany(schema => PropertiesOf(schema.Value)
                .Where(property => property.Name == "role")
                .Select(property => $"{schema.Name}.role {string.Join(", ", ReferencesIn(property.Value))}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
        var role = SchemaAt(RoleSchema);
        PropertiesOf(role).Select(property => property.Name).ShouldBe(["builtIn", "name"], ignoreOrder: true);
        ReferencesIn(role.GetProperty("properties").GetProperty("builtIn")).ShouldBe([MemberRoleSchema]);
        IsNullable(role.GetProperty("properties").GetProperty("builtIn")).ShouldBeTrue();
        EnumOf(SchemaAt(MemberRoleSchema)).ShouldBe(JsonValuesOf(typeof(MemberRole)));
    }

    // N11 and K3: the operations of the audit entries are one enum in the document, so a client knows each one and hears of a new one.
    [Fact]
    public void BuildOpenApiDocument_AuditOperation_ListsEveryOperationOfAuditOperations()
    {
        var declared = typeof(AuditOperations).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal);

        var documented = EnumOf(Schemas().Single(schema => schema.Name == "AuditOperation").Value);

        declared.ShouldNotBeEmpty();
        documented.ShouldBe(string.Join(", ", declared));
    }

    // N11 and K3: the operation of an entry of either audit log is the one schema AuditOperation. A field is found by its name, as the
    // response types keep the operation as a string (AuditEntryResponse).
    [Fact]
    public void BuildOpenApiDocument_EveryResponseFieldNamedOperation_IsAnAuditOperation()
    {
        var expected = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal) && type.GetProperty("Operation") is not null)
            .Select(type => $"{type.Name}.operation");

        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Contains(AuditOperationSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // The maintainer's decision in audit fix 08: one enum of an invitation's state, InvitationResponseState. The preview, the Owner
    // invitation of a tenant and the invitation of the tenant's list refer to it, so a generated client has one type for it. No other
    // schema is an enum of the invitation's states, or of a part of them; an enum of another thing's states is not a copy.
    [Fact]
    public void BuildOpenApiDocument_EveryStateOfAnInvitation_IsTheOneInvitationStateEnum()
    {
        string[] invitationStates = ["pending", "expired", "accepted", "cancelled"];
        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Contains(InvitationStateSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));
        var copies = Schemas()
            .Where(schema => schema.Name != "InvitationResponseState" && schema.Value.TryGetProperty("enum", out _))
            .Where(schema => schema.Value.GetProperty("enum").EnumerateArray().All(value => invitationStates.Contains(value.GetString())))
            .Select(schema => schema.Name);

        documented.ShouldBe(["InvitationPreviewResponse.state", "InvitationResponse.state", "OwnerInvitationResponse.state"], ignoreOrder: true);
        EnumOf(Resolve(Schemas().Single(schema => schema.Name == "InvitationResponseState").Value)).ShouldBe(string.Join(", ", invitationStates));
        copies.ShouldBeEmpty();
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

    // The maintainer's decision 1 in audit fix 09: the validators are the one source of a request's limits, so a client checks a form
    // with the numbers the API enforces. Every request field with a length rule has the rule's minLength and maxLength, and no other
    // request field has either.
    [Fact]
    public void BuildOpenApiDocument_RequestFieldWithALengthRule_HasTheLengthsOfTheRule()
    {
        var expected = RequestRules()
            .Where(rule => rule.Rule is ILengthValidator { Max: >= 0 })
            .Select(rule => $"{rule.Field} {((ILengthValidator)rule.Rule).Min} {((ILengthValidator)rule.Rule).Max}");

        var documented = RequestFields()
            .Where(field => field.Value.TryGetProperty("maxLength", out _) || field.Value.TryGetProperty("minLength", out _))
            .Select(field => $"{field.Name} {LengthOf(field.Value, "minLength")} {LengthOf(field.Value, "maxLength")}");

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // Decision 1 of audit fix 09: an email rule is format email, and no other request field has the format.
    [Fact]
    public void BuildOpenApiDocument_RequestFieldWithAnEmailRule_HasFormatEmail()
    {
        var expected = RequestRules().Where(rule => rule.Rule is IEmailValidator).Select(rule => rule.Field);

        var documented = RequestFields().Where(field => FormatOf(field.Value) == "email").Select(field => field.Name);

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // The maintainer's decision 3 after audit fix 09: a regex rule is the field's pattern, where .NET and ECMAScript read the regex the
    // same way. The time zone's regex ends with \z, which ECMAScript does not know, and the web picks a time zone from a list, so its
    // field has no pattern.
    [Fact]
    public void BuildOpenApiDocument_RequestFieldWithARegexRule_HasThePatternOfTheRule()
    {
        string[] withoutPattern = ["ChangePreferencesRequest.timeZone"];
        var expected = RequestRules()
            .Where(rule => rule.Rule is IRegularExpressionValidator && !withoutPattern.Contains(rule.Field))
            .Select(rule => $"{rule.Field} {((IRegularExpressionValidator)rule.Rule).Expression}");

        var documented = RequestFields()
            .Where(field => field.Value.TryGetProperty("pattern", out _))
            .Select(field => $"{field.Name} {field.Value.GetProperty("pattern").GetString()}");

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // The maintainer's decision after audit fix 09: every length limit of a request is a length rule, so the document reads its number.
    // A limit that counts the trimmed value is a length rule too (TrimmedLength), never a predicate.
    [Fact]
    public void BuildOpenApiDocument_RuleWithTheTooLongCode_IsALengthRule()
    {
        var limits = RequestRules().Where(rule => rule.ErrorCode == "TooLong").ToList();

        var predicates = limits.Where(rule => rule.Rule is not ILengthValidator).Select(rule => $"{rule.Field} {rule.Rule.Name}");

        limits.ShouldNotBeEmpty();
        predicates.ShouldBeEmpty();
    }

    // Decision 1 of audit fix 09 and N8: the document finds a command's validator by the request that carries the command, XRequest for
    // XCommand. A validator whose request does not exist describes nothing in the document.
    [Fact]
    public void MapRequestToValidator_EveryCommandValidator_FindsItsRequest()
    {
        var requests = Solution.LayerOfEveryModule("Api")
            .SelectMany(project => Solution.Load(project).GetExportedTypes())
            .Select(type => type.Name)
            .Where(name => name.EndsWith("Request", StringComparison.Ordinal))
            .ToHashSet();
        var commands = Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Application").GetExportedTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Select(type => type.BaseType!.GetGenericArguments()[0])
            .ToList();

        var withoutRequest = commands.Select(RequestOf).Where(request => !requests.Contains(request));

        commands.ShouldNotBeEmpty();
        withoutRequest.ShouldBeEmpty();
    }

    // Decision 1 of audit fix 09: a rule that the document cannot read is reported, never copied as a number. Each one is listed here, so
    // a new kind of rule, such as a range, fails the test until someone decides how the document shows it. A predicate (Must) has no
    // form to read, and the time zone's regex has no pattern. A rule that needs a value is required in the document already: the
    // generator marks the field required from the request's constructor.
    [Fact]
    public void BuildOpenApiDocument_RuleTheDocumentCannotRead_IsAKnownOne()
    {
        string[] known =
        [
            "AcceptInvitationRequest.firstName PredicateValidator",
            "AcceptInvitationRequest.lastName PredicateValidator",
            "ChangePreferencesRequest.timeZone RegularExpressionValidator",
            "RenameUserRequest.firstName PredicateValidator",
            "RenameUserRequest.lastName PredicateValidator",
            "SuspendTenantRequest.reason PredicateValidator",
        ];

        var unread = RequestRules()
            .Where(rule => rule.Rule is not (ILengthValidator or IEmailValidator or INotNullValidator or INotEmptyValidator))
            .Where(rule => !(rule.Rule is IRegularExpressionValidator && PatternedRequestFields().Contains(rule.Field)))
            .Select(rule => $"{rule.Field} {rule.Rule.Name}")
            .Distinct();

        unread.ShouldBe(known, ignoreOrder: true);
    }

    // Decision 4 of audit fix 09: the limits of a list's query are the constants that the API checks, and the search says that the API
    // trims it.
    [Fact]
    public void BuildOpenApiDocument_QueryParameterOfAList_HasTheLimitsTheApiChecks()
    {
        var expected = new Dictionary<string, string>
        {
            ["page"] = $"minimum {PageRequest.FirstPage}",
            ["pageSize"] = $"minimum {PageRequest.MinPageSize}, maximum {PageRequest.MaxPageSize}",
            ["limit"] = $"minimum {CursorRequest.MinLimit}, maximum {CursorRequest.MaxLimit}",
            ["q"] = $"maxLength {ListRequest.SearchMaxLength}",
        };
        var parameters = Operations()
            .SelectMany(operation => QueryParametersOf(operation.Value).Select(parameter => (Operation: OperationIdOf(operation.Value), Parameter: parameter)))
            .Where(found => expected.ContainsKey(found.Parameter.GetProperty("name").GetString()!))
            .ToList();

        var wrong = parameters
            .Where(found => LimitsOf(found.Parameter.GetProperty("schema")) != expected[found.Parameter.GetProperty("name").GetString()!])
            .Select(found => $"{found.Operation} {found.Parameter.GetProperty("name").GetString()}: {LimitsOf(found.Parameter.GetProperty("schema"))}");
        var untrimmed = parameters
            .Where(found => found.Parameter.GetProperty("name").GetString() == "q" && !DescriptionOf(found.Parameter).Contains("trims", StringComparison.Ordinal))
            .Select(found => found.Operation);

        parameters.Select(found => found.Parameter.GetProperty("name").GetString()).Distinct().ShouldBe(expected.Keys, ignoreOrder: true);
        wrong.ShouldBeEmpty();
        untrimmed.ShouldBeEmpty();
    }

    // Decision 2 of audit fix 09: a description names the API's limit, never its number, so the number has one owner, the rule, and a
    // changed rule never leaves an old number in the document. The numbers are each maximum length a Domain project declares and the
    // limits of a list.
    [Fact]
    public void BuildOpenApiDocument_NoDescription_TypesTheNumberOfALimit()
    {
        int[] limits = [.. DomainMaxLengths(), PageRequest.MaxPageSize, ListRequest.SearchMaxLength, CursorRequest.MaxLimit];

        var typed = ObjectsIn(_document, "#")
            .Where(found => found.Value.TryGetProperty("description", out var description) && description.ValueKind == JsonValueKind.String)
            .Where(found => limits.Any(limit => Regex.IsMatch(found.Value.GetProperty("description").GetString()!, $@"\b{limit}\b")))
            .Select(found => found.Path);

        DomainMaxLengths().ShouldNotBeEmpty();
        typed.ShouldBeEmpty();
    }

    // A Domain type is internal to its module, so its constants are read by reflection.
    private static IEnumerable<int> DomainMaxLengths() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Domain").GetTypes())
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.FieldType == typeof(int) && field.Name.EndsWith("MaxLength", StringComparison.Ordinal))
            .Select(field => (int)field.GetRawConstantValue()!);

    // The rules of every validator, by the request field each names. The command X of a request XRequest carries the request's fields
    // under the same names, and a validation answer names a field by its command property in camel case (K5).
    private static IEnumerable<(string Field, IPropertyValidator Rule, string ErrorCode)> RequestRules() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Application").GetExportedTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .SelectMany(type => ((IValidator)Activator.CreateInstance(type)!).CreateDescriptor().GetMembersWithValidators()
                .SelectMany(member => member.Select(rule =>
                    ($"{RequestOf(type.BaseType!.GetGenericArguments()[0])}.{JsonNamingPolicy.CamelCase.ConvertName(member.Key)}",
                        rule.Validator,
                        rule.Options.ErrorCode))));

    // N8: the command XCommand is carried by the request XRequest, as OpenApiContract maps them.
    private static string RequestOf(Type command) => $"{command.Name[..^"Command".Length]}Request";

    private List<string> PatternedRequestFields() =>
        [.. RequestFields().Where(field => field.Value.TryGetProperty("pattern", out _)).Select(field => field.Name)];

    // A length the schema leaves out is 0 for minLength, as JSON Schema reads it.
    private static int LengthOf(JsonElement schema, string keyword) =>
        schema.TryGetProperty(keyword, out var length) ? length.GetInt32() : 0;

    private static IEnumerable<JsonElement> QueryParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray().Where(parameter => parameter.GetProperty("in").GetString() == "query")
            : [];

    private static readonly string[] Limits = ["minimum", "maximum", "maxLength"];

    // The numeric and length limits of a schema, in one order.
    private static string LimitsOf(JsonElement schema) =>
        string.Join(", ", Limits
            .Where(limit => schema.TryGetProperty(limit, out _))
            .Select(limit => $"{limit} {schema.GetProperty(limit).GetInt32()}"));

    private IEnumerable<(string Name, JsonElement Value)> RequestFields() =>
        Schemas()
            .Where(schema => schema.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(schema => PropertiesOf(schema.Value).Select(property => ($"{schema.Name}.{property.Name}", property.Value)));

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

    private static Type? EnumTypeOf(Type type) => (Nullable.GetUnderlyingType(type) ?? type) is { IsEnum: true } found ? found : null;

    // The values an enum has in JSON, from SharedKernel's one source of them (B10).
    private static string JsonValuesOf(Type type) =>
        type.IsEnum ? string.Join(", ", JsonNamesOf(type)) : $"<{type.Name}, not an enum>";

    private static IReadOnlyList<string> JsonNamesOf(Type type) =>
        (IReadOnlyList<string>)typeof(JsonEnum<>).MakeGenericType(type).GetProperty(nameof(JsonEnum<>.Names))!.GetValue(null)!;

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
