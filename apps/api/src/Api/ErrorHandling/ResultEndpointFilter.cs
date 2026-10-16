using Microsoft.AspNetCore.Http.Metadata;
using SharedKernel;

namespace Api.ErrorHandling;

// Endpoints return the Result of their command; this maps it to HTTP once for every endpoint.
internal sealed class ResultEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var returned = await next(context);

        return returned is Result result ? ToHttpResult(result, Creates(context.HttpContext.GetEndpoint())) : returned;
    }

    // An endpoint that creates a resource declares 201, and its success answers 201 with the value (RFC 9110, section 15.3.2).
    private static bool Creates(Endpoint? endpoint) =>
        endpoint?.Metadata.OfType<IProducesResponseTypeMetadata>().Any(response => response.StatusCode == StatusCodes.Status201Created) == true;

    private static IResult ToHttpResult(Result result, bool creates)
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
        return creates ? TypedResults.Created((string?)null, value) : TypedResults.Ok(value);
    }
}
