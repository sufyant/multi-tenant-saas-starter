using Npgsql;

namespace Tenancy.IntegrationTests;

// Every data source the API opens comes from DatabaseConnection, so these tests open one on the real server.
public sealed class DatabaseConnectionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task OpenDataSource_WithoutTheGssSetting_ConnectsWithGssEncryptionOff()
    {
        await using var dataSource = DatabaseConnection.CreateDataSource(database.ApplicationConnectionString);
        await using var command = dataSource.CreateCommand("SELECT current_user");

        var user = await command.ExecuteScalarAsync(Cancellation);

        user.ShouldBe(DatabaseRoles.Application);
        new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).GssEncryptionMode.ShouldBe(GssEncryptionMode.Disable);
    }

    // An operator who needs GSSAPI encryption names it in the connection string, in any spelling Npgsql reads.
    [Theory]
    [InlineData("GSS Encryption Mode=Prefer", GssEncryptionMode.Prefer)]
    [InlineData("GssEncryptionMode=Require", GssEncryptionMode.Require)]
    [InlineData("gss encryption mode=disable", GssEncryptionMode.Disable)]
    public async Task OpenDataSource_ThatNamesTheGssSetting_KeepsItsValue(string setting, GssEncryptionMode named)
    {
        await using var dataSource = DatabaseConnection.CreateDataSource($"{database.ApplicationConnectionString};{setting}");

        new NpgsqlConnectionStringBuilder(dataSource.ConnectionString).GssEncryptionMode.ShouldBe(named);
    }
}
