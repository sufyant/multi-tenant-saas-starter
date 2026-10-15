using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.OpenApi;

namespace Api.Hosting;

// N11: the OpenAPI document is the contract every client is generated from. These transformers add what a generated client needs and
// the endpoints do not state: an operation id for each endpoint, the parameters a route group puts in a path, the values of each
// status, and the one error schema. They change the document only; the JSON on the wire stays as it is.
internal static class OpenApiContract
{
    private const string DocumentName = "v1";

    public static OpenApiOptions DescribeContract(this OpenApiOptions options) =>
        options.AddOperationTransformer(NameOperation).AddOperationTransformer(DeclarePathParameters).AddSchemaTransformer(DescribeProblemDetails);

    // N7: a status in a response is one lowercase word. The module owns the list of values; the document lists them as an enum on
    // the Status property of each response type.
    public static IServiceCollection DescribeResponseStatuses(this IServiceCollection services, IReadOnlyList<string> statuses) =>
        services.Configure<OpenApiOptions>(DocumentName, options => options.AddSchemaTransformer((schema, context, _) =>
        {
            if (context.JsonPropertyInfo is { Name: "status" } property
                && property.PropertyType == typeof(string)
                && property.DeclaringType.Name.EndsWith("Response", StringComparison.Ordinal))
            {
                schema.Enum = [.. statuses.Select(status => (JsonNode)JsonValue.Create(status))];
            }

            return Task.CompletedTask;
        }));

    // N7: an endpoint is a method with the action's name, so the method's name is the operation id.
    private static Task NameOperation(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        operation.OperationId ??= context.Description.ActionDescriptor.EndpointMetadata.OfType<MethodInfo>().FirstOrDefault()?.Name;
        return Task.CompletedTask;
    }

    // OpenAPI 3.1, "Path Templating": each parameter in a path is a declared path parameter. A route group's parameter, such as the
    // tenant's {tenantId} (T1), reaches no endpoint method, so the document would leave it out. The description's path has no route
    // constraints, so the parameter is a plain string.
    private static Task DeclarePathParameters(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var path = RoutePatternFactory.Parse(context.Description.RelativePath!);
        foreach (var parameter in path.Parameters.Where(parameter => operation.Parameters?.Any(declared => declared.Name == parameter.Name) != true))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = parameter.Name,
                In = ParameterLocation.Path,
                Required = true,
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }

        return Task.CompletedTask;
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
