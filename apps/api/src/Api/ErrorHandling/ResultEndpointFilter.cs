using Microsoft.AspNetCore.Http.Metadata;
using SharedKernel;

namespace Api.ErrorHandling;

// Endpoints return the Result of their command; this maps it to HTTP once for every endpoint.
internal sealed class ResultEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var returned = await next(context);
        var endpoint = context.HttpContext.GetEndpoint();

        return returned is Result result ? ToHttpResult(result, Creates(endpoint), LocationOf(context.HttpContext, endpoint)) : returned;
    }

    // An endpoint that creates a resource declares 201, and its success answers 201 with the value (RFC 9110, section 15.3.2). Where a
    // GET reads the new resource, the endpoint names that GET, and the 201 sends the address in Location; a replay sends the same (I5).
    private static bool Creates(Endpoint? endpoint) =>
        endpoint?.Metadata.OfType<IProducesResponseTypeMetadata>().Any(response => response.StatusCode == StatusCodes.Status201Created) == true;

    // The address comes from the route of the endpoint that reads the resource, so no endpoint writes a route a second time.
    private static Func<object, string>? LocationOf(HttpContext context, Endpoint? endpoint) =>
        endpoint?.Metadata.GetMetadata<CreatedLocation>() is { } location
            ? created => context.RequestServices.GetRequiredService<LinkGenerator>()
                .GetPathByName(context, location.EndpointName, location.RouteValuesFor(created))
                ?? throw new InvalidOperationException($"No endpoint named {location.EndpointName} builds the Location.")
            : null;

    private static IResult ToHttpResult(Result result, bool creates, Func<object, string>? location)
    {
        if (!result.IsSuccess)
        {
            return Problems.For(result.Error);
        }

        // Result<T> is generic, and the filter runs for endpoints of every T.
        var type = result.GetType();
        if (!type.IsGenericType)
        {
            return TypedResults.NoContent();
        }

        var value = type.GetProperty(nameof(Result<object>.Value))!.GetValue(result);
        return creates ? TypedResults.Created(location?.Invoke(value!), value) : TypedResults.Ok(value);
    }
}
