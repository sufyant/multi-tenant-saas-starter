using ControlPlane.Domain;
using SharedKernel;

namespace ControlPlane.Application.DataExports.GetMyDataExport;

// A person who never asked for an export has none: the screen then offers "Export my data".
public static class GetMyDataExportHandler
{
    public static async Task<Result<DataExportView>> HandleAsync(GetMyDataExportQuery query, IDataExportQueries exports, CancellationToken cancellationToken) =>
        await exports.FindLatestAsync(query.ActorId, cancellationToken) is { } export
            ? export
            : Error.NotFound(ControlPlaneErrorCodes.DataExportNotFound, "You have no export of your data.");
}
