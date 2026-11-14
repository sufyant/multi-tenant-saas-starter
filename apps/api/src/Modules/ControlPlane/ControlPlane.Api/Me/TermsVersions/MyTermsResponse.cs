namespace ControlPlane.Api.Me.TermsVersions;

/// <summary>What the signed-in person must do about the terms, and the version it is about; null when they must do nothing.</summary>
public sealed record MyTermsResponse(MyTermsResponseAction Action, MyTermsVersionResponse? Version);
