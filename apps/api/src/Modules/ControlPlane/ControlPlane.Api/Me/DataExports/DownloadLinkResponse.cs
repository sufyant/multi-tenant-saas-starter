namespace ControlPlane.Api.Me.DataExports;

/// <summary>A link that downloads the person's export, and when it stops working. The link is made for this answer and never stored.</summary>
public sealed record DownloadLinkResponse(Uri Url, DateTimeOffset ExpiresAt);
