namespace ControlPlane.Infrastructure.Persistence.TermsVersions;

/// <summary>
/// What the cache keeps of the terms for one person's gate (C4, <see cref="CachedReads.UserTerms"/>): whether they are a user, and each
/// version's rows with whether they accepted it. The state of a version is never stored: the gate weighs the rows on the day of each read.
/// </summary>
internal sealed record TermsGateRows(bool IsUser, TermsGateVersionRow[] Versions);
