namespace ControlPlane.Api.Platform.Users;

/// <summary>
/// Whether the user accepted the terms: the version in force, null before the first version takes effect, and the scheduled version, null
/// when none is scheduled or it requires no acceptance.
/// </summary>
public sealed record UserTermsResponse(UserTermsVersionResponse? InForce, UserTermsVersionResponse? Scheduled);
