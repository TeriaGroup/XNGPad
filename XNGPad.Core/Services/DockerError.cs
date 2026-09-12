namespace XNGPad.Core.Services;

public static class DockerError
{
    public static bool IsDaemonDown(string text)
    {
        var t = text.ToLowerInvariant();
        return t.Contains("dockerdesktoplinuxengine")
               || t.Contains("cannot connect")
               || t.Contains("error during connect")
               || t.Contains("open //./pipe")
               || t.Contains("npipe:")
               || t.Contains("is the docker daemon running")
               || t.Contains("docker desktop is not running");
    }

    public static string Key(string combined, string fallback = "ErrorDockerCommand")
    {
        if (string.IsNullOrWhiteSpace(combined))
            return fallback;
        var t = combined.ToLowerInvariant();
        if (t.Contains("timed out."))
            return "ErrorDockerTimeout";
        if (IsDaemonDown(t))
            return "ErrorWsl";
        if (t.Contains("no space") || t.Contains("not enough disk"))
            return "ErrorDockerDisk";
        if (t.Contains("i/o timeout")
            || t.Contains("tls handshake")
            || t.Contains("network is unreachable")
            || t.Contains("temporary failure")
            || (t.Contains("pull") && (t.Contains("denied") || t.Contains("timeout") || t.Contains("connection"))))
            return "ErrorPull";
        return fallback;
    }

    public static string ShortMessage(string combined)
    {
        if (string.IsNullOrWhiteSpace(combined))
            return "";
        var lines = combined.Replace('\r', '\n').Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            var line = lines[i];
            if (line.Length < 8)
                continue;
            if (line.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.StartsWith("latest:", StringComparison.OrdinalIgnoreCase))
                continue;
            if (line.Length > 180)
                line = line[..177] + "…";
            return line;
        }

        return "";
    }
}
