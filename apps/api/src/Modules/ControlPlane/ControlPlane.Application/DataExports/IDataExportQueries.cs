namespace ControlPlane.Application.DataExports;

/// <summary>The user's data exports for the queries (Q2). It declares the user in the transaction, so the own_files policy decides what is seen (R11).</summary>
/// <remarks>Public because a public handler takes it (W9).</remarks>
public interface IDataExportQueries
{
    /// <summary>The latest data export of the signed-in person with this identity provider id; none when they never asked for one.</summary>
    internal Task<DataExportView?> FindLatestAsync(string externalUserId, CancellationToken cancellationToken);
}
