using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.ErrorHandling;
using Api.Idempotency;
using Api.RateLimiting;
using ControlPlane.Contracts;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi;
using SharedKernel;

namespace Api.Hosting;

// N11: the OpenAPI document is the contract every client is generated from. These transformers add what a generated client needs and
// the endpoints do not state: an operation id for each endpoint, the parameters a route group puts in a path, the values of a list's
// sort and filters, the limits of a request and of a list, the values of an enum with a converter of its own, the idempotency key and
// the headers of an answer, the bearer scheme, the members of the one error schema with the enums of the error codes, the enum of the
// permissions, and the enum of the audit operations. They change the document only. The one rule here that the wire follows is how
// JSON reads a number, because the document describes the numbers from it.
internal static partial class OpenApiContract
{
    private const string BearerScheme = "bearer";
    private const string PermissionSchema = "Permission";
    private const string PermissionsMember = "permissions";
    private const string AuditOperationSchema = "AuditOperation";
    private const string AuditOperationsClass = "AuditOperations";
    private const string OperationMember = "operation";

    // An idempotent operation replays a saved result: its success or a business rejection. A request that failed validation (400) or
    // authentication (401) saves none (I5, I6).
    private static readonly string[] NeverReplayed = ["400", "401"];

    public static IServiceCollection AddApiContract(this IServiceCollection services) =>
        services
            // A number is a JSON number, never a string: the web defaults read one from a string, and the document would then call each
            // number an integer or a string.
            .ConfigureHttpJsonOptions(options => options.SerializerOptions.NumberHandling = JsonNumberHandling.Strict)
            .AddOpenApi(options =>
            {
                options.CreateSchemaReferenceId = NameSchema;
                options
                    .AddOperationTransformer(NameOperation)
                    .AddOperationTransformer(DeclarePathParameters)
                    .AddOperationTransformer(DescribeListParameters)
                    .AddOperationTransformer(DescribeListLimits)
                    .AddOperationTransformer(DescribeIdempotency)
                    .AddOperationTransformer(DescribeLocation)
                    .AddOperationTransformer(DescribeRetryAfter)
                    .AddSchemaTransformer(DescribeEnumValues)
                    .AddSchemaTransformer(DescribeRequestRules)
                    .AddDocumentTransformer(RequireBearerToken)
                    .AddDocumentTransformer(DescribeProblemDetails)
                    .AddDocumentTransformer(DescribePermissions)
                    .AddDocumentTransformer(DescribeAuditOperations);
            });

    // A nullable enum, such as MemberRole?, gets the schema name of its enum, MemberRole, so the two schemas would share one name, and the
    // one written last would win: the enum would refer to itself. A nullable enum so has no schema of its own; DescribeEnumValues writes
    // it in its field, as a reference to the enum's schema that may be null.
    private static string? NameSchema(JsonTypeInfo type) =>
        Nullable.GetUnderlyingType(type.Type) is { IsEnum: true } ? null : OpenApiOptions.CreateDefaultSchemaReferenceId(type);

    // N7: an endpoint is a method with the action's name, so the method's name is the operation id.
    private static Task NameOperation(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.OperationId ??= context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault()?.Name;
        return Task.CompletedTask;
    }

