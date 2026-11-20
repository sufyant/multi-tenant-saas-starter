using SharedKernel;

namespace ControlPlane.Application.Users.SignUp;

/// <summary>
/// A person who signed up at the identity provider becomes a user (step 40d, 1.27). The verified email addresses are read from the identity
/// provider before the transaction begins, so no call leaves the process while the user is locked. The new user gets the first verified
/// address, no name and the language given. A person who ticked the terms gives the version in force, and accepts it in the same
/// transaction. A known user is answered as they are: sign-up sets a state.
/// </summary>
/// <param name="TermsVersionId">The terms version in force that the person accepts with the sign-up; none when they accept no terms.</param>
[MessageName("sign-up")]
public sealed record SignUpCommand(string ActorId, IReadOnlyList<string> VerifiedEmails, Language Language, Guid? TermsVersionId = null);
