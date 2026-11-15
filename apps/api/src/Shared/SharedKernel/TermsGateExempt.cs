namespace SharedKernel;

/// <summary>
/// Endpoint metadata: a person who must accept a terms version may still call this me endpoint (docs/flows/terms-of-use.md, section 8).
/// Only the flow's exempt list carries it: the terms themselves, the person's own account and their data, and Accept invitation's own
/// path. Every other me and tenant endpoint refuses such a person in one place, the host's access check.
/// </summary>
public sealed class TermsGateExempt;