    // OpenAPI 3.1, "Path Templating": each parameter in a path is a declared path parameter. A route group's parameter, such as the
    // tenant's {tenantId} (T1), reaches no endpoint method, so the document would leave it out. The description's path has lost the
    // route constraints; the endpoint's route diagnostics metadata keeps the route as it was mapped, so {tenantId:guid} is a uuid.
    private static Task DeclarePathParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var route = RoutePatternFactory.Parse(context.Description.ActionDescriptor.EndpointMetadata.OfType<IRouteDiagnosticsMetadata>().Single().Route);
        foreach (var parameter in route.Parameters.Where(parameter => operation.Parameters?.Any(declared => declared.Name == parameter.Name) != true))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = parameter.Name,
                In = ParameterLocation.Path,
                Required = true,
                Schema = parameter.ParameterPolicies.Any(policy => policy.Content == "guid")
                    ? new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" }
                    : new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }

        return Task.CompletedTask;
    }

    // K5: the sort and each filter of a list are enums, so a generated client knows their values. Their parameter types bind any text,
    // which ListRequest and ListFilter check, so the document would call them strings; the values are the JSON names the check accepts.
    private static Task DescribeListParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var description in context.Description.ParameterDescriptions)
        {
            var type = Nullable.GetUnderlyingType(description.Type) ?? description.Type;
            var values = type.IsGenericType ? ListValuesOf(type.GetGenericTypeDefinition(), type.GetGenericArguments()[0]) : null;
            if (values is not null && operation.Parameters?.SingleOrDefault(parameter => parameter.Name == description.Name) is OpenApiParameter parameter)
            {
                parameter.Schema = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [.. values.Select(value => JsonValue.Create(value))] };
            }
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string>? ListValuesOf(Type parameter, Type values)
    {
        var describe = parameter == typeof(ListSort<>) ? typeof(ListSort) : parameter == typeof(ListFilter<>) ? typeof(ListFilter) : null;
        return (IReadOnlyList<string>?)describe?.GetMethod(nameof(ListSort.ValuesOf))!.MakeGenericMethod(values).Invoke(null, null);
    }

    // K5: the limits of a list's query, from the constants that PageRequest, CursorRequest and ListRequest check. A parameter is found
    // by its name, as the list contract names it. The search is trimmed before its length is counted.
    private static Task DescribeListLimits(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        foreach (var parameter in (operation.Parameters ?? []).Where(parameter => parameter.In == ParameterLocation.Query).OfType<OpenApiParameter>())
        {
            if (parameter.Schema is not OpenApiSchema schema)
            {
                continue;
            }

            switch (parameter.Name)
            {
                case "page":
                    schema.Minimum = Number(PageRequest.FirstPage);
                    break;
                case "pageSize":
                    schema.Minimum = Number(PageRequest.MinPageSize);
                    schema.Maximum = Number(PageRequest.MaxPageSize);
                    break;
                case "limit":
                    schema.Minimum = Number(CursorRequest.MinLimit);
                    schema.Maximum = Number(CursorRequest.MaxLimit);
                    break;
                case "q":
                    schema.MaxLength = ListRequest.SearchMaxLength;
                    parameter.Description = "The search. The API trims it before it counts its length.";
                    break;
            }
        }

        return Task.CompletedTask;
    }

    // I1, I2 and I5: an idempotent operation requires its key, and each answer it can replay may carry Idempotent-Replayed: true.
    private static Task DescribeIdempotency(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotencyKeyRequired>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyMiddleware.Header,
            In = ParameterLocation.Header,
            Required = true,
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = IdempotencyMiddleware.MaxLength, Pattern = "^[!-~]+$" },
        });
        foreach (var (status, answer) in operation.Responses ?? [])
        {
            if (!NeverReplayed.Contains(status) && answer is OpenApiResponse response)
            {
                response.Headers ??= new Dictionary<string, IOpenApiHeader>();
                response.Headers[IdempotencyExecutor.ReplayedHeader] = new OpenApiHeader
                {
                    Description = "true when the answer replays the saved answer of an earlier request with the same key; absent otherwise.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Enum = [JsonValue.Create("true")] },
                };
            }
        }

        return Task.CompletedTask;
    }

    // RFC 9110, section 10.2.2: where a GET reads the resource an endpoint creates, its 201 names the resource in Location
    // (ResultEndpointFilter).
    private static Task DescribeLocation(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<CreatedLocation>().Any()
            && operation.Responses?.GetValueOrDefault(StatusCodes.Status201Created.ToString(CultureInfo.InvariantCulture)) is OpenApiResponse created)
        {
            created.Headers ??= new Dictionary<string, IOpenApiHeader>();
            created.Headers[HeaderNames.Location] = new OpenApiHeader
            {
                Description = "The address of the new resource, where a GET reads it.",
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri-reference" },
            };
        }

        return Task.CompletedTask;
    }

    // RFC 9110, section 10.2.3: the rate limiter answers 429 with Retry-After, the seconds to wait (UserRateLimiting). Each operation
    // it covers declares the header on its 429. A 429 of a handler, such as the daily limit of invitations, sends none, so the header is
    // not required.
    private static Task DescribeRetryAfter(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (UserRateLimiting.Covers(context.Description.ActionDescriptor.EndpointMetadata)
            && operation.Responses?.GetValueOrDefault(StatusCodes.Status429TooManyRequests.ToString(CultureInfo.InvariantCulture)) is OpenApiResponse rejected)
        {
            rejected.Headers ??= new Dictionary<string, IOpenApiHeader>();
            rejected.Headers[HeaderNames.RetryAfter] = new OpenApiHeader
            {
                Description = "The seconds to wait before the next request, when the rate limiter refused this one.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = Number(0) },
            };
        }

        return Task.CompletedTask;
    }

    // Decision 1 of audit fix 09: the validators are the one source of a request's limits. The command X of a request XRequest carries
    // the request's fields under the same names (K5), so the rules of X's validator describe XRequest. The validator's descriptor lists
    // its rules: a length rule gives minLength and maxLength, an email rule format email, and a regex rule the pattern, where ECMAScript
    // reads the regex as .NET does. The generator marks a field required from the request's constructor, and no request body takes a
    // number, so no rule is a range. Any other rule, such as a predicate (Must) or the time zone's regex, stays out of the document, and
    // OpenApiContractTests lists each one, so a new kind fails until someone decides how the document shows it.
    private static Task DescribeRequestRules(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!RequestValidators.TryGetValue(context.JsonTypeInfo.Type.Name, out var validatorType))
        {
            return Task.CompletedTask;
        }

        var validator = (IValidator)ActivatorUtilities.CreateInstance(context.ApplicationServices, validatorType);
        foreach (var member in validator.CreateDescriptor().GetMembersWithValidators())
        {
            var name = JsonNamingPolicy.CamelCase.ConvertName(member.Key);
            if (schema.Properties?.TryGetValue(name, out var property) != true || property is not OpenApiSchema field)
            {
                continue;
            }

            foreach (var (rule, _) in member)
            {
                switch (rule)
                {
                    case ILengthValidator length:
                        field.MinLength = length.Min > 0 ? length.Min : field.MinLength;
                        field.MaxLength = length.Max >= 0 ? length.Max : field.MaxLength;
                        break;
                    case IEmailValidator:
                        field.Format = "email";
                        break;
                    case IRegularExpressionValidator regex when !NotEcmaScript().IsMatch(regex.Expression):
                        field.Pattern = regex.Expression;
                        break;
                }
            }
        }

        return Task.CompletedTask;
    }

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    // A pattern of the document is an ECMAScript regex (OpenAPI 3.1, JSON Schema Validation, section 6.3.3). These .NET anchors and
    // lookbehinds have no ECMAScript form, or another meaning there, so a regex with one is no pattern: \A, \Z, \z, (?<= and (?<!.
    [GeneratedRegex(@"\\[AZz]|\(\?<[=!]")]
    private static partial Regex NotEcmaScript();

    // The generator knows the values of the default enum converter only. An enum with a converter of its own, such as Language and
    // MemberRole, which read an exact name only (ExactNameEnumConverter), would so have a schema without values, and a nullable field of
    // it no schema at all. So the schema is a string with the JSON names, the names the converter reads, and a nullable field refers to
    // the schema and may be null. OpenApiContractTests fails for a field that holds a language and does not refer to the schema Language.
    private static async Task DescribeEnumValues(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        var type = context.JsonTypeInfo.Type;
        if (type.IsEnum && schema.Enum is null)
        {
            var values = (IReadOnlyList<string>)typeof(ListFilter).GetMethod(nameof(ListFilter.ValuesOf))!
                .MakeGenericMethod(type).Invoke(null, null)!;
            schema.Type = JsonSchemaType.String;
            schema.Enum = [.. values.Select(value => (JsonNode)JsonValue.Create(value))];
        }
        else if (Nullable.GetUnderlyingType(type) is { IsEnum: true } underlying && schema is { Type: null, Enum: null, OneOf: null })
        {
            schema.OneOf =
            [
                new OpenApiSchema { Type = JsonSchemaType.Null },
                await context.GetOrCreateSchemaAsync(underlying, cancellationToken: cancellationToken),
            ];
        }
    }

    // A5: every /v1 route is signed-in only or requires a permission, so it needs a bearer token from the identity provider. The anonymous
    // routes, the document and the API reference, are outside /v1 and so outside the document (OpenApiContractTests), so no operation
    // sets the requirement aside.
    private static Task RequireBearerToken(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "A session token of the identity provider, in the Authorization header.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [] }];
        return Task.CompletedTask;
    }

    // K5: every error answer is Problem Details (ResultResponses) with a code. The host adds four members to its schema: the code, one
    // of the one enum of error codes; the field codes of a request that failed validation, an enum too; the trace id; and the tenants an
    // error is about, which only some errors name. The enums are
    // schemas of their own, so a generated client has a type for each and can check that a screen has a text for every code. Every
    // answer carries the code and the trace id, so both are required; the field codes and the tenants may be absent, so a client reads
    // them as may be null. Every operation also declares Problem Details as its default answer, for a status it does not list, such as
    // 405, 408, 413, 415 or 500 (Problems).
    private static Task DescribeProblemDetails(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas?.TryGetValue(nameof(ProblemDetails), out var found) != true || found is not OpenApiSchema problem)
        {
            return Task.CompletedTask;
        }

        document.Components.Schemas[nameof(ErrorCode)] = EnumSchema(
            DeclaredErrorCodes, "The error's code, area.reason. A client shows its own text for it, never the English detail.");
        document.Components.Schemas[nameof(FieldErrorCode)] = EnumSchema(Problems.FieldCodes, "Why one field of a request failed validation.");

        problem.Properties ??= new Dictionary<string, IOpenApiSchema>();
        problem.Properties[Problems.CodeMember] = new OpenApiSchemaReference(nameof(ErrorCode), document);
        problem.Properties[Problems.ErrorsMember] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object | JsonSchemaType.Null,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchemaReference(nameof(FieldErrorCode), document) },
            Description = "The field codes for each field of a request that failed validation, by the field's JSON name.",
        };
        problem.Properties[Problems.TraceIdMember] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The trace id of the request, for the logs.",
        };
        problem.Properties[Problems.TenantIdsMember] = new OpenApiSchema
        {
            Type = JsonSchemaType.Array | JsonSchemaType.Null,
            Items = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            Description = "The tenants the error is about, when a client needs them to act, such as the tenants a user is the last Owner of.",
        };
        problem.Required ??= new HashSet<string>();
        problem.Required.Add(Problems.CodeMember);
        problem.Required.Add(Problems.TraceIdMember);

        foreach (var (_, operation) in document.Paths.Values.SelectMany(path => path.Operations ?? []))
        {
            operation.Responses ??= [];
            operation.Responses["default"] = new OpenApiResponse
            {
                Description = "Any other error answer.",
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(nameof(ProblemDetails), document) },
                },
            };
        }

        return Task.CompletedTask;
    }

    // The host's assembly and each assembly it references, directly or not, that references SharedKernel: only such an assembly can
    // declare an error code, the audit operations or a command's validator, and each module's Domain and Application project is one.
    // The search starts at the host and follows only those references.
    private static IReadOnlyList<Assembly> AssembliesThatReachSharedKernel { get; } = FindAssembliesThatReachSharedKernel();

    private static List<Assembly> FindAssembliesThatReachSharedKernel()
    {
        var kernel = typeof(ErrorCode).Assembly.GetName().Name;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Queue<Assembly>([typeof(OpenApiContract).Assembly]);
        var found = new List<Assembly>();
        while (pending.TryDequeue(out var assembly))
        {
            if (!seen.Add(assembly.GetName().Name!))
            {
                continue;
            }

            found.Add(assembly);
            foreach (var referenced in assembly.GetReferencedAssemblies().Where(name => !seen.Contains(name.Name!)).Select(Assembly.Load))
            {
                if (referenced.GetName().Name == kernel || referenced.GetReferencedAssemblies().Any(name => name.Name == kernel))
                {
                    pending.Enqueue(referenced);
                }
            }
        }

        return found;
    }

    // The validator of each command, by the name of the request that carries the command: X's validator describes XRequest. A command's
    // validator sits in its module's Application project, which reaches SharedKernel, as each module's Domain project does.
    private static Dictionary<string, Type> RequestValidators { get; } = AssembliesThatReachSharedKernel
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type is { IsAbstract: false, BaseType.IsGenericType: true } && type.BaseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>))
        .ToDictionary(type => $"{type.BaseType!.GetGenericArguments()[0].Name}Request", StringComparer.Ordinal);

    // K5: each owner declares its codes in a static class whose name ends with ErrorCodes: SharedKernel, the host, and each module in its
    // Domain project. The document joins them into one enum. ErrorCodeTests checks that it finds every declared code.
    private static IReadOnlyList<string> DeclaredErrorCodes { get; } =
    [
        .. AssembliesThatReachSharedKernel.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: true, IsSealed: true } && type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(field => field.FieldType == typeof(ErrorCode))
            .Select(field => ((ErrorCode)field.GetValue(null)!).Value)
            .Distinct()
            .Order(StringComparer.Ordinal),
    ];

    // K3: Audit declares the operation of every audit record once, in Audit.Domain's AuditOperations. The host does not reference a
    // Domain project (section 2), so it finds the class by its name, as it finds the error codes. OpenApiContractTests checks the enum.
    private static IReadOnlyList<string> DeclaredAuditOperations { get; } =
    [
        .. AssembliesThatReachSharedKernel.SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: true, IsSealed: true, Name: AuditOperationsClass })
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal),
    ];

    // The operations of both audit logs are one enum in the document, so a client has a type for them and hears of a new one. A
    // response keeps the operation as a string, so each field that holds one, a field named operation, refers to the enum
    // (AuditEntryResponse.operation). OpenApiContractTests checks both.
    private static Task DescribeAuditOperations(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is not { } schemas)
        {
            return Task.CompletedTask;
        }

        schemas[AuditOperationSchema] = EnumSchema(DeclaredAuditOperations, "The operation of an audit record, area.action.");
        foreach (var schema in schemas.Values.OfType<OpenApiSchema>().Where(schema => schema.Properties?.ContainsKey(OperationMember) == true))
        {
            schema.Properties![OperationMember] = new OpenApiSchemaReference(AuditOperationSchema, document);
        }

        return Task.CompletedTask;
    }

    // The web shows an action only when the member's permissions hold its name (section 6, "What the screens read"). The names are one
    // enum in the document, every constant of the catalogue (Permissions), so a generated client has a type for them. A response keeps
    // the names as strings, so each field that holds them, a field named permissions, refers to the enum. OpenApiContractTests checks both.
    private static Task DescribePermissions(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Components?.Schemas is not { } schemas)
        {
            return Task.CompletedTask;
        }

        schemas[PermissionSchema] = EnumSchema(CataloguePermissions, "A permission of the catalogue. Every endpoint checks its own permission.");
        foreach (var schema in schemas.Values.OfType<OpenApiSchema>().Where(schema => schema.Properties?.ContainsKey(PermissionsMember) == true))
        {
            schema.Properties![PermissionsMember] = new OpenApiSchema
            {
                Type = JsonSchemaType.Array,
                Items = new OpenApiSchemaReference(PermissionSchema, document),
            };
        }

        return Task.CompletedTask;
    }

    private static IReadOnlyList<string> CataloguePermissions { get; } =
    [
        .. typeof(Permissions).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (string)field.GetRawConstantValue()!)
            .Order(StringComparer.Ordinal),
    ];

    private static OpenApiSchema EnumSchema(IReadOnlyList<string> values, string description) =>
        new() { Type = JsonSchemaType.String, Enum = [.. values.Select(value => (JsonNode)JsonValue.Create(value))], Description = description };
}
