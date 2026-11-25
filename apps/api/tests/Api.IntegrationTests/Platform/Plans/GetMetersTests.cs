using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tenancy;

namespace Api.IntegrationTests.Platform.Plans;

// GET /v1/platform/meters: every meter the code defines, the template's and the product's, so the plan editor offers a meter that no
// plan sets yet (step 41, X5).
public sealed class GetMetersTests(Database database) : IAsyncLifetime
{
    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        // The template defines no meter (step 40), so the host defines its own.
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of(("storage.gigabytes", "Gigabytes of files the organisation stores."), ("ai.requests", "AI requests the members make.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetMeters_TheCodesMeters_AnswersEachByKeyWithWhatItCounts()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var catalog = await admin.GetFromJsonAsync<JsonElement>("/v1/platform/meters", Cancellation);

        catalog.GetRawText().ShouldBe(
            """{"meters":[{"key":"ai.requests","description":"AI requests the members make."},{"key":"storage.gigabytes","description":"Gigabytes of files the organisation stores."}]}""");
    }
}
