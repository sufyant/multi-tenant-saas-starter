using System.Text.Json;
using Testcontainers.Redis;

namespace Api.IntegrationTests.Fixtures;

// One Valkey server for a test class, from the image the cache service runs (.railway/valkey.json, InfrastructureValueTests). Valkey
// speaks the Redis protocol, so the Redis module of Testcontainers runs it.
// A test may start one with arguments of its own, such as a small memory limit.
public sealed class Valkey : IAsyncLifetime
{
    private readonly RedisContainer _container;

    public Valkey()
        : this([])
    {
    }

    private Valkey(string[] arguments) => _container = new RedisBuilder(Image).WithCommand(arguments).Build();

    public static string Image =>
        JsonDocument.Parse(File.ReadAllText("valkey.json")).RootElement.GetProperty("image").GetString()
        ?? throw new InvalidOperationException("valkey.json names no image.");

    public string ConnectionString => _container.GetConnectionString();

    public static Valkey WithArguments(params string[] arguments) => new(arguments);

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public Task StopAsync() => _container.StopAsync();

    // A paused server keeps its connections open and answers nothing: a slow Valkey.
    public Task PauseAsync() => _container.PauseAsync();

    public Task UnpauseAsync() => _container.UnpauseAsync();
}
