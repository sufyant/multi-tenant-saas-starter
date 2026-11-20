using System.Text.Json.Serialization;
using SharedKernel;

namespace ControlPlane.Api.Me.Users;

/// <summary>
/// The language the person chose on the sign-up page, one of the list, required; and the id of the terms version in force that the person
/// accepts with the sign-up, or null. Another version answers 409 <c>terms_version.changed</c>, and an id no version has 404
/// <c>terms_version.not_found</c>.
/// </summary>
public sealed record SignUpRequest([property: JsonRequired] Language Language, Guid? TermsVersionId = null);
