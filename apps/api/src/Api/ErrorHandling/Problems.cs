using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Serialization;
using Api.Idempotency;
using SharedKernel;

namespace Api.ErrorHandling;

// K5: every error answer is Problem Details with an ErrorCode, and a client shows its own text for the code, never the English
// detail. This is the one file that writes the code member (ErrorCodeTests). An answer from an Error carries the Error's code; any
// other answer the host writes, such as the 401 of authentication or the 429 of the rate limiter, gets the code of its status here.
internal static class Problems
{
    public const string CodeMember = "code";

    public const string ErrorsMember = "errors";

    public const string TraceIdMember = "traceId";

    /// <summary>The JSON names of every field code of a validation answer.</summary>
    public static IReadOnlyList<string> FieldCodes { get; } = [.. Enum.GetValues<FieldErrorCode>().Select(NameOf)];

    /// <summary>The answer to a failed result, or to a refusal: its status, its English detail and its code.</summary>
    public static IResult For(Error error) =>
        TypedResults.Problem(
            statusCode: StatusCodeFor(error.Type),
            detail: error.Description,
            extensions: new Dictionary<string, object?> { [CodeMember] = error.Code.Value });

    /// <summary>The answer to a request whose fields failed validation: the field codes for each field, by its JSON name.</summary>
    public static HttpValidationProblemDetails ForFields(IDictionary<string, string[]> errors) =>
        new(errors) { Status = StatusCodes.Status400BadRequest, Extensions = { [CodeMember] = HostErrorCodes.RequestValidationFailed.Value } };

    // Every Problem Details the host writes passes here, from a result, the status code pages or the exception handler. One without a
    // code gets the code of its status. Each gets the trace id; a replayed idempotent answer keeps the first request's (I5).
    public static void Describe(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        problem.Extensions.TryAdd(CodeMember, CodeOf(problem.Status ?? context.HttpContext.Response.StatusCode).Value);
        problem.Extensions[TraceIdMember] =
            context.HttpContext.Items[IdempotencyMiddleware.TraceItem] ?? Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
    }

    public static string NameOf<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        typeof(TEnum).GetField(value.ToString())!.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()!.Name;

    private static int StatusCodeFor(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.Unprocessable => StatusCodes.Status422UnprocessableEntity,
        ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Every error type maps to a status code."),
    };

    // The answers no endpoint gave: a request the router, the server, authentication, authorization or the rate limiter refused, and a
    // server error. A 401 says nothing about why the token failed (API8).
    private static ErrorCode CodeOf(int status) => status switch
    {
        StatusCodes.Status401Unauthorized => HostErrorCodes.AuthUnauthenticated,
        StatusCodes.Status403Forbidden => HostErrorCodes.AuthForbidden,
        StatusCodes.Status404NotFound => HostErrorCodes.RouteNotFound,
        StatusCodes.Status405MethodNotAllowed => HostErrorCodes.RouteMethodNotAllowed,
        StatusCodes.Status408RequestTimeout => HostErrorCodes.RequestTimeout,
        StatusCodes.Status413PayloadTooLarge => HostErrorCodes.RequestTooLarge,
        StatusCodes.Status415UnsupportedMediaType => HostErrorCodes.RequestUnsupportedMediaType,
        StatusCodes.Status429TooManyRequests => HostErrorCodes.RateLimitExceeded,
        < StatusCodes.Status500InternalServerError => HostErrorCodes.RequestInvalid,
        _ => HostErrorCodes.ServerError,
    };
}
