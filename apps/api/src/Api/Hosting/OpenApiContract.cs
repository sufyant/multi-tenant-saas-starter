using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;
using SharedKernel;

namespace Api.Hosting;

// N11: the OpenAPI document is the contract every client is generated from. These transformers add what a generated client needs and
// the endpoints do not state: an operation id for each endpoint, the parameters a route group puts in a path, the values of a list's
// sort and filters, and the members of the one error schema. They change the document only; the JSON on the wire stays as it is.
internal static class OpenApiContract
{
    public static OpenApiOptions DescribeContract(this OpenApiOptions options) =>
        options.AddOperationTransformer(NameOperation)
            .AddOperationTransformer(DeclarePathParameters)
            .AddOperationTransformer(DescribeListParameters)
            .AddSchemaTransformer(DescribeProblemDetails);

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

    // Every error answer is Problem Details (ResultResponses). The host adds three members to it: the error's code when an endpoint
    // returns a failed result, the fields of a request that failed validation, and the trace id of every error.
    private static Task DescribeProblemDetails(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.JsonTypeInfo.Type != typeof(ProblemDetails) || context.JsonPropertyInfo is not null)
        {
            return Task.CompletedTask;
        }

        schema.Properties ??= new Dictionary<string, IOpenApiSchema>();
        schema.Properties["code"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The error's code, such as tenant.not_found. Present when an endpoint refuses the request itself.",
        };
        schema.Properties["errors"] = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = new OpenApiSchema { Type = JsonSchemaType.String } },
            Description = "The messages for each field of a request that failed validation.",
        };
        schema.Properties["traceId"] = new OpenApiSchema
        {
            Type = JsonSchemaType.String,
            Description = "The trace id of the request, for the logs.",
        };
        return Task.CompletedTask;
    }
}
