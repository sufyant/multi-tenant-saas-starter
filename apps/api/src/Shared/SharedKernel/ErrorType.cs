namespace SharedKernel;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Forbidden,

    /// <summary>The request is well formed but cannot be carried out as it stands, such as an idempotency key reused for another request.</summary>
    Unprocessable,

    /// <summary>The caller has used up a limit for now, such as a tenant's invitations of the last 24 hours (RFC 6585, 429).</summary>
    TooManyRequests,
}
