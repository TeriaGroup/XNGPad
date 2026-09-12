using System.IO.Pipes;
using System.Text;

namespace XNGPad.Core.Services;

internal static class DockerPipe
{
    private static readonly string[] Names =
    [
        "dockerDesktopLinuxEngine",
        "docker_engine_linux",
        "docker_engine"
    ];

    public static async Task<bool> PingAsync(CancellationToken ct = default)
    {
        foreach (var name in Names)
        {
            try
            {
                using var pipe = new NamedPipeClientStream(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct);
                linked.CancelAfter(1500);
                await pipe.ConnectAsync(linked.Token).ConfigureAwait(false);
                var req = Encoding.ASCII.GetBytes("GET /_ping HTTP/1.0\r\nHost: docker\r\n\r\n");
                await pipe.WriteAsync(req, linked.Token).ConfigureAwait(false);
                await pipe.FlushAsync(linked.Token).ConfigureAwait(false);
                var buf = new byte[256];
                var n = await pipe.ReadAsync(buf, linked.Token).ConfigureAwait(false);
                var s = Encoding.ASCII.GetString(buf, 0, Math.Max(0, n));
                if (s.Contains("OK", StringComparison.OrdinalIgnoreCase) || s.Contains("200"))
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }
}
