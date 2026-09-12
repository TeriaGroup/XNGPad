using System.Diagnostics;
using System.Text.Json;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed class InstallService
{
    public static readonly TimeSpan InstallTimeout = TimeSpan.FromMinutes(15);

    private readonly IProcessRunner _runner;
    private readonly HttpClient _http;

    public InstallService(IProcessRunner runner, HttpClient http)
    {
        _runner = runner;
        _http = http;
    }

    public Task<IReadOnlyList<McpDef>> LoadRegistryAsync(string cachePath, CancellationToken ct)
        => McpRegistry.LoadAsync(_http, cachePath, ct);

    public Task<Dictionary<string, string>> TranslateHintsToRuAsync(
        IEnumerable<string> texts,
        string cachePath,
        CancellationToken ct,
        IProgress<Dictionary<string, string>>? progress = null)
        => McpRegistry.TranslateToRuAsync(_http, texts, cachePath, ct, progress);

    public bool IsNodeInstalled() => FindNpm() != null;

    public bool IsOpenCodeInstalled() => FindOpenCode() != null;

    public async Task InstallNodeAsync(CancellationToken ct = default)
    {
        if (IsNodeInstalled())
            return;

        var winget = FindWinget();
        if (winget != null)
        {
            var result = await _runner.RunAsync(
                winget,
                ["install", "-e", "--id", "OpenJS.NodeJS.LTS", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
                InstallTimeout,
                ct).ConfigureAwait(false);
            if (IsNodeInstalled() || result.Success)
                return;
        }

        var msi = await DownloadNodeMsiAsync(ct).ConfigureAwait(false);
        var msiexec = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
        var resultMsi = await _runner.RunAsync(
            msiexec,
            ["/i", msi, "/passive", "/norestart"],
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (IsNodeInstalled() || resultMsi.ExitCode is 0 or 3010)
            return;
        throw new XNGPadException("ErrorInstallFailed") { Detail = DockerError.ShortMessage(resultMsi.Combined) };
    }

    public async Task InstallDockerAsync(CancellationToken ct = default)
    {
        var winget = FindWinget();
        if (winget != null)
        {
            var result = await _runner.RunAsync(
                winget,
                ["install", "-e", "--id", "Docker.DockerDesktop", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
                InstallTimeout,
                ct).ConfigureAwait(false);
            if (result.Success)
                return;
        }

        var dest = Path.Combine(Path.GetTempPath(), "DockerDesktopInstaller.exe");
        await DownloadAsync(Constants.DockerInstallerUrl, dest, ct).ConfigureAwait(false);
        _runner.StartDetached(dest);
    }

    public async Task InstallOpenCodeAsync(CancellationToken ct = default)
    {
        if (IsOpenCodeInstalled())
            return;
        if (!IsNodeInstalled())
            await InstallNodeAsync(ct).ConfigureAwait(false);

        var npm = FindNpm() ?? throw new XNGPadException("ErrorNodeMissing");
        var cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var result = await _runner.RunAsync(
            cmd,
            ["/c", npm, "install", "-g", "opencode-ai"],
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (!IsOpenCodeInstalled() && !result.Success)
            throw new XNGPadException("ErrorInstallFailed") { Detail = DockerError.ShortMessage(result.Combined) };
    }

    public static string? FindWinget()
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "winget.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WindowsApps", "winget.exe")
        };
        foreach (var p in candidates.Concat(OnPath("winget.exe")))
        {
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    public static string? FindNpm()
    {
        foreach (var dir in ToolDirs())
        {
            foreach (var name in new[] { "npm.cmd", "npm.exe" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p))
                    return p;
            }
        }

        return null;
    }

    public static string? FindOpenCode()
    {
        foreach (var dir in OpenCodeDirs())
        {
            foreach (var name in new[] { "OpenCode.exe", "opencode.exe", "opencode.cmd", "opencode" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p))
                    return p;
            }
        }

        try
        {
            var procs = Process.GetProcessesByName("OpenCode");
            if (procs.Length > 0)
            {
                var path = procs[0].MainModule?.FileName;
                foreach (var p in procs)
                    p.Dispose();
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    return path;
                return "OpenCode";
            }
        }
        catch
        {
        }

        return null;
    }

    private static IEnumerable<string> OpenCodeDirs()
    {
        var localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        var dirs = new List<string>
        {
            Path.Combine(localPrograms, "@opencode-aidesktop"),
            Path.Combine(localPrograms, "opencode"),
            Path.Combine(localPrograms, "OpenCode")
        };
        try
        {
            if (Directory.Exists(localPrograms))
                dirs.AddRange(Directory.EnumerateDirectories(localPrograms, "*opencode*"));
        }
        catch
        {
        }

        dirs.AddRange(ToolDirs());
        return dirs;
    }

    private async Task<string> DownloadNodeMsiAsync(CancellationToken ct)
    {
        var json = await _http.GetStringAsync("https://nodejs.org/dist/index.json", ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        string? version = null;
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("lts", out var lts))
                continue;
            if (lts.ValueKind is JsonValueKind.False or JsonValueKind.Null)
                continue;
            version = item.GetProperty("version").GetString();
            break;
        }

        if (string.IsNullOrWhiteSpace(version))
            throw new XNGPadException("ErrorInstallFailed");
        var url = $"https://nodejs.org/dist/{version}/node-{version}-x64.msi";
        var dest = Path.Combine(Path.GetTempPath(), $"node-{version}-x64.msi");
        await DownloadAsync(url, dest, ct).ConfigureAwait(false);
        return dest;
    }

    private async Task DownloadAsync(string url, string dest, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        await using var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var output = File.Create(dest);
        await input.CopyToAsync(output, ct).ConfigureAwait(false);
    }

    private static IEnumerable<string> ToolDirs()
    {
        var extra = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs")
        };
        foreach (var dir in extra.Concat(PathDirs()))
        {
            if (!string.IsNullOrWhiteSpace(dir))
                yield return dir;
        }
    }

    private static IEnumerable<string> PathDirs()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }

    private static IEnumerable<string> OnPath(string file)
    {
        foreach (var dir in PathDirs())
            yield return Path.Combine(dir, file);
    }
}
