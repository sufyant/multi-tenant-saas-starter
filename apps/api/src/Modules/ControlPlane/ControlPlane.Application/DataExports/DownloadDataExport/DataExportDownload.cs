namespace ControlPlane.Application.DataExports.DownloadDataExport;

/// <summary>The file of a ready export that the person may download: its key in the file store and the name the download gives it.</summary>
public sealed record DataExportDownload(string ObjectKey, string FileName);
