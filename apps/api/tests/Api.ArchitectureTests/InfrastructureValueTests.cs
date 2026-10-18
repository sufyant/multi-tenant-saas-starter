using System.Reflection;
using System.Text.Json;

namespace Api.ArchitectureTests;

// Audit B15: railway.ts and the deploy scripts read the management port and the health path from .railway/api.json, and the
// Dockerfile exposes the port. The host owns both values, so a copy that drifted would fail only at the deploy's health check.
public class InfrastructureValueTests
{
    [Fact]
    public void RailwayInfrastructure_PortAndHealthPath_AreTheHostsOwn()
    {
        using var infrastructure = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, "..", "..", ".railway", "api.json")));
        var host = Solution.Load(Solution.Host);
        var options = Activator.CreateInstance(host.GetType("Api.Hosting.HostingOptions", throwOnError: true)!, nonPublic: true)!;
        var managementPort = (int)options.GetType().GetProperty("ManagementPort")!.GetValue(options)!;
        var healthPath = (string)host.GetType("Api.ApiPipeline", throwOnError: true)!
            .GetField("HealthPath", BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

        infrastructure.RootElement.GetProperty("managementPort").GetInt32().ShouldBe(managementPort);
        infrastructure.RootElement.GetProperty("healthPath").GetString().ShouldBe($"{healthPath}/ready");
        File.ReadLines(Path.Combine(Solution.Root, "Dockerfile")).ShouldContain($"EXPOSE {managementPort}");
    }
}
