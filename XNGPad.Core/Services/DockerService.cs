using System.Diagnostics;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed class DockerService
{
    public static readonly TimeSpan PullTimeout = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan RunTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan InfoTimeout = TimeSpan.FromSeconds(12);
    public static readonly TimeSpan LogsTimeout = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan ExecTimeout = TimeSpan.FromSeconds(20);
    public static readonly TimeSpan DesktopWait = TimeSpan.FromSeconds(90);

    private readonly IProcessRunner _runner;
    private string? _dockerExe;

    public DockerService(IProcessRunner runner)
    {
        _runner = runner;
    }

    public string? DockerExePath
    {
        get
        {
            if (_dockerExe != null && File.Exists(_dockerExe))
                return _dockerExe;
            _dockerExe = FindDockerExe();
            return _dockerExe;
        }
    }

    public string? DesktopExePath => FindDesktopExe();

    public bool IsInstalled() => DockerExePath != null;

    public bool DesktopPresentButCliMissing()
    {
        return FindDesktopExe() != null && DockerExePath == null;
    }

    public bool IsDesktopProcessRunning()
    {
        foreach (var name in new[] { "Docker Desktop", "com.docker.backend" })
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                    return true;
            }
            catch
            {
            }
        }

        return false;
    }

    public async Task<DaemonInfo> GetDaemonInfoAsync(CancellationToken ct = default)
    {
        if (DockerExePath == null)
            return new DaemonInfo();

        if (await DockerPipe.PingAsync(ct).ConfigureAwait(false))
            return new DaemonInfo { Running = true };

        var result = await DockerAsync(InfoTimeout, ct, "ps", "-q").ConfigureAwait(false);
        if (result.Success && !DockerError.IsDaemonDown(result.Combined))
            return new DaemonInfo { Running = true };

        return new DaemonInfo
        {
            Running = false,
            LooksLikeWslOrEngine = true,
            LooksLikeNotRunning = true
        };
    }

    public async Task<bool> IsDaemonRunningAsync(CancellationToken ct = default)
    {
        var info = await GetDaemonInfoAsync(ct).ConfigureAwait(false);
        return info.Running;
    }

    public async Task<bool> StartDesktopAsync(CancellationToken ct = default)
    {
        if (await IsDaemonRunningAsync(ct).ConfigureAwait(false))
            return true;

        if (!IsDesktopProcessRunning())
        {
            var desktop = FindDesktopExe();
            if (desktop == null)
                return false;
            _runner.StartDetached(desktop);
        }

        var deadline = DateTime.UtcNow + DesktopWait;
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (await IsDaemonRunningAsync(ct).ConfigureAwait(false))
                return true;
            await Task.Delay(2000, ct).ConfigureAwait(false);
        }

        return await IsDaemonRunningAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ImageExistsAsync(string image, CancellationToken ct = default)
    {
        var result = await DockerAsync(InfoTimeout, ct, "image", "inspect", image).ConfigureAwait(false);
        return result.Success;
    }

    public async Task PullAsync(string image, CancellationToken ct = default)
    {
        var result = await DockerAsync(PullTimeout, ct, "pull", "-q", "--platform", "linux/amd64", image)
            .ConfigureAwait(false);
        if (result.Success)
            return;
        if (await ImageExistsAsync(image, ct).ConfigureAwait(false)
            || await ImageExistsAsync("searxng/searxng:latest", ct).ConfigureAwait(false))
            return;
        ThrowIfFailed(result, "ErrorPull");
    }

    public async Task<ContainerInfo> InspectAsync(string name, CancellationToken ct = default)
    {
        var result = await DockerAsync(InfoTimeout, ct, "inspect", "-f", "{{.State.Running}}\t{{.Config.Image}}", name)
            .ConfigureAwait(false);
        if (!result.Success)
            return new ContainerInfo { Exists = false };

        var line = result.StandardOutput.Trim();
        var parts = line.Split('\t');
        var running = parts.Length > 0 && parts[0].Equals("true", StringComparison.OrdinalIgnoreCase);
        var image = parts.Length > 1 ? parts[1].Trim() : "";
        return new ContainerInfo { Exists = true, Running = running, Image = image };
    }

    public async Task RunAsync(string name, int hostPort, string image, string volumeName, CancellationToken ct = default)
    {
        var publish = $"127.0.0.1:{hostPort}:{Constants.ContainerPort}";
        var volume = $"{volumeName}:/etc/searxng";
        var result = await DockerAsync(
            RunTimeout,
            ct,
            "run",
            "-d",
            "--platform",
            "linux/amd64",
            "--name",
            name,
            "-p",
            publish,
            "-v",
            volume,
            image).ConfigureAwait(false);
        if (result.Success)
            return;

        var text = result.Combined.ToLowerInvariant();
        if (text.Contains("port is already allocated") || text.Contains("address already in use"))
            throw new PortInUseException(hostPort);
        ThrowIfFailed(result);
    }

    public async Task StartAsync(string name, CancellationToken ct = default)
    {
        var result = await DockerAsync(RunTimeout, ct, "start", name).ConfigureAwait(false);
        ThrowIfFailed(result);
    }

    public async Task StopAsync(string name, CancellationToken ct = default)
    {
        var result = await DockerAsync(RunTimeout, ct, "stop", name).ConfigureAwait(false);
        ThrowIfFailed(result);
    }

    public async Task RestartAsync(string name, CancellationToken ct = default)
    {
        var result = await DockerAsync(RunTimeout, ct, "restart", name).ConfigureAwait(false);
        ThrowIfFailed(result);
    }

    public async Task RemoveForceAsync(string name, CancellationToken ct = default)
    {
        var result = await DockerAsync(RunTimeout, ct, "rm", "-f", name).ConfigureAwait(false);
        ThrowIfFailed(result);
    }

    public async Task<string> LogsAsync(string name, int tail = 80, CancellationToken ct = default)
    {
        var result = await DockerAsync(LogsTimeout, ct, "logs", "--tail", tail.ToString(), name).ConfigureAwait(false);
        var text = result.Combined;
        return text;
    }

    public async Task<ProcessResult> ExecAsync(
        string name,
        IReadOnlyList<string> command,
        TimeSpan? timeout = null,
        string? stdin = null,
        CancellationToken ct = default)
    {
        var args = new List<string> { "exec" };
        if (stdin != null)
            args.Add("-i");
        args.Add(name);
        args.AddRange(command);
        return await DockerCli(timeout ?? ExecTimeout, ct, stdin, args).ConfigureAwait(false);
    }

    public static bool IsSearxngImage(string image)
    {
        var n = image.Trim().ToLowerInvariant().Replace('\\', '/');
        return n.Contains("searxng/searxng", StringComparison.Ordinal);
    }

    private Task<ProcessResult> DockerAsync(TimeSpan timeout, CancellationToken ct, params string[] args)
        => DockerCli(timeout, ct, null, args);

    private async Task<ProcessResult> DockerCli(
        TimeSpan timeout,
        CancellationToken ct,
        string? stdin,
        IReadOnlyList<string> args)
    {
        var exe = DockerExePath ?? throw new XNGPadException("ErrorDockerNotInstalled");
        return await _runner.RunAsync(exe, args, timeout, ct, stdin).ConfigureAwait(false);
    }

    private static void ThrowIfFailed(ProcessResult result, string fallback = "ErrorDockerCommand")
    {
        if (result.Success)
            return;
        throw new XNGPadException(DockerError.Key(result.Combined, fallback))
        {
            Detail = DockerError.ShortMessage(result.Combined)
        };
    }

    private static string? FindDockerExe()
    {
        foreach (var candidate in DockerCandidates())
        {
            try
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }

    private static string? FindDesktopExe()
    {
        foreach (var candidate in DesktopCandidates())
        {
            try
            {
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> DockerCandidates()
    {
        yield return Path.Combine(
            AppPaths.Current.ProgramFiles,
            "Docker", "Docker", "resources", "bin", "docker.exe");
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length == 0)
                continue;
            yield return Path.Combine(trimmed, "docker.exe");
        }
    }

    private static IEnumerable<string> DesktopCandidates()
    {
        yield return Path.Combine(
            AppPaths.Current.ProgramFiles,
            "Docker", "Docker", "Docker Desktop.exe");
        yield return Path.Combine(
            AppPaths.Current.ProgramFilesX86,
            "Docker", "Docker", "Docker Desktop.exe");
    }
}
