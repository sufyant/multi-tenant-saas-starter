namespace SharedKernel;

/// <summary>
/// Endpoint metadata: this operation needs a second factor verified within the reverification window, not only one verified in the
/// session (ARCHITECTURE.md, A6). The host checks it before the endpoint runs, so before an idempotency key is claimed.
/// </summary>
public sealed class ReverificationRequired;
