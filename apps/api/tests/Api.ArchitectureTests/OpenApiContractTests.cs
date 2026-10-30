using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Audit.Api.Tenant.AuditLogs;
using Audit.Domain;
using ControlPlane.Contracts;
using ControlPlane.Contracts.Plans;
using ControlPlane.Domain.Tenants;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.DependencyInjection;
using Notifications.Domain;
using SharedKernel;

namespace Api.ArchitectureTests;

// N11: the committed OpenAPI document is the contract every client is generated from. Each operation has its endpoint's name as its
// id, each enum in a response lists exactly the values of its own enum (N7n), each path parameter is declared, and each error
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

    private const string AuditRoleSchema = "#/components/schemas/AuditRoleResponse";

    private const string EnumNames = "x-enumNames";

    // The enums the host adds to fields that are strings in C# (N11i, N11j): the permissions and the audit operations.
    private const string NotificationTypeSchema = "#/components/schemas/NotificationType";

    private const string NotificationCategorySchema = "#/components/schemas/NotificationCategory";

    private static readonly string[] EnumsOfStrings = [PermissionSchema, AuditOperationSchema, NotificationTypeSchema, NotificationCategorySchema];

    private static readonly string[] Limits = ["minimum", "maximum", "maxLength"];

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

    // N7e: an endpoint is a named method. A lambda's method has a compiler-generated name, such as <Map>b__0_0, which is no name.
    [Fact]
    public void BuildOpenApiDocument_EveryOperationId_IsAPascalCaseName()
    {
        var ids = Operations().Select(operation => OperationIdOf(operation.Value));

        ids.ShouldNotBeEmpty();
        ids.Where(id => !PascalCaseName().IsMatch(id)).ShouldBeEmpty();
    }

    // N7n: every enum of a response lists exactly the values of its C# enum: a status, a state, a kind, a role or a language. The
    // host adds two enums to fields that stay strings in C#, so the C# types cannot show them: their own tests read them.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumOfAResponse_ListsExactlyTheValuesOfItsEnum()
    {
        var expected = ResponseTypes()
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

    // N11f: an enum is a string on the wire, so each enum schema says so, and a generated client reads it as text.
    [Fact]
    public void BuildOpenApiDocument_EveryEnumSchema_HasTheTypeString()
    {
        var enums = Schemas().Where(schema => schema.Value.TryGetProperty("enum", out _));

        var untyped = enums.Where(schema => !schema.Value.TryGetProperty("type", out var type) || type.GetString() != "string").Select(schema => schema.Name);

        enums.ShouldNotBeEmpty();
        untyped.ShouldBeEmpty();
    }

    // N11z: a generated client names each member of an enum from x-enumNames, one name for each value.
    [Fact]
    public void BuildOpenApiDocument_EveryEnum_HasOneEnumNameForEachValue()
    {
        var enums = ObjectsIn(_document, "#").Where(found => found.Value.TryGetProperty("enum", out _)).ToList();

        var unnamed = enums
            .Where(found => !found.Value.TryGetProperty(EnumNames, out var names) || names.GetArrayLength() != found.Value.GetProperty("enum").GetArrayLength())
            .Select(found => found.Path);

        enums.ShouldNotBeEmpty();
        unnamed.ShouldBeEmpty();
    }

    // N11z: two members of one enum never share a name, or the generated client would keep one of them.
    [Fact]
    public void BuildOpenApiDocument_EnumNamesOfOneEnum_AreUnique()
    {
        var named = ObjectsIn(_document, "#").Where(found => found.Value.TryGetProperty(EnumNames, out _)).ToList();

        var repeated = named
            .Where(found => NamesOf(found.Value).Distinct(StringComparer.Ordinal).Count() != NamesOf(found.Value).Count)
            .Select(found => found.Path);

        named.ShouldNotBeEmpty();
        repeated.ShouldBeEmpty();
    }

    // N11z: the name of a member is its value split on '.', '_' and '-', each part in PascalCase. A leading '-', a descending sort, is
    // the suffix Descending.
    [Theory]
    [InlineData("#/components/schemas/ErrorCode", "server.error", "ServerError")]
    [InlineData("#/components/schemas/ErrorCode", "user.suspended", "UserSuspended")]
    [InlineData("#/components/schemas/Permission", "platform.tenants.invite_owner", "PlatformTenantsInviteOwner")]
    [InlineData("#/components/schemas/Language", "en", "En")]
    [InlineData("#/paths/~1v1~1platform~1users/get/parameters/sort", "-email", "EmailDescending")]
    public void BuildOpenApiDocument_EnumValue_IsNamedFromItsParts(string schema, string value, string expected)
    {
        var found = EnumAt(schema);

        var name = NamesOf(found)[ValuesOf(found).IndexOf(value)];

        name.ShouldBe(expected);
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

    // K8: every invitation keeps the language of its email, so its answer always carries it, and a client that invites again can send it
    // back.
    [Theory]
    [InlineData("InvitationResponse")]
    [InlineData("OwnerInvitationResponse")]
    public void BuildOpenApiDocument_LanguageOfAnInvitation_IsARequiredLanguage(string name)
    {
        var schema = Schemas().Single(schema => schema.Name == name).Value;
        var language = schema.GetProperty("properties").GetProperty("language");

        RequiredOf(schema).ShouldContain("language");
        ReferencesIn(language).ShouldBe([LanguageSchema]);
        IsNullable(language).ShouldBeFalse();
    }

    // N11i and K5: the permission list is one enum in the document, so a generated client knows each name and a typo does not compile.
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

    // N11i and E1: the entitlement keys are two enums in the document, each in the order of its list, so a generated client knows
    // each key and its kind, and the platform shows a key added in code at once.
    [Theory]
    [InlineData("FeatureKey", typeof(Features))]
    [InlineData("LimitKey", typeof(Limits))]
    public void BuildOpenApiDocument_EntitlementKeys_ListEveryKeyOfItsListInItsOrder(string schemaName, Type keys)
    {
        var listed = keys.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!);

        var documented = EnumOf(Schemas().Single(schema => schema.Name == schemaName).Value);

        listed.ShouldNotBeEmpty();
        documented.ShouldBe(string.Join(", ", listed));
    }

    // N11i: the reserved slugs are the enum ReservedSlug, every slug of ControlPlane's list, so the web reads them from the generated
    // client and can check each of its top-level routes against them.
    [Fact]
    public void BuildOpenApiDocument_ReservedSlugs_ListEverySlugOfTheList()
    {
        var documented = EnumOf(Schemas().Single(schema => schema.Name == "ReservedSlug").Value);

        documented.ShouldContain("settings");
        documented.ShouldBe(string.Join(", ", ReservedSlugs.All));
    }

    // N11i and K5: every request and response field that holds permission names is an array of the one schema Permission, so the web
    // compares each name with the enum and sends only names of the list. A field is found by its name, as the types keep the names as
    // strings (MyTenantDetailResponse, CreateRoleRequest).
    [Fact]
    public void BuildOpenApiDocument_EveryRequestAndResponseFieldNamedPermissions_IsAnArrayOfPermission()
    {
        var expected = ResponseTypes()
            .Concat(RequestTypes())
            .Where(type => type.GetProperty("Permissions") is not null)
            .Select(type => $"{type.Name}.permissions");

        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => IsAnArrayOf(property.Value, PermissionSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // N7k and K5: every response field that holds a role is one of two schemas. A role of the tenant that a member, an invitation or a
    // tenant of the user holds is RoleResponse, { id, name, builtIn }, so a request gives it back by its id. A role in the details of an
    // audit entry is AuditRoleResponse, the role as it was at that moment (the maintainer's decision in step r4b). A field holds a role when it is named role, or when its type is one of the two.
    [Fact]
    public void BuildOpenApiDocument_EveryResponseFieldThatHoldsARole_RefersToOneOfTheTwoRoleSchemas()
    {
        var expected = ResponseTypes()
            .SelectMany(type => type.GetProperties()
                .Where(property => property.Name == "Role" || property.PropertyType.Name is "RoleResponse" or nameof(AuditRoleResponse))
                .Select(property => $"{type.Name}.{JsonNamingPolicy.CamelCase.ConvertName(property.Name)} {RoleSchemaOf(property.PropertyType)}"));

        var documented = ResponseSchemas()
            .SelectMany(schema => PropertiesOf(schema.Value)
                .Where(property => property.Name == "role" || ReferencesIn(property.Value).Contains(RoleSchema) || ReferencesIn(property.Value).Contains(AuditRoleSchema))
                .Select(property => $"{schema.Name}.{property.Name} {string.Join(", ", ReferencesIn(property.Value))}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
        var role = SchemaAt(RoleSchema);
        PropertiesOf(role).Select(property => property.Name).ShouldBe(["id", "name", "builtIn"], ignoreOrder: true);
        IsNullable(role.GetProperty("properties").GetProperty("id")).ShouldBeFalse();
        var auditRole = SchemaAt(AuditRoleSchema);
        PropertiesOf(auditRole).Select(property => property.Name).ShouldBe(["id", "name", "builtIn"], ignoreOrder: true);
        IsNullable(auditRole.GetProperty("properties").GetProperty("id")).ShouldBeFalse();
    }

    // N11j and K3: the operations of the audit entries are one enum in the document, so a client knows each one and hears of a new one.
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

    // U1: the notification types and categories are the two schemas NotificationType and NotificationCategory, with every value of the
    // catalog, and every field named category, and the type beside it, refers to them.
    [Fact]
    public void BuildOpenApiDocument_NotificationTypesAndCategories_ListTheCatalogAndAreReferredToByTheirFields()
    {
        var types = EnumOf(Schemas().Single(schema => schema.Name == "NotificationType").Value);
        var categories = EnumOf(Schemas().Single(schema => schema.Name == "NotificationCategory").Value);
        var referring = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Any(reference => reference is NotificationTypeSchema or NotificationCategorySchema))
                .Select(property => $"{schema.Name}.{property.Name}"));

        types.ShouldBe(string.Join(", ", NotificationCatalog.Kinds.Keys.Order(StringComparer.Ordinal)));
        categories.ShouldBe(string.Join(", ", NotificationCatalog.Categories));
        referring.ShouldBe(
            [
                "CategoryEmailPreferenceRequest.category",
                "CategoryEmailPreferenceResponse.category",
                "NotificationResponse.type",
                "NotificationResponse.category",
            ],
            ignoreOrder: true);
    }

    // N11j and K3: the operation of an entry of either audit log is the one schema AuditOperation. A field is found by its name, as the
    // response types keep the operation as a string (AuditEntryResponse).
    [Fact]
    public void BuildOpenApiDocument_EveryResponseFieldNamedOperation_IsAnAuditOperation()
    {
        var expected = ResponseTypes()
            .Where(type => type.GetProperty("Operation") is not null)
            .Select(type => $"{type.Name}.operation");

        var documented = Schemas()
            .Where(schema => schema.Value.TryGetProperty("properties", out _))
            .SelectMany(schema => schema.Value.GetProperty("properties").EnumerateObject()
                .Where(property => ReferencesIn(property.Value).Contains(AuditOperationSchema))
                .Select(property => $"{schema.Name}.{property.Name}"));

        expected.ShouldNotBeEmpty();
        documented.ShouldBe(expected, ignoreOrder: true);
    }

    // K3 and D1: the details of an audit entry are one of its shapes, by its operation. The discriminator maps each operation to the schema
    // of its shape, by the shape's own name. Each shape requires the operation and holds only that one value, so a generated client narrows
    // the details by it.
    [Fact]
    public void BuildOpenApiDocument_DetailsOfAnAuditEntry_AreAShapeForEachOperationThatKeepsDetails()
    {
        var expected = typeof(AuditDetailsResponse).GetCustomAttributes<JsonDerivedTypeAttribute>()
            .Select(shape => $"{shape.TypeDiscriminator} {shape.DerivedType.Name}: requires operation True, {shape.TypeDiscriminator}");
        var details = Schemas().Single(schema => schema.Name == nameof(AuditDetailsResponse)).Value;

        var documented = details.GetProperty("discriminator").GetProperty("mapping").EnumerateObject()
            .Select(shape => (Operation: shape.Name, Name: shape.Value.GetString()!.Split('/')[^1], Schema: SchemaAt(shape.Value.GetString()!)))
            .Select(shape => $"{shape.Operation} {shape.Name}: requires operation {RequiredOf(shape.Schema).Contains("operation")}, "
                + EnumOf(shape.Schema.GetProperty("properties").GetProperty("operation")));

        details.GetProperty("discriminator").GetProperty("propertyName").GetString().ShouldBe("operation");
        ReferencesIn(details.GetProperty("anyOf")).Order(StringComparer.Ordinal)
            .ShouldBe(details.GetProperty("discriminator").GetProperty("mapping").EnumerateObject().Select(shape => shape.Value.GetString()!).Order(StringComparer.Ordinal));
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

        located.Select(operation => OperationIdOf(operation.Value)).ShouldBe(["CreateTenant", "CreatePlan"], ignoreOrder: true);
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

    // N7d, N8: an operation has one tag, the folder of its endpoints below the module's Api project, {Audience}/{Resource}, so the API
    // reference and a generated client group the operations as the folders do. The document lists each tag it uses.
    [Fact]
    public void BuildOpenApiDocument_EveryOperation_HasOneTagThatIsAnAudienceAndResourceFolder()
    {
        var tags = Operations().Select(operation => (Operation: $"{operation.Method} {operation.Path}", Tags: TagsOf(operation.Value))).ToList();
        var folders = Solution.LayerOfEveryModule("Api")
            .SelectMany(project => Directory.EnumerateDirectories(Solution.FolderOf(project)).SelectMany(Directory.EnumerateDirectories)
                .Select(folder => Path.GetRelativePath(Solution.FolderOf(project), folder).Replace(Path.DirectorySeparatorChar, '/')))
            .ToHashSet();
        var listed = _document.GetProperty("tags").EnumerateArray().Select(tag => tag.GetProperty("name").GetString()!);

        var notOne = tags.Where(operation => operation.Tags.Count != 1).Select(operation => operation.Operation);
        var noFolder = tags.SelectMany(operation => operation.Tags).Where(tag => !folders.Contains(tag)).Distinct();

        tags.ShouldNotBeEmpty();
        notOne.ShouldBeEmpty();
        noFolder.ShouldBeEmpty();
        listed.ShouldBe(tags.SelectMany(operation => operation.Tags).Distinct(), ignoreOrder: true);
    }

    // Decision 1 of audit fix 09 and N8: the document finds a command's validator by the request that carries the command, XRequest for
    // XCommand. A validator whose request does not exist describes nothing in the document. A query's validator describes the query
    // parameters of its operation instead (step r13).
    [Fact]
    public void MapRequestToValidator_EveryCommandValidator_FindsItsRequest()
    {
        var requests = Solution.LayerOfEveryModule("Api")
            .SelectMany(project => Solution.Load(project).GetExportedTypes())
            .Select(type => type.Name)
            .Where(name => name.EndsWith("Request", StringComparison.Ordinal))
            .ToHashSet();
        var commands = CommandValidators()
            .Select(type => type.BaseType!.GetGenericArguments()[0])
            .ToList();

        var withoutRequest = commands.Select(RequestOf).Where(request => !requests.Contains(request));

        commands.ShouldNotBeEmpty();
        withoutRequest.ShouldBeEmpty();
    }

    // Decision 1 of audit fix 09: a rule that the document cannot read is reported, never copied as a number. Each one is listed here, so
    // a new kind of rule, such as a range, fails the test until someone decides how the document shows it. A predicate (Must) has no
    // form to read, and the time zone's regex has no pattern. The slug's predicate refuses a reserved slug, which the document lists as
    // the enum ReservedSlug. A rule that needs a value is required in the document already: the
    // generator marks the field required from the request's constructor.
    [Fact]
    public void BuildOpenApiDocument_RuleTheDocumentCannotRead_IsAKnownOne()
    {
        string[] known =
        [
            "AcceptInvitationRequest.firstName PredicateValidator",
            "AcceptInvitationRequest.lastName PredicateValidator",
            "ChangePreferencesRequest.timeZone RegularExpressionValidator",
            "CreateTenantRequest.slug PredicateValidator",
            "RenameMeRequest.firstName PredicateValidator",
            "RenameMeRequest.lastName PredicateValidator",
            "RenameUserRequest.firstName PredicateValidator",
            "RenameUserRequest.lastName PredicateValidator",
            "SuspendTenantRequest.reason PredicateValidator",
            "SetEntitlementOverrideRequest.reason PredicateValidator",
        ];

        var unread = RequestRules()
            .Where(rule => rule.Rule is not (ILengthValidator or IEmailValidator or INotNullValidator or INotEmptyValidator))
            .Where(rule => !(rule.Rule is IRegularExpressionValidator && PatternedRequestFields().Contains(rule.Field)))
            .Select(rule => $"{rule.Field} {rule.Rule.Name}")
            .Distinct();

        unread.ShouldBe(known, ignoreOrder: true);
    }

    // N11k for a query (step r13): the validator of XQuery describes the query parameters of the operation X. Each of its rules names a
    // parameter of that operation, and the document shows it: a length rule as maxLength, an email rule as format email. A rule that
    // needs a value says nothing the document can show. Any other kind of rule fails until someone decides how the document shows it,
    // and a rule with the too_long code is a length rule, as for a request.
    [Fact]
    public void BuildOpenApiDocument_RuleOfAQueryValidator_IsShownOnItsQueryParameter()
    {
        var parameters = Operations()
            .SelectMany(operation => QueryParametersOf(operation.Value)
                .Select(parameter => (Key: $"{OperationIdOf(operation.Value)}.{parameter.GetProperty("name").GetString()}", Schema: parameter.GetProperty("schema"))))
            .ToDictionary(parameter => parameter.Key, parameter => parameter.Schema);
        var rules = QueryValidators()
            .SelectMany(type => ((IValidator)Activator.CreateInstance(type)!).CreateDescriptor().GetMembersWithValidators()
                .SelectMany(member => member.Select(rule => (
                    Parameter: $"{type.BaseType!.GetGenericArguments()[0].Name[..^"Query".Length]}.{JsonNamingPolicy.CamelCase.ConvertName(member.Key)}",
                    rule.Validator,
                    rule.Options.ErrorCode))))
            .ToList();

        var unshown = rules
            .Where(rule => !parameters.TryGetValue(rule.Parameter, out var schema) || !Shows(schema, rule.Validator))
            .Select(rule => $"{rule.Parameter} {rule.Validator.Name}");
        var tooLong = rules.Where(rule => rule.ErrorCode == "TooLong" && rule.Validator is not ILengthValidator).Select(rule => rule.Parameter);

        rules.ShouldNotBeEmpty();
        unshown.ShouldBeEmpty();
        tooLong.ShouldBeEmpty();
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

    // The rules of every command's validator, by the request field each names. The command X of a request XRequest carries the request's
    // fields under the same names, and a validation answer names a field by its command property in camel case (K5).
    private static IEnumerable<(string Field, IPropertyValidator Rule, string ErrorCode)> RequestRules() =>
        CommandValidators()
            .SelectMany(type => ((IValidator)Activator.CreateInstance(type)!).CreateDescriptor().GetMembersWithValidators()
                .SelectMany(member => member.Select(rule =>
                    ($"{RequestOf(type.BaseType!.GetGenericArguments()[0])}.{JsonNamingPolicy.CamelCase.ConvertName(member.Key)}",
                        rule.Validator,
                        rule.Options.ErrorCode))));

    // The validators of the commands. A query's validator, such as ListUsersValidator, checks query parameters, not a request (step r13).
    private static IEnumerable<Type> CommandValidators() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Application").GetExportedTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Where(type => type.BaseType!.GetGenericArguments()[0].Name.EndsWith("Command", StringComparison.Ordinal));

    // The validators of the queries: XQuery's describes the query parameters of the operation X (step r13).
    private static IEnumerable<Type> QueryValidators() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Application").GetExportedTypes())
            .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
            .Where(type => type.BaseType!.GetGenericArguments()[0].Name.EndsWith("Query", StringComparison.Ordinal));

    // What the document shows of a rule on a query parameter.
    private static bool Shows(JsonElement schema, IPropertyValidator rule) => rule switch
    {
        ILengthValidator length => LengthOf(schema, "maxLength") == length.Max,
        IEmailValidator => FormatOf(schema) == "email",
        INotNullValidator or INotEmptyValidator => true,
        _ => false,
    };

    // N8: the command XCommand is carried by the request XRequest, as OpenApiContract maps them.
    private static string RequestOf(Type command) => $"{command.Name[..^"Command".Length]}Request";

    // A length the schema leaves out is 0 for minLength, as JSON Schema reads it.
    private static int LengthOf(JsonElement schema, string keyword) =>
        schema.TryGetProperty(keyword, out var length) ? length.GetInt32() : 0;

    private static IEnumerable<JsonElement> QueryParametersOf(JsonElement operation) =>
        operation.TryGetProperty("parameters", out var parameters)
            ? parameters.EnumerateArray().Where(parameter => parameter.GetProperty("in").GetString() == "query")
            : [];

    // The numeric and length limits of a schema, in one order.
    private static string LimitsOf(JsonElement schema) =>
        string.Join(", ", Limits
            .Where(limit => schema.TryGetProperty(limit, out _))
            .Select(limit => $"{limit} {schema.GetProperty(limit).GetInt32()}"));

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

    private static List<string> NamesOf(JsonElement schema) => [.. schema.GetProperty(EnumNames).EnumerateArray().Select(name => name.GetString()!)];

    private static List<string> ValuesOf(JsonElement schema) => [.. schema.GetProperty("enum").EnumerateArray().Select(value => value.GetString()!)];

    private static Type? EnumTypeOf(Type type) => (Nullable.GetUnderlyingType(type) ?? type) is { IsEnum: true } found ? found : null;

    // The values an enum has in JSON, from SharedKernel's one source of them (D1).
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

    // The response types of every module: those of its Api project, and those its Contracts shares with another module (N8).
    // A role of the audit details is Audit's own schema; any other role is the one of ControlPlane (N7k).
    private static string RoleSchemaOf(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) == typeof(AuditRoleResponse) ? AuditRoleSchema : RoleSchema;

    private static IEnumerable<Type> RequestTypes() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes())
            .Where(type => type.Name.EndsWith("Request", StringComparison.Ordinal));

    private static IEnumerable<Type> ResponseTypes() =>
        Solution.Modules
            .SelectMany(module => Solution.Load($"{module}.Api").GetExportedTypes().Concat(Solution.Load($"{module}.Contracts").GetExportedTypes()))
            .Where(type => type.Name.EndsWith("Response", StringComparison.Ordinal));

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

    private static List<string> TagsOf(JsonElement operation) =>
        operation.TryGetProperty("tags", out var tags) ? [.. tags.EnumerateArray().Select(tag => tag.GetString()!)] : [];

    [GeneratedRegex("^[A-Z][A-Za-z]+$")]
    private static partial Regex PascalCaseName();

    [GeneratedRegex("^[a-z]+(_[a-z]+)*\\.[a-z]+(_[a-z]+)*$")]
    private static partial Regex AreaAndReason();

    [GeneratedRegex(@"\{(\w+):[^}]+\}")]
    private static partial Regex RouteConstraint();

    private List<string> PatternedRequestFields() =>
        [.. RequestFields().Where(field => field.Value.TryGetProperty("pattern", out _)).Select(field => field.Name)];

    private IEnumerable<(string Name, JsonElement Value)> RequestFields() =>
        Schemas()
            .Where(schema => schema.Name.EndsWith("Request", StringComparison.Ordinal))
            .SelectMany(schema => PropertiesOf(schema.Value).Select(property => ($"{schema.Name}.{property.Name}", property.Value)));

    private IEnumerable<RouteEndpoint> DocumentedEndpoints() =>
        _application.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith("/v1/", StringComparison.Ordinal))
            .Where(endpoint => endpoint.Metadata.GetMetadata<IExcludeFromDescriptionMetadata>() is not { ExcludeFromDescription: true });

    private string SecuritySchemeOf(string name) =>
        _document.GetProperty("components").TryGetProperty("securitySchemes", out var schemes) && schemes.TryGetProperty(name, out var scheme)
            ? $"{scheme.GetProperty("type").GetString()} {scheme.GetProperty("scheme").GetString()}"
            : "<no scheme>";

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

    // A schema the document names, such as an enum, is a reference into components/schemas.
    private JsonElement Resolve(JsonElement schema) =>
        schema.TryGetProperty("$ref", out var reference)
            ? _document.GetProperty("components").GetProperty("schemas").GetProperty(reference.GetString()!.Split('/')[^1])
            : schema;

    private IEnumerable<(string Method, string Path, JsonElement Value)> Operations() =>
        _document.GetProperty("paths").EnumerateObject()
            .SelectMany(path => path.Value.EnumerateObject().Select(operation => (operation.Name, path.Name, operation.Value)));

    // The schema at a JSON pointer, with ~1 for '/'. In an array, such as an operation's parameters, a segment is the item's name, and
    // a parameter gives its schema.
    private JsonElement EnumAt(string pointer) =>
        pointer.Split('/').Skip(1).Select(segment => segment.Replace("~1", "/", StringComparison.Ordinal)).Aggregate(_document, (element, segment) =>
            element.ValueKind == JsonValueKind.Array
                ? element.EnumerateArray().Single(item => item.GetProperty("name").GetString() == segment).GetProperty("schema")
                : element.GetProperty(segment));

    private JsonElement.ObjectEnumerator Schemas() => _document.GetProperty("components").GetProperty("schemas").EnumerateObject();
}
