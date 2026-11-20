using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ControlPlane.Application.Plans;
using ControlPlane.Contracts.Usage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Tenancy;

namespace Api.IntegrationTests.Platform.Usage;

// GET /v1/platform/tenants/{tenantId}/usage (4.37, 4.42): the platform's Usage tab answers the tenant's own Usage (GetUsageTests).
public sealed class GetTenantUsageTests(Database database) : IAsyncLifetime
{
    private const string Meter = "ai.requests";

    private readonly TestData _testData = new(database);

    private ApiFactory _api = null!;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public ValueTask InitializeAsync()
    {
        _api = new ApiFactory(
            database.ConnectionStringFor(DatabaseRoles.Application),
            configureServices: services => services.Replace(ServiceDescriptor.Singleton(MeterCode.Of((Meter, "AI requests the members make.")))));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task GetTenantUsage_ForASystemAdmin_AnswersTheTenantsUse()
    {
        var tenant = await TenantWithMeterAsync();
        await UseAsync(tenant, 7_200);
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var answer = await admin.GetAsync($"/v1/platform/tenants/{tenant}/usage", Cancellation);

        answer.StatusCode.ShouldBe(HttpStatusCode.OK);
        var meter = (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("meters").EnumerateArray().Single();
        (meter.GetProperty("used").GetInt64(), meter.GetProperty("extra").GetInt64()).ShouldBe((7_200L, 0L));
    }

    [Fact]
    public async Task GetTenantUsage_OfATenantThatDoesNotExist_IsNotFound()
    {
        var admin = _api.CreateClient(await _testData.AddSystemAdminAsync(), secondFactor: true);

        var answer = await admin.GetAsync($"/v1/platform/tenants/{Guid.CreateVersion7()}/usage", Cancellation);

        (answer.StatusCode, (await answer.Content.ReadFromJsonAsync<JsonElement>(Cancellation)).GetProperty("code").GetString())
            .ShouldBe((HttpStatusCode.NotFound, "tenant.not_found"));
    }

    private async Task<Guid> TenantWithMeterAsync()
    {
        var planId = await _testData.AddPlanAsync(new());
        await _testData.SetPlanMeterAsync(planId, Meter, 10_000, "Charge", unitPrice: 100, unitSize: 1000);
        return (await _testData.AddTenantAsync(planId: planId)).Id;
    }

    private async Task UseAsync(Guid tenantId, long quantity)
    {
        using var scope = _api.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IUsageMeter>().UseAsync(tenantId, Meter, quantity, Cancellation)).ShouldBe(MeterUse.Used);
    }
}
