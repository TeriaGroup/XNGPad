namespace XNGPad.Core.Services;

public sealed class NodeService
{
    public bool IsNpxAvailable()
    {
        foreach (var dir in CandidateDirs())
        {
            foreach (var name in new[] { "npx.cmd", "npx.exe", "npx" })
            {
                try
                {
                    if (File.Exists(Path.Combine(dir, name)))
                        return true;
                }
                catch
                {
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> CandidateDirs()
    {
        var extra = new[]
        {
            Path.Combine(AppPaths.Current.ProgramFiles, "nodejs"),
            Path.Combine(AppPaths.Current.ProgramFilesX86, "nodejs"),
            Path.Combine(AppPaths.Current.LocalApplicationData, "Programs", "nodejs")
        };

        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in path.Split(Path.PathSeparator).Concat(extra))
        {
            var trimmed = dir.Trim().Trim('"');
            if (trimmed.Length > 0)
                yield return trimmed;
        }
    }
}
