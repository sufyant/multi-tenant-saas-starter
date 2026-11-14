using SharedKernel;

namespace ControlPlane.Application.Terms.GetTermsOfUse;

/// <summary>
/// Every terms version, the newest day first, each with its state and its acceptances, and how many users may accept. The versions are
/// few, one for each change of the terms, so the platform reads them whole, as a tenant's plan reads every key.
/// </summary>
[MessageName("get-terms-of-use")]
public sealed record GetTermsOfUseQuery;
