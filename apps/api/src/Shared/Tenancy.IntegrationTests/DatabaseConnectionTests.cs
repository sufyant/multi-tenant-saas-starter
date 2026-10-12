using Npgsql;

namespace Tenancy.IntegrationTests;

public sealed class DatabaseConnectionTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void Of_AConnectionStringWithoutTheSetting_TurnsGssEncryptionOff()
    {
        var settings = new NpgsqlConnectionStringBuilder(DatabaseConnection.Of("Host=db;Username=api_application;Password=secret"));

        settings.GssEncryptionMode.ShouldBe(GssEncryptionMode.Disable);
    }

    // An operator who needs GSSAPI encryption names it in the connection string, in any spelling Npgsql reads.
    [Theory]
    [InlineData("Host=db;GSS Encryption Mode=Prefer", GssEncryptionMode.Prefer)]
    [InlineData("Host=db;GssEncryptionMode=Require", GssEncryptionMode.Require)]
    [InlineData("Host=db;gss encryption mode=disable", GssEncryptionMode.Disable)]
    public void Of_AConnectionStringThatNamesTheSetting_KeepsItsValue(string connectionString, GssEncryptionMode named)
    {
        var settings = new NpgsqlConnectionStringBuilder(DatabaseConnection.Of(connectionString));

        settings.GssEncryptionMode.ShouldBe(named);
    }

    [Fact]
    public async Task CreateDataSource_TheApplicationRole_ConnectsAsThatRole()
    {
        await using var dataSource = DatabaseConnection.CreateDataSource(database.ApplicationConnectionString);
        await using var command = dataSource.CreateCommand("SELECT current_user");

        var user = await command.ExecuteScalarAsync(Cancellation);

        user.ShouldBe(DatabaseRoles.Application);
    }
}
