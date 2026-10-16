using SharedKernel;

namespace Api.ErrorHandling;

// The codes of the answers the host gives itself: authentication, authorization without a refusal, the rate limiter, idempotency,
// requests it cannot read or route, and server errors (K5).
internal static class HostErrorCodes
{
    public static readonly ErrorCode AuthUnauthenticated = ErrorCode.Declare("auth.unauthenticated");

    public static readonly ErrorCode AuthForbidden = ErrorCode.Declare("auth.forbidden");

    public static readonly ErrorCode IdempotencyKeyInvalid = ErrorCode.Declare("idempotency.key_invalid");

    public static readonly ErrorCode IdempotencyKeyInProgress = ErrorCode.Declare("idempotency.key_in_progress");

    public static readonly ErrorCode IdempotencyKeyReused = ErrorCode.Declare("idempotency.key_reused");

    public static readonly ErrorCode RateLimitExceeded = ErrorCode.Declare("rate_limit.exceeded");

    public static readonly ErrorCode RequestInvalid = ErrorCode.Declare("request.invalid");

    public static readonly ErrorCode RequestTimeout = ErrorCode.Declare("request.timeout");

    public static readonly ErrorCode RequestTooLarge = ErrorCode.Declare("request.too_large");

    public static readonly ErrorCode RequestUnsupportedMediaType = ErrorCode.Declare("request.unsupported_media_type");

    public static readonly ErrorCode RequestValidationFailed = ErrorCode.Declare("request.validation_failed");

    public static readonly ErrorCode RouteMethodNotAllowed = ErrorCode.Declare("route.method_not_allowed");

    public static readonly ErrorCode RouteNotFound = ErrorCode.Declare("route.not_found");

    public static readonly ErrorCode ServerError = ErrorCode.Declare("server.error");
}
