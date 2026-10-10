using System.Text.RegularExpressions;
using XNGPad.Core.Models;
using XNGPad.Core.Services;

namespace XNGPad.Tests;

// X-05: tests must never touch the user's real AppData, profile or the system hosts file.
public class IsolationTests
{
    private static string Real(Environment.SpecialFolder folder) =>
        Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);

    internal static string RealHostsFile() =>
        Path.Combine(Real(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    internal static string[] RealUserPaths() =>
    [
        Path.Combine(Real(Environment.SpecialFolder.ApplicationData), "XNGPad"),
        Path.Combine(Real(Environment.SpecialFolder.UserProfile), ".config", "opencode"),
        Path.Combine(Real(Environment.SpecialFolder.UserProfile), ".kompasmcp"),
        RealHostsFile()
    ];

    private static string Snapshot(string path)
    {
        if (File.Exists(path))
        {
            var f = new FileInfo(path);
            return $"{path}|file|{f.Length}|{f.LastWriteTimeUtc.Ticks}";
        }
        if (!Directory.Exists(path))
            return $"{path}|missing";
        var entries = Directory.EnumerateFileSystemEntries(path, "*", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(e => File.Exists(e) ? $"{e}:{new FileInfo(e).Length}:{File.GetLastWriteTimeUtc(e).Ticks}" : e);
        return $"{path}|dir|{string.Join(";", entries)}";
    }

    [Fact]
    public void SideEffectingCalls_DoNotTouchRealUserFilesOrHosts()
    {
        var before = RealUserPaths().Select(Snapshot).ToArray();

        McpCatalog.CreateNode(McpCatalog.All.First(x => x.Extra == McpExtra.Telegram), new AppSettings(), "1", "h");
        McpCatalog.CreateNode(McpCatalog.All.First(x => x.Extra == McpExtra.Ufp), new AppSettings());
        TelegramLaunch.TryHosts();
        KompasConfig.Write("{}");
        Assert.StartsWith(TestPaths.Root, KompasConfig.ConfigFilePath);
        Assert.True(KompasConfig.Exists);

        Assert.Equal(before, RealUserPaths().Select(Snapshot).ToArray());
    }

    [Fact]
    public void Guard_EveryInjectedPathIsInsideTheSandbox_NeverTheRealOne()
    {
        var current = AppPaths.Current;
        Assert.IsType<TestPaths>(current);
        var real = new SystemAppPaths();
        foreach (var prop in typeof(IAppPaths).GetProperties())
        {
            var value = (string)prop.GetValue(current)!;
            Assert.StartsWith(TestPaths.Root, value);
            Assert.NotEqual((string)prop.GetValue(real)!, value);
        }
    }

    [Fact]
    public void Guard_CoreReadsUserAndSystemFoldersOnlyThroughAppPaths()
    {
        var core = Path.Combine(RepoRoot(), "XNGPad.Core");
        var offenders = Directory.EnumerateFiles(core, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && Path.GetFileName(f) != "AppPaths.cs")
            .SelectMany(f => File.ReadLines(f).Select((line, i) => (f, i, line)))
            .Where(x => Regex.IsMatch(x.line, @"Environment\.GetFolderPath|SpecialFolder\."))
            .Where(x => !KnownLegacy(x.f, x.line))
            .Select(x => $"{Path.GetFileName(x.f)}:{x.i + 1}")
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void TryHosts_AppendsOnceToTheInjectedHostsFile()
    {
        var hosts = AppPaths.Current.HostsFile;
        Assert.NotEqual(RealHostsFile(), hosts);

        TelegramLaunch.TryHosts();
        TelegramLaunch.TryHosts();

        var lines = File.ReadAllLines(hosts).Where(l => l.Contains("my.telegram.org")).ToArray();
        Assert.Equal(["149.154.167.220 my.telegram.org"], lines);
    }

    [Fact]
    public void Production_DefaultsAreTheSystemFolders()
    {
        var p = new SystemAppPaths();
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), p.ApplicationData);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), p.UserProfile);
        Assert.Equal(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), p.LocalApplicationData);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts"), p.HostsFile);
    }

    // KompasConfig.FilePath is captured once at type load and kept for compatibility; nothing in XNGPad reads it any more
    // (Exists and Write use ConfigFilePath through AppPaths). Remove it, and this exception, in a follow-up.
    private static bool KnownLegacy(string file, string line) =>
        Path.GetFileName(file) == "KompasConfig.cs" && line.Trim() == "Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),";

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "XNGPad.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("XNGPad.sln not found");
    }
}
