namespace Audit.Api.Platform.AuditLogs;

/// <summary>How long the platform's log keeps an entry, in days: the deployment setting, never unlimited.</summary>
public sealed record PlatformAuditRetentionResponse(int Days);
