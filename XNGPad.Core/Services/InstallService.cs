using System.Diagnostics;
using System.IO.Compression;
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
            if (IsNodeInstalled())
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

    public static IReadOnlyList<string> PythonCommand()
    {
        var python = FindPython();
        if (python != null)
            return [python];
        var py = FindPyLauncher();
        if (py != null)
            return [py, "-3"];
        return ["python"];
    }

    public static string? FindPython()
    {
        foreach (var dir in PythonDirs())
        {
            foreach (var name in new[] { "python.exe", "python3.exe" })
            {
                var p = Path.Combine(dir, name);
                if (File.Exists(p))
                    return p;
            }
        }

        return null;
    }

    public static string? FindPyLauncher()
    {
        foreach (var dir in ToolDirs())
        {
            var p = Path.Combine(dir, "py.exe");
            if (File.Exists(p))
                return p;
        }

        return null;
    }

    public async Task EnsureOfficeMcpAsync(CancellationToken ct = default)
    {
        await EnsurePythonAsync(ct).ConfigureAwait(false);
        var cmd = PythonCommand().ToList();
        cmd.AddRange(["-m", "pip", "install", "--user", "mcp-office"]);
        var pip = await _runner.RunAsync(cmd[0], cmd.Skip(1).ToList(), InstallTimeout, ct).ConfigureAwait(false);
        if (!pip.Success)
            throw new XNGPadException("ErrorInstallFailed") { Detail = DockerError.ShortMessage(pip.Combined) };
    }

    public const string MatlabMcpDownloadUrl =
        "https://github.com/matlab/matlab-mcp-server/releases/latest/download/matlab-mcp-server-windows-x64.exe";

    public const long MatlabMcpMinBytes = 15_000_000;

    public bool IsMatlabInstalled(string? savedDir = null) => McpCatalog.FindMatlabInstall(savedDir) != null;

    public bool IsMatlabMcpInstalled() =>
        File.Exists(McpCatalog.MatlabExePath()) && McpCatalog.IsMatlabMcpToolboxInstalled();

    public async Task EnsureMatlabMcpAsync(CancellationToken ct = default)
    {
        var exe = McpCatalog.MatlabExePath();
        if (File.Exists(exe) && new FileInfo(exe).Length >= MatlabMcpMinBytes)
            return;
        TryDelete(exe);
        var dir = Path.GetDirectoryName(exe)!;
        Directory.CreateDirectory(dir);
        await DownloadAsync(MatlabMcpDownloadUrl, exe, ct).ConfigureAwait(false);
        if (!File.Exists(exe))
            throw new XNGPadException("ErrorMatlabDownload");
    }

    public async Task InstallMatlabMcpAsync(string? matlabRoot, CancellationToken ct = default)
    {
        var root = McpCatalog.FindMatlabInstall(matlabRoot);
        if (root == null)
            throw new XNGPadException("ErrorMatlabMissing");

        await EnsureMatlabMcpAsync(ct).ConfigureAwait(false);
        if (McpCatalog.IsMatlabMcpToolboxInstalled())
            return;

        var result = await _runner.RunAsync(
            McpCatalog.MatlabExePath(),
            McpCatalog.MatlabSetupArgs(root),
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (!result.Success || !McpCatalog.IsMatlabMcpToolboxInstalled())
            throw new XNGPadException("ErrorMatlabMcpSetup") { Detail = DockerError.ShortMessage(result.Combined) };
    }

    public const string MermaidPackage = "mcp-mermaid@0.4.1";

    public const string MermaidChromiumUrl =
        "https://cdn.playwright.dev/builds/cft/153.0.8010.12/win64/chrome-win64.zip";

    public const string MermaidHeadlessUrl =
        "https://cdn.playwright.dev/builds/cft/153.0.8010.12/win64/chrome-headless-shell-win64.zip";

    public bool IsMermaidInstalled() => File.Exists(McpCatalog.MermaidEntry()) && IsMermaidChromiumInstalled();

    public static bool IsMermaidChromiumInstalled() =>
        BrowserReady(MermaidChromiumDir(), Path.Combine("chrome-win64", "chrome.exe"))
        && BrowserReady(MermaidHeadlessDir(), Path.Combine("chrome-headless-shell-win64", "chrome-headless-shell.exe"));

    public static string MermaidChromiumDir() => PlaywrightBrowserDir("chromium-1243");

    public static string MermaidHeadlessDir() => PlaywrightBrowserDir("chromium_headless_shell-1243");

    private static string PlaywrightBrowserDir(string name) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ms-playwright",
        name);

    private static bool BrowserReady(string dir, string relativeExe) =>
        File.Exists(Path.Combine(dir, relativeExe))
        && File.Exists(Path.Combine(dir, "INSTALLATION_COMPLETE"));

    public async Task EnsureMermaidMcpAsync(CancellationToken ct = default)
    {
        if (!File.Exists(McpCatalog.MermaidEntry()))
        {
            var npm = FindNpm() ?? throw new XNGPadException("ErrorNodeMissing");
            var dir = McpCatalog.MermaidDir();
            Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(
                Path.Combine(dir, "package.json"),
                "{\"name\":\"xngpad-mermaid\",\"private\":true}\n",
                ct).ConfigureAwait(false);
            var install = await _runner.RunAsync(
                npm,
                ["install", "--ignore-scripts", MermaidPackage, "--prefix", dir],
                InstallTimeout,
                ct).ConfigureAwait(false);
            if (!install.Success || !File.Exists(McpCatalog.MermaidEntry()))
                throw new XNGPadException("ErrorMermaidInstall") { Detail = DockerError.ShortMessage(install.Combined) };
        }

        if (IsMermaidChromiumInstalled())
            return;

        await InstallBrowserZipAsync(MermaidChromiumDir(), MermaidChromiumUrl, "chrome-win64.zip", ct).ConfigureAwait(false);
        await InstallBrowserZipAsync(MermaidHeadlessDir(), MermaidHeadlessUrl, "chrome-headless-shell-win64.zip", ct).ConfigureAwait(false);
        if (!IsMermaidChromiumInstalled())
            throw new XNGPadException("ErrorMermaidInstall");
    }

    private async Task InstallBrowserZipAsync(string dir, string url, string zipName, CancellationToken ct)
    {
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(dir, zipName);
        await DownloadAsync(url, zip, ct).ConfigureAwait(false);
        ZipFile.ExtractToDirectory(zip, dir, overwriteFiles: true);
        TryDelete(zip);
        await File.WriteAllTextAsync(Path.Combine(dir, "INSTALLATION_COMPLETE"), "", ct).ConfigureAwait(false);
    }

    public const string MathcadMcpZipUrl =
        "https://github.com/fqfqfqff/mathcad-prime-mcp/archive/045db7d4066f4c4fa3bd25bcf1eb8e5439c19edf.zip";

    public bool IsMathcadMcpInstalled() =>
        File.Exists(McpCatalog.MathcadPython())
        && File.Exists(Path.Combine(McpCatalog.MathcadSrc(), "mathcad_prime_mcp", "server.py"));

    public async Task EnsureMathcadMcpAsync(CancellationToken ct = default)
    {
        if (IsMathcadMcpInstalled())
            return;

        await EnsurePythonAsync(ct).ConfigureAwait(false);
        var dir = McpCatalog.MathcadDir();
        Directory.CreateDirectory(dir);
        var zip = Path.Combine(Path.GetTempPath(), "mathcad-prime-mcp.zip");
        await DownloadAsync(MathcadMcpZipUrl, zip, ct).ConfigureAwait(false);
        var extract = Path.Combine(Path.GetTempPath(), "mathcad-prime-" + Guid.NewGuid().ToString("N"));
        try
        {
            ZipFile.ExtractToDirectory(zip, extract, overwriteFiles: true);
            var server = Directory.EnumerateFiles(extract, "server.py", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Contains($"{Path.DirectorySeparatorChar}mathcad_prime_mcp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                ?? throw new XNGPadException("ErrorMathcadDownload");
            var repo = Directory.GetParent(server)?.Parent?.Parent?.FullName
                ?? throw new XNGPadException("ErrorMathcadDownload");
            CopyTree(repo, dir, skipSchemaProxy: false);
        }
        finally
        {
            TryDeleteDir(extract);
        }

        var venv = Path.Combine(dir, "venv");
        var py = Path.Combine(venv, "Scripts", "python.exe");
        if (!File.Exists(py))
        {
            var basePy = PythonCommand();
            var created = await _runner.RunAsync(
                basePy[0],
                basePy.Skip(1).Concat(["-m", "venv", venv]).ToList(),
                InstallTimeout,
                ct).ConfigureAwait(false);
            if (!File.Exists(py))
                throw new XNGPadException("ErrorPythonMissing") { Detail = DockerError.ShortMessage(created.Combined) };
        }

        var pip = await _runner.RunAsync(
            py,
            ["-m", "pip", "install", "mcp>=2", "pywin32", "psutil", "pywinauto", "pillow", "python-docx"],
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (!pip.Success || !IsMathcadMcpInstalled())
            throw new XNGPadException("ErrorMathcadDownload") { Detail = DockerError.ShortMessage(pip.Combined) };

        var post = Path.Combine(venv, "Scripts", "pywin32_postinstall.py");
        if (File.Exists(post))
            await _runner.RunAsync(py, [post, "-install"], InstallTimeout, ct).ConfigureAwait(false);

        var src = McpCatalog.MathcadSrc().Replace("'", "");
        var imported = await _runner.RunAsync(
            py,
            ["-c", "import sys; sys.path.insert(0, r'" + src + "'); import mathcad_prime_mcp.server"],
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (!imported.Success)
            throw new XNGPadException("ErrorMathcadDownload") { Detail = DockerError.ShortMessage(imported.Combined) };
    }

    private async Task EnsurePythonAsync(CancellationToken ct)
    {
        if (FindPython() != null || FindPyLauncher() != null)
            return;

        var winget = FindWinget();
        if (winget == null)
            throw new XNGPadException("ErrorPythonMissing");
        var result = await _runner.RunAsync(
            winget,
            ["install", "-e", "--id", "Python.Python.3.12", "--accept-package-agreements", "--accept-source-agreements", "--disable-interactivity"],
            InstallTimeout,
            ct).ConfigureAwait(false);
        if (FindPython() == null && FindPyLauncher() == null)
            throw new XNGPadException("ErrorPythonMissing") { Detail = DockerError.ShortMessage(result.Combined) };
    }

    public async Task EnsureKompasMcpAsync(CancellationToken ct = default, string? inviteOrJson = null)
    {
        await EnsureKompasExeAsync(ct).ConfigureAwait(false);
        KompasConfig.EnsureProxy();
        await EnsureKompasConfigAsync(inviteOrJson, ct).ConfigureAwait(false);
    }

    public async Task SaveKompasConfigAsync(string inviteOrJson, CancellationToken ct = default)
    {
        await EnsureKompasConfigAsync(inviteOrJson, ct).ConfigureAwait(false);
    }

    private async Task EnsureKompasExeAsync(CancellationToken ct)
    {
        var exe = McpCatalog.KompasExePath();
        if (File.Exists(exe))
            return;
        var dir = Path.GetDirectoryName(exe)!;
        Directory.CreateDirectory(dir);
        var zipUrl = await KompasZipUrlAsync(ct).ConfigureAwait(false);
        var zip = Path.Combine(Path.GetTempPath(), "kompas-mcp.zip");
        await DownloadAsync(zipUrl, zip, ct).ConfigureAwait(false);
        var extract = Path.Combine(Path.GetTempPath(), "kompas-mcp-" + Guid.NewGuid().ToString("N"));
        ZipFile.ExtractToDirectory(zip, extract, overwriteFiles: true);
        var found = Directory.EnumerateFiles(extract, "kompas-mcp.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new XNGPadException("ErrorKompasDownload");
        CopyTree(Path.GetDirectoryName(found)!, dir, skipSchemaProxy: true);

        if (!File.Exists(exe))
            throw new XNGPadException("ErrorKompasDownload");
    }

    private static void CopyTree(string source, string dest, bool skipSchemaProxy)
    {
        Directory.CreateDirectory(dest);
        foreach (var file in Directory.EnumerateFiles(source))
        {
            var name = Path.GetFileName(file);
            if (skipSchemaProxy && name.Equals("schema-proxy.mjs", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(dest, name), overwrite: true);
        }

        foreach (var sub in Directory.EnumerateDirectories(source))
            CopyTree(sub, Path.Combine(dest, Path.GetFileName(sub)), skipSchemaProxy);
    }

    private async Task EnsureKompasConfigAsync(string? inviteOrJson, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(inviteOrJson) && KompasConfig.Exists)
            return;
        var json = inviteOrJson != null && KompasConfig.LooksLikeConfig(inviteOrJson.Trim())
            ? inviteOrJson.Trim()
            : await FetchKompasConfigAsync(string.IsNullOrWhiteSpace(inviteOrJson) ? KompasConfig.DefaultInvite : inviteOrJson.Trim(), ct)
                .ConfigureAwait(false);
        KompasConfig.Write(json);
    }

    private async Task<string> FetchKompasConfigAsync(string invite, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://kompasmcp.ru/onboard");
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 XNGPad");
        req.Headers.TryAddWithoutValidation("Origin", "https://kompasmcp.ru");
        req.Headers.TryAddWithoutValidation("Referer", "https://kompasmcp.ru/");
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["invite_code"] = invite });
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var json = KompasConfig.ExtractJson(html);
        if (!resp.IsSuccessStatusCode || json == null)
            throw new XNGPadException("ErrorKompasInvite");
        return json;
    }

    private async Task<string> KompasZipUrlAsync(CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/repos/dwnmf/KOMPAS-3D-MCP-bin/releases/latest");
        req.Headers.TryAddWithoutValidation("User-Agent", "XNGPad");
        req.Headers.TryAddWithoutValidation("Accept", "application/vnd.github+json");
        using var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in assets.EnumerateArray())
            {
                var name = a.TryGetProperty("name", out var n) ? n.GetString() : "";
                var url = a.TryGetProperty("browser_download_url", out var u) ? u.GetString() : "";
                if (!string.IsNullOrWhiteSpace(url) && name != null && name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    return url;
            }
        }

        throw new XNGPadException("ErrorKompasDownload");
    }

    private static IEnumerable<string> PythonDirs()
    {
        var local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
        var pf = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python");
        foreach (var root in new[] { local, pf })
        {
            if (!Directory.Exists(root))
                continue;
            IEnumerable<string> dirs;
            try
            {
                dirs = Directory.EnumerateDirectories(root);
            }
            catch
            {
                continue;
            }

            foreach (var d in dirs)
                yield return d;
        }

        foreach (var dir in ToolDirs())
            yield return dir;
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
        var partial = dest + ".partial";
        TryDelete(partial);
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "XNGPad");
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            await using (var input = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var output = File.Create(partial))
                await input.CopyToAsync(output, ct).ConfigureAwait(false);
            File.Move(partial, dest, overwrite: true);
        }
        catch
        {
            TryDelete(partial);
            throw;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDir(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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
