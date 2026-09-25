using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace XNGPad.Core.Services;

public static class KompasConfig
{
    public const string DefaultInvite = "invite-d56e976a";

    public static string FilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".kompasmcp",
        "config.json");

    public static bool Exists => File.Exists(FilePath);

    public static void Write(string json)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(FilePath, json.Trim() + Environment.NewLine, new UTF8Encoding(false));
    }

    public static bool LooksLikeConfig(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            return doc.RootElement.TryGetProperty("client_id", out _)
                || doc.RootElement.TryGetProperty("token", out _)
                || doc.RootElement.TryGetProperty("mode", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string? ExtractJson(string htmlOrJson)
    {
        var t = htmlOrJson.Trim();
        if (t.StartsWith('{') && LooksLikeConfig(t))
            return t;

        const string prefix = "data:application/json";
        var i = 0;
        while ((i = t.IndexOf(prefix, i, StringComparison.OrdinalIgnoreCase)) >= 0)
        {
            var comma = t.IndexOf(',', i);
            if (comma < 0)
                break;
            var end = t.IndexOf('"', comma);
            if (end < 0)
                break;
            var json = Uri.UnescapeDataString(t[(comma + 1)..end].Replace("+", " "));
            if (LooksLikeConfig(json))
                return json;
            i = end + 1;
        }

        foreach (Match m in Regex.Matches(t, @"<textarea[^>]*>([\s\S]*?)</textarea>", RegexOptions.IgnoreCase))
        {
            var inner = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
            if (inner.StartsWith('{') && LooksLikeConfig(inner))
                return inner;
        }

        return null;
    }
}
