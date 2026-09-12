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
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "nodejs")
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
