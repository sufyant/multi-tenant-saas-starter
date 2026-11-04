using SharedKernel;

namespace ControlPlane.Application.DataExports.DeleteStoredFile;

/// <summary>Deletes one file from the file store, after the commit that ended its use.</summary>
[MessageName("delete-stored-file")]
public sealed record DeleteStoredFileCommand(string ObjectKey);
