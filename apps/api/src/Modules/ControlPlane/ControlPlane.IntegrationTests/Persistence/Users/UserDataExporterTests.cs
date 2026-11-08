using ControlPlane.Infrastructure.Persistence.Users;
using Microsoft.Extensions.DependencyInjection;
using SharedKernel;
using Database = ControlPlane.IntegrationTests.Fixtures.Database;

namespace ControlPlane.IntegrationTests.Persistence.Users;

// ControlPlane's part of a person's data export (docs/flows/personal-data-export.md). The reason of a user's suspension is personal data
// about the person, so their profile holds it (GDPR, Article 15; the maintainer's decision of 2026-11-08).
public sealed class UserDataExporterTests(Database database)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ExportPersonalData_ASuspendedUser_HoldsTheReasonInTheProfile()
    {
        var user = await TestData.AddUserAsync(database.Services);
        await database.ScalarAsSuperuserAsync<object>(
            $"UPDATE control_plane.users SET suspended_at = now(), suspended_by = id, suspension_reason = 'Shared account' WHERE id = '{user.Id}'");

        var parts = await ExportAsync(user.Id);

        System.Text.Json.JsonSerializer.Serialize(parts.Single(part => part.Name == "profile").Content, System.Text.Json.JsonSerializerOptions.Web).ShouldContain("\"suspensionReason\":\"Shared account\"");
    }

    [Fact]
    public async Task ExportPersonalData_AUserNotSuspended_HoldsNoReasonInTheProfile()
    {
        var user = await TestData.AddUserAsync(database.Services);

        var parts = await ExportAsync(user.Id);

        System.Text.Json.JsonSerializer.Serialize(parts.Single(part => part.Name == "profile").Content, System.Text.Json.JsonSerializerOptions.Web).ShouldContain("\"suspensionReason\":null");
    }

    private async Task<IReadOnlyList<PersonalDataPart>> ExportAsync(Guid userId)
    {
        await using var scope = database.Services.CreateAsyncScope();
        var exporter = scope.ServiceProvider.GetServices<IPersonalDataExporter>().OfType<UserDataExporter>().Single();
        return await exporter.ExportAsync(userId, Cancellation);
    }
}
