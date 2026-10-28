using System.Diagnostics.CodeAnalysis;

namespace SharedKernel;

/// <summary>
/// An expected failure: a business outcome the caller handles, not an exception. The code is one of the API's error codes (K5); the
/// description is English, for developers and logs, and a client never shows it.
/// </summary>
[SuppressMessage("Naming", "CA1716", Justification = "Error is a keyword only in Visual Basic, which no consumer of the API uses.")]
public sealed record Error(ErrorCode Code, string Description, ErrorType Type)
{
    /// <summary>The tenants the error is about, when a client needs them to act, such as the tenants a user is the last Owner of.</summary>
    public IReadOnlyList<Guid>? TenantIds { get; init; }

    /// <summary>The entitlement key the error is about, such as the feature that is off or the limit that is reached.</summary>
    public string? Entitlement { get; init; }

    public static Error Validation(ErrorCode code, string description) => new(code, description, ErrorType.Validation);

    public static Error NotFound(ErrorCode code, string description) => new(code, description, ErrorType.NotFound);

    public static Error Conflict(ErrorCode code, string description) => new(code, description, ErrorType.Conflict);

    public static Error Forbidden(ErrorCode code, string description) => new(code, description, ErrorType.Forbidden);

    public static Error Unprocessable(ErrorCode code, string description) => new(code, description, ErrorType.Unprocessable);

    public static Error TooManyRequests(ErrorCode code, string description) => new(code, description, ErrorType.TooManyRequests);
}
