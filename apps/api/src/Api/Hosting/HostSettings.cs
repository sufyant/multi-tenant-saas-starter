using Microsoft.AspNetCore.Hosting;

namespace Api.Hosting;

// The process types one build output runs as (section 1): web serves the API, worker handles messages, all does both, for local
// development and tests.
internal enum HostRole
{
    Web,
    Worker,
    All,
}

// The browser origins that may call the API (Host:Cors); none when the list is empty.
internal sealed class CorsSettings
{
    public string[] AllowedOrigins { get; set; } = [];

    // An origin is a scheme, a host and a port, exactly as a browser sends it: no wildcard, no path, no trailing slash.
    public bool AreOrigins => AllowedOrigins.All(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http"
        && uri.GetLeftPart(UriPartial.Authority) == origin);
}

// The host's own settings (section Host), checked on start.
internal sealed class HostSettings
{
    public const string Section = "Host";

    public string? Role { get; set; }

    // How long a stopping host may take to finish the work it has (Twelve-Factor IX).
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public CorsSettings Cors { get; set; } = new();

    // The port of the health endpoints (section 8). Only the platform's health check and probes reach it.
    public int ManagementPort { get; set; } = 8081;

    // The role, or null when the setting is missing or names no role; the start then fails on it.
    public HostRole? RoleOrNull => Role switch
    {
        "web" => HostRole.Web,
        "worker" => HostRole.Worker,
        "all" => HostRole.All,
        _ => null,
    };
}

internal static class HostSettingsExtensions
{
    // The settings are read while the host is composed, since the role decides what it is composed of, and checked on start like
    // every other setting: the migration step composes the host without them and never starts it.
    public static HostSettings AddHostSettings(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.Read();

        // A worker has no business endpoints, so it listens on the management port alone.
        string[] publicUrls = settings.RoleOrNull is HostRole.Worker ? [] : PublicUrls(builder.Configuration);
        var publicPorts = publicUrls.Select(url => BindingAddress.Parse(url).Port).ToArray();
        builder.WebHost.UseUrls([.. publicUrls, $"http://*:{settings.ManagementPort}"]);

        // The ports are part of the URLs now. Left set, they make the host warn on start that the URLs override them.
        builder.WebHost.UseSetting(WebHostDefaults.HttpPortsKey, "");
        builder.WebHost.UseSetting(WebHostDefaults.HttpsPortsKey, "");

        builder.Services.AddOptions<HostSettings>()
            .BindConfiguration(HostSettings.Section)
            .Validate(settings => settings.RoleOrNull is not null, $"{HostSettings.Section}:Role must be web, worker or all.")
            .Validate(settings => settings.ShutdownTimeout > TimeSpan.Zero, $"{HostSettings.Section}:ShutdownTimeout must be positive.")
            .Validate(settings => settings.Cors.AreOrigins, $"{HostSettings.Section}:Cors:AllowedOrigins must list origins such as https://app.example.com, without a wildcard or a path.")
            .Validate(settings => settings.ManagementPort is > 0 and <= ushort.MaxValue, $"{HostSettings.Section}:ManagementPort must be a port from 1 to 65535.")
            .Validate(
                settings => !publicPorts.Contains(settings.ManagementPort),
                $"{HostSettings.Section}:ManagementPort must differ from the public port, {string.Join(", ", publicPorts)}.")
            .ValidateOnStart();
        builder.Services.Configure<HostOptions>(host => host.ShutdownTimeout = settings.ShutdownTimeout);

        return settings;
    }

    // The settings as they are, before the start checks them.
    public static HostSettings Read(this IConfiguration configuration) =>
        configuration.GetSection(HostSettings.Section).Get<HostSettings>() ?? new HostSettings();

    public static bool ServesTheApi(this HostRole? role) => role is not HostRole.Worker;

    // The public addresses, read the way the web host reads them on start: ASPNETCORE_URLS, else ASPNETCORE_HTTP_PORTS and
    // ASPNETCORE_HTTPS_PORTS, else Kestrel's default. An endpoint given to Kestrel on its own would replace them all (Microsoft Learn,
    // Configure endpoints for the ASP.NET Core Kestrel web server), so the management port is added to these instead.
    private static string[] PublicUrls(ConfigurationManager configuration)
    {
        if (Split(configuration[WebHostDefaults.ServerUrlsKey]) is { Length: > 0 } urls)
        {
            return urls;
        }

        string[] ports =
        [
            .. Split(configuration[WebHostDefaults.HttpPortsKey]).Select(port => $"http://*:{port}"),
            .. Split(configuration[WebHostDefaults.HttpsPortsKey]).Select(port => $"https://*:{port}"),
        ];

        return ports is { Length: > 0 } ? ports : ["http://localhost:5000"];
    }

    private static string[] Split(string? value) => value?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];
}
