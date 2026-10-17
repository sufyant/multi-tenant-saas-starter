using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;

namespace Api.IntegrationTests.Fixtures;

// The public port and the management port of a host that runs on Kestrel. The ports come from 20000 to 32767, below the ephemeral
// range of Linux (32768 to 60999) and of macOS and Windows (49152 to 65535), so no outbound connection takes one between the check and
// the host's bind. Each process starts at a random place in the range and takes the next port each time, so it never gives a port
// twice, and two test processes seldom meet. A port is given only if Kestrel could bind it on all interfaces a moment ago, as the host
// binds its management port; a port that another socket holds is passed over before any host uses it.
internal sealed record HostPorts(int Public, int Management)
{
    private const int First = 20000;

    private const int Count = 12768;

    private static int _next = Random.Shared.Next(Count);

    public static HostPorts Free() => new(Next(), Next());

    // The host takes its public address from ASPNETCORE_URLS, which the urls setting stands for, and its management port from its own
    // setting.
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["urls"] = $"http://127.0.0.1:{Public}",
        ["Host:ManagementPort"] = Management.ToString(CultureInfo.InvariantCulture),
    };

    private static int Next()
    {
        for (var tried = 0; tried < Count; tried++)
        {
            var port = First + Interlocked.Increment(ref _next) % Count;
            if (CanBind(port))
            {
                return port;
            }
        }

        throw new InvalidOperationException($"No port from {First} to {First + Count - 1} is free.");
    }

    private static bool CanBind(int port)
    {
        try
        {
            using var socket = SocketTransportOptions.CreateDefaultBoundListenSocket(new IPEndPoint(IPAddress.IPv6Any, port));
            return true;
        }
        catch (SocketException exception) when (exception.SocketErrorCode == SocketError.AddressAlreadyInUse)
        {
            return false;
        }
    }
}
