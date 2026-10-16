using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using SharedKernel;

namespace Api.ErrorHandling;

// Wolverine's FluentValidation middleware stops an invalid command by throwing; this turns that into a 400 listing the fields. Each field
// is named as the request's JSON names it, and lists the field code each failed rule states, never its English message, so a screen
// writes its own text (K5). A rule without a field code fails ErrorCodeTests.
internal sealed class ValidationExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validation)
        {
            return false;
        }

        var errors = validation.Errors
            .GroupBy(failure => JsonNamingPolicy.CamelCase.ConvertName(failure.PropertyName))
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => Problems.NameOf(Enum.Parse<FieldErrorCode>(failure.ErrorCode))).Distinct().ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = Problems.ForFields(errors),
            Exception = exception,
        });
    }
}
