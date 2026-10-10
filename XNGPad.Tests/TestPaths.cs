using System.Runtime.CompilerServices;
using XNGPad.Core.Services;

namespace XNGPad.Tests;

/// <summary>
/// X-05: before any test runs, every user/system location is redirected into one temp sandbox.
/// The sandbox is deleted when the test process exits.
/// </summary>
public sealed class TestPaths : IAppPaths
{
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "xngpad-test-" + Guid.NewGuid().ToString("N"));

    public string ApplicationData { get; } = Dir("AppData", "Roaming");
    public string LocalApplicationData { get; } = Dir("AppData", "Local");
    public string UserProfile { get; } = Dir("profile");
    public string MyDocuments { get; } = Dir("profile", "Documents");
    public string Desktop { get; } = Dir("profile", "Desktop");
    public string ProgramFiles { get; } = Dir("Program Files");
    public string ProgramFilesX86 { get; } = Dir("Program Files (x86)");
    public string HostsFile { get; } = Path.Combine(Dir("Windows", "System32", "drivers", "etc"), "hosts");

    private static string Dir(params string[] parts)
    {
        var dir = Path.Combine([Root, .. parts]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [ModuleInitializer]
    internal static void Install()
    {
        AppPaths.Current = new TestPaths();
        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            try { Directory.Delete(Root, recursive: true); } catch { }
        };
    }
}
