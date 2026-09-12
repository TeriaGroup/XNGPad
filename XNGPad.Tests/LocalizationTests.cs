using System.Xml.Linq;
using XNGPad.Core.Services;

namespace XNGPad.Tests;

public class LocalizationTests
{
    [Fact]
    public void RuAndEn_HaveTheSameKeys()
    {
        var dir = FindResourcesDir();
        var ru = LoadKeys(Path.Combine(dir, "Resources.ru.resx"));
        var en = LoadKeys(Path.Combine(dir, "Resources.en.resx"));
        var def = LoadKeys(Path.Combine(dir, "Resources.resx"));

        Assert.True(ru.Count > 0);
        Assert.Equal(Sorted(en.Keys), Sorted(ru.Keys));
        Assert.Equal(Sorted(def.Keys), Sorted(ru.Keys));
        Assert.All(ru, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value)));
        Assert.All(en, kv => Assert.False(string.IsNullOrWhiteSpace(kv.Value)));
    }

    [Fact]
    public void LocalizationService_SwitchesLanguage()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("en");
        Assert.Equal("missing", loc.Get("DockerMissing"));
        loc.SetLanguage("ru");
        Assert.Equal("нет", loc.Get("DockerMissing"));
        Assert.Equal("XNGPad", loc.Get("AppTitle"));
    }

    private static string FindResourcesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "XNGPad.Core", "Resources");
            if (File.Exists(Path.Combine(candidate, "Resources.ru.resx")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Resources folder not found.");
    }

    private static Dictionary<string, string> LoadKeys(string path)
    {
        var doc = XDocument.Load(path);
        return doc.Root!
            .Elements("data")
            .Select(e => new
            {
                Name = (string?)e.Attribute("name") ?? "",
                Value = (e.Element("value")?.Value ?? "").Trim()
            })
            .Where(x => x.Name.Length > 0)
            .ToDictionary(x => x.Name, x => x.Value);
    }

    private static List<string> Sorted(IEnumerable<string> keys) => keys.OrderBy(k => k, StringComparer.Ordinal).ToList();
}
