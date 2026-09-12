using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace XNGPad.Core.Services;

public static class TcpPort
{
    public static bool IsInUse(int port)
    {
        try
        {
            var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            foreach (var ep in listeners)
            {
                if (ep.Port == port && IsLocal(ep.Address))
                    return true;
            }

            return false;
        }
        catch
        {
            try
            {
                using var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return false;
            }
            catch (SocketException)
            {
                return true;
            }
        }
    }

    private static bool IsLocal(IPAddress address)
    {
        return IPAddress.Any.Equals(address)
               || IPAddress.Loopback.Equals(address)
               || IPAddress.IPv6Any.Equals(address)
               || IPAddress.IPv6Loopback.Equals(address);
    }
}
