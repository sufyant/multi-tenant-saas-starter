namespace ControlPlane.Domain.TermsVersions;

/// <summary>A terms version as the gate weighs it: whether it requires acceptance, whether its creation told the users, and when it was
/// created.</summary>
internal sealed record TermsGateVersion(Guid Id, bool RequiresAcceptance, bool SendsEmail, DateTimeOffset CreatedAt);
