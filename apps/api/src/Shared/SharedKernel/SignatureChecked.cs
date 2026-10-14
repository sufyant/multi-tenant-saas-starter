namespace SharedKernel;

/// <summary>
/// Endpoint metadata: this webhook endpoint has no signed-in user. It proves its sender by checking the request's signature before it
/// parses the request (A5).
/// </summary>
public sealed class SignatureChecked;
