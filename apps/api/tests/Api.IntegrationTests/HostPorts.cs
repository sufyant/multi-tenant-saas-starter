using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Api.IntegrationTests;

// The public port and the management port of a host that runs on Kestrel. Both were free a moment ago; the two sockets that found
// them are open together, so the two ports differ.
internal sealed record HostPorts(int Public, int Management)
{
    public static HostPorts Free()
    {
        using var first = Bind();
        using var second = Bind();

        return new(PortOf(first), PortOf(second));
    }

    // The host takes its public address from ASPNETCORE_URLS, which the urls setting stands for, and its management port from its own
    // setting.
    public IReadOnlyDictionary<string, string?> Settings => new Dictionary<string, string?>
    {
        ["urls"] = $"http://127.0.0.1:{Public}",
        ["Host:ManagementPort"] = Management.ToString(CultureInfo.InvariantCulture),
    };

    private static Socket Bind()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return socket;
    }

    private static int PortOf(Socket socket) => ((IPEndPoint)socket.LocalEndPoint!).Port;
}
