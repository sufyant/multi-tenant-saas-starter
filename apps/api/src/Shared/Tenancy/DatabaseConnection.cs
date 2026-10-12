using Npgsql;

namespace Tenancy;

/// <summary>
/// The settings every PostgreSQL connection of the API gets on top of its connection string. Every data source and DbContext the
/// API opens, for requests, Wolverine and the migration step, takes its connection string from here.
/// </summary>
public static class DatabaseConnection
{
    // The builder keeps each setting a connection string names under this canonical name, whatever spelling the string used.
    private const string GssEncryptionModeKey = "GSS Encryption Mode";

    /// <summary>
    /// The connection string with GSSAPI encryption off, unless it names the setting itself. TLS protects the connection. Npgsql's
    /// default, Prefer, loads the Kerberos library, which the runtime image does not have, and the runtime prints a false error.
    /// </summary>
    public static string Of(string connectionString)
    {
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
#pragma warning disable CA1841 // Npgsql's ContainsKey answers whether it knows a keyword; Keys holds only the settings the string names.
        if (!settings.Keys.Contains(GssEncryptionModeKey))
#pragma warning restore CA1841
        {
            settings.GssEncryptionMode = GssEncryptionMode.Disable;
        }

        return settings.ConnectionString;
    }

    /// <summary>A data source for the connection string, with the settings of <see cref="Of"/>.</summary>
    public static NpgsqlDataSource CreateDataSource(string connectionString) => NpgsqlDataSource.Create(Of(connectionString));
}
