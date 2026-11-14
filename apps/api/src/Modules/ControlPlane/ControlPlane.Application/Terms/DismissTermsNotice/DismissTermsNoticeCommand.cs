using SharedKernel;

namespace ControlPlane.Application.Terms.DismissTermsNotice;

/// <summary>The signed-in person closes the notice of a terms version, so it shows no more. It runs without a tenant.</summary>
[MessageName("dismiss-terms-notice")]
public sealed record DismissTermsNoticeCommand(string ActorId, Guid VersionId);
