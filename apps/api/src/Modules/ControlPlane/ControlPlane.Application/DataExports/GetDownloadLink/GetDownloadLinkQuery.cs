using SharedKernel;

namespace ControlPlane.Application.DataExports.GetDownloadLink;

/// <summary>A link that downloads a file the endpoint has checked, for five minutes. The link is never stored.</summary>
[MessageName("get-download-link")]
public sealed record GetDownloadLinkQuery(string ObjectKey, string FileName);
