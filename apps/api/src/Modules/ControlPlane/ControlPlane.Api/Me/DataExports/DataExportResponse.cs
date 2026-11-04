namespace ControlPlane.Api.Me.DataExports;

/// <summary>
/// The person's latest data export: where it is, when they asked, when it was ready, and until when they can download it, which stays after
/// it expired, so the screen says when the file was deleted.
/// </summary>
public sealed record DataExportResponse(
    Guid Id,
    DataExportResponseStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? ReadyAt,
    DateTimeOffset? ExpiresAt,
    long? SizeBytes);
