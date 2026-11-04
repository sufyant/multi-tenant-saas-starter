namespace ControlPlane.Application.DataExports.GetDownloadLink;

/// <summary>A link that downloads a file, and when it stops working.</summary>
public sealed record DownloadLink(Uri Url, DateTimeOffset ExpiresAt);
