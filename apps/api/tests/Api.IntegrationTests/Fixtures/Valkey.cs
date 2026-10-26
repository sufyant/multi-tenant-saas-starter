using System.Text.Json;
using Testcontainers.Redis;

namespace Api.IntegrationTests.Fixtures;

// One Valkey server for a test class, from the image the cache service runs (.railway/valkey.json, InfrastructureValueTests). Valkey
// speaks the Redis protocol, so the Redis module of Testcontainers runs it.
public sealed class Valkey : IAsyncLifetime
{
    private readonly RedisContainer _container = new RedisBuilder(Image).Build();

    public static string Image =>
        JsonDocument.Parse(File.ReadAllText("valkey.json")).RootElement.GetProperty("image").GetString()
        ?? throw new InvalidOperationException("valkey.json names no image.");

    public string ConnectionString => _container.GetConnectionString();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public Task StopAsync() => _container.StopAsync();
}
