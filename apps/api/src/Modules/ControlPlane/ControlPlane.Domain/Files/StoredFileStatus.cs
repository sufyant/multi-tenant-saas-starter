namespace ControlPlane.Domain.Files;

/// <summary>Where a stored file is: being prepared, ready to download, failed, or expired and deleted.</summary>
internal enum StoredFileStatus
{
    Preparing,
    Ready,
    Failed,
    Expired,
}
