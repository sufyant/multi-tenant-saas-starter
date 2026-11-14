using SharedKernel;

namespace ControlPlane.Application.TermsVersions.CreateTermsVersion;

/// <summary>
/// A system admin creates a terms version: the day it takes effect, the link to its text, an optional summary, and whether a person must
/// accept it, and whether its creation emails the users. One version may be scheduled at a time.
/// </summary>
[MessageName("create-terms-version")]
public sealed record CreateTermsVersionCommand(string ActorId, DateOnly EffectiveDate, string Url, string? Summary, bool RequiresAcceptance, bool SendsEmail = true);
