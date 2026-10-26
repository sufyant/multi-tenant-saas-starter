using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Api.ArchitectureTests;

// D1: railway.ts and the deploy scripts read the management port and the health path from .railway/api.json, and the
// Dockerfile exposes the port. The host owns both values, so a copy that drifted would fail only at the deploy's health check.
// The web apps reach the API on its public port, which the image owns (ASPNETCORE_HTTP_PORTS of its base image), so the
// Dockerfile exposes that port too.
public sealed partial class InfrastructureValueTests
{
    // The cache service and the test fixtures run one Valkey image, pinned by digest in .railway/valkey.json. A fixture that names an
    // image of its own may test another Valkey than the one the environments run.
    [Fact]
    public void CacheImage_RailwayAndTheFixtures_RunOneImagePinnedByDigest()
    {
        using var cache = JsonDocument.Parse(File.ReadAllText(Path.Combine(Solution.Root, "..", "..", ".railway", "valkey.json")));
        var fixtures = Directory.EnumerateFiles(Solution.Root, "*.cs", SearchOption.AllDirectories)
            .Where(file => ImageInCode().IsMatch(File.ReadAllText(file)));

        cache.RootElement.GetProperty("image").GetString()!.ShouldMatch(@"^valkey/valkey:[0-9]+\.[0-9]+\.[0-9]+@sha256:[0-9a-f]{64}$");
        fixtures.Select(file => Path.GetRelativePath(Solution.Root, file)).ShouldBeEmpty();
    }

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
        File.ReadLines(Path.Combine(Solution.Root, "Dockerfile")).ShouldContain($"EXPOSE {infrastructure.RootElement.GetProperty("publicPort").GetInt32()}");
    }

    [GeneratedRegex(@"RedisBuilder\(""")]
    private static partial Regex ImageInCode();
}
