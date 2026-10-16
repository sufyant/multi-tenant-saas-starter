using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using SharedKernel;

namespace Api.ErrorHandling;

// Endpoints return the Result of their command, and ResultEndpointFilter maps it. The response type inferred from the
// handler is therefore Result itself, which no client ever receives. This convention replaces it with what the filter sends, so
// the OpenAPI document describes the real responses.
internal static class ResultResponses
{
    private static readonly int[] FailureStatuses =
    [
        StatusCodes.Status400BadRequest,
        StatusCodes.Status403Forbidden,
        StatusCodes.Status404NotFound,
        StatusCodes.Status409Conflict,
        StatusCodes.Status422UnprocessableEntity,
    ];

    public static TBuilder DescribeResults<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Finally(endpoint =>
        {
            var inferred = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>()
                .FirstOrDefault(response => response.Type is { } type && typeof(Result).IsAssignableFrom(type));
            if (inferred is null)
            {
                return;
            }

            // An endpoint that declares 201 keeps it in place of 200 (ResultEndpointFilter).
            endpoint.Metadata.Remove(inferred);
            var creates = endpoint.Metadata.OfType<IProducesResponseTypeMetadata>().Any(response => response.StatusCode == StatusCodes.Status201Created);
            if (!creates)
            {
                endpoint.Metadata.Add(inferred.Type!.IsGenericType
                    ? new ProducesResponseTypeMetadata(StatusCodes.Status200OK, inferred.Type.GetGenericArguments()[0], ["application/json"])
                    : new ProducesResponseTypeMetadata(StatusCodes.Status204NoContent, typeof(void)));
            }

            // Every failure is the one Problem Details schema, a 400 with the fields of a request that failed validation too (N11).
            foreach (var status in FailureStatuses)
            {
                endpoint.Metadata.Add(new ProducesResponseTypeMetadata(status, typeof(ProblemDetails), ["application/problem+json"]));
            }
        });

        return builder;
    }
}
