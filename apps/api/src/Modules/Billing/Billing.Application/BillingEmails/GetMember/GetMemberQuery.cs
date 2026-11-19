using SharedKernel;

namespace Billing.Application.BillingEmails.GetMember;

/// <summary>The user id of the signed-in member, by the identity provider's user id, which ControlPlane's contracts read (W1).</summary>
[MessageName("get-billing-member")]
public sealed record GetMemberQuery(string ExternalUserId);
