namespace XNGPad.Core.Services;

/// <summary>
/// Every user or system location XNGPad reads or writes. Production uses <see cref="SystemAppPaths"/>;
/// tests swap in a temp sandbox so they never touch the real AppData, profile or hosts file (X-05).
/// </summary>
public interface IAppPaths
{
    string ApplicationData { get; }
    string LocalApplicationData { get; }
    string UserProfile { get; }
    string MyDocuments { get; }
    string Desktop { get; }
    string ProgramFiles { get; }
    string ProgramFilesX86 { get; }

    /// <summary>The system hosts file (%SystemRoot%\System32\drivers\etc\hosts on Windows).</summary>
    string HostsFile { get; }
}

public sealed class SystemAppPaths : IAppPaths
{
    public string ApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    public string LocalApplicationData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public string UserProfile => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    public string MyDocuments => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string Desktop => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    public string ProgramFiles => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    public string ProgramFilesX86 => Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
    public string HostsFile => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
}

public static class AppPaths
{
    private static IAppPaths _current = new SystemAppPaths();

    /// <summary>Ambient provider used by the static services. Only tests replace it.</summary>
    public static IAppPaths Current
    {
        get => Volatile.Read(ref _current);
        set => Volatile.Write(ref _current, value ?? throw new ArgumentNullException(nameof(value)));
    }
}
