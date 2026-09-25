using System.Text;
using System.Text.Json;

namespace XNGPad.Core.Services;

public static class McpRegistry
{
    private const string Api = "https://registry.modelcontextprotocol.io/v0/servers?limit=100&version=latest";
    private static readonly JsonSerializerOptions CacheJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<IReadOnlyList<McpDef>> LoadAsync(HttpClient http, string cachePath, CancellationToken ct)
    {
        var cached = ReadCache(cachePath);
        var fresh = cached != null
            && File.Exists(cachePath)
            && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < TimeSpan.FromHours(24);
        if (fresh)
            return cached!;
        try
        {
            var list = await FetchAsync(http, ct).ConfigureAwait(false);
            WriteCache(cachePath, list);
            return list;
        }
        catch (Exception ex) when (cached != null && ex is not OperationCanceledException)
        {
            return cached;
        }
    }

    public static async Task<IReadOnlyList<McpDef>> FetchAsync(HttpClient http, CancellationToken ct)
    {
        var list = new List<McpDef>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? cursor = null;
        for (var page = 0; page < 8; page++)
        {
            if (ct.IsCancellationRequested)
                break;
            var url = cursor == null ? Api : Api + "&cursor=" + Uri.EscapeDataString(cursor);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation("User-Agent", "XNGPad");
            using var pageCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            pageCts.CancelAfter(TimeSpan.FromSeconds(8));
            HttpResponseMessage resp;
            try
            {
                resp = await http.SendAsync(req, pageCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            using (resp)
            {
            if (!resp.IsSuccessStatusCode)
                break;
            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            foreach (var def in ParseServers(json))
            {
                var id = def.Id;
                if (!seen.Add(id))
                {
                    id = def.DisplayName.ToLowerInvariant().Replace(' ', '-');
                    if (!seen.Add(id))
                        continue;
                    list.Add(def with { Id = id });
                    continue;
                }

                list.Add(def);
            }

            cursor = NextCursor(json);
            if (cursor == null)
                break;
            }
        }

        return list;
    }

    public static IReadOnlyList<McpDef> ParseServers(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("servers", out var servers) || servers.ValueKind != JsonValueKind.Array)
            return [];
        var list = new List<McpDef>();
        foreach (var item in servers.EnumerateArray())
        {
            if (!item.TryGetProperty("server", out var server))
                continue;
            var def = Parse(server);
            if (def != null)
                list.Add(def);
        }

        return list;
    }

    public static IReadOnlyList<McpDef> Merge(IEnumerable<McpDef> curated, IEnumerable<McpDef> remote)
    {
        var list = curated.ToList();
        var ids = new HashSet<string>(list.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        var pkgs = new HashSet<string>(
            list.Select(x => x.Package).Where(p => p.Length > 0),
            StringComparer.OrdinalIgnoreCase);
        foreach (var r in remote)
        {
            if (!ids.Add(r.Id))
                continue;
            if (r.Package.Length > 0 && !pkgs.Add(r.Package))
                continue;
            list.Add(r);
        }

        return list;
    }

    public static bool Matches(McpDef def, string query)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0)
            return true;
        var hay = string.Join(' ', def.Id, def.DisplayName, def.HintKey, def.Hint, def.GitHubUrl, def.Package, def.RemoteUrl, def.TokenEnvKey);
        foreach (var part in q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!hay.Contains(part, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string FormatHint(McpDef def, LocalizationService loc, IReadOnlyDictionary<string, string>? ruHints = null)
    {
        if (!string.IsNullOrEmpty(def.HintKey))
            return loc.Get(def.HintKey);
        var text = def.Hint ?? "";
        if (text.Length == 0)
            return "";
        if (loc.Language == "ru" && ruHints != null && ruHints.TryGetValue(text, out var ru) && !string.IsNullOrWhiteSpace(ru))
            return ru;
        return text;
    }

    public static Dictionary<string, string> ReadHintCache(string path)
    {
        try
        {
            if (!File.Exists(path))
                return new Dictionary<string, string>(StringComparer.Ordinal);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path), CacheJson);
            return map == null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(map, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    public static async Task<Dictionary<string, string>> TranslateToRuAsync(
        HttpClient http,
        IEnumerable<string> texts,
        string cachePath,
        CancellationToken ct,
        IProgress<Dictionary<string, string>>? progress = null)
    {
        var map = ReadHintCache(cachePath);
        var missing = texts
            .Where(t => !string.IsNullOrWhiteSpace(t) && t.Length > 2 && !map.ContainsKey(t) && !HasCyrillic(t))
            .Distinct(StringComparer.Ordinal)
            .Take(40)
            .ToList();
        if (missing.Count == 0)
            return map;

        var done = 0;
        using var gate = new SemaphoreSlim(3);
        try
        {
            await Task.WhenAll(missing.Select(async t =>
            {
                await gate.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var ru = await TranslateOneAsync(http, t, ct).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(ru) || string.Equals(ru, t, StringComparison.OrdinalIgnoreCase))
                        return;
                    Dictionary<string, string>? snap = null;
                    lock (map)
                    {
                        map[t] = ru.Trim();
                        if (progress != null && Interlocked.Increment(ref done) % 40 == 0)
                            snap = new Dictionary<string, string>(map, StringComparer.Ordinal);
                    }

                    if (snap != null)
                        progress?.Report(snap);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                }
                finally
                {
                    gate.Release();
                }
            })).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            WriteHintCache(cachePath, map);
        }

        return map;
    }

    public static string? ParseGoogleTranslate(string json)
    {
        json = json.Trim();
        if (json.StartsWith(")]}'", StringComparison.Ordinal))
            json = json[4..].TrimStart();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
            return null;
        var first = doc.RootElement[0];
        if (first.ValueKind != JsonValueKind.Array)
            return null;
        var sb = new StringBuilder();
        foreach (var part in first.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.Array && part.GetArrayLength() > 0 && part[0].ValueKind == JsonValueKind.String)
                sb.Append(part[0].GetString());
        }

        var s = sb.ToString().Trim();
        return s.Length == 0 ? null : s;
    }

    public static string? ParseMyMemory(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("responseData", out var data)
            || !data.TryGetProperty("translatedText", out var t)
            || t.ValueKind != JsonValueKind.String)
            return null;
        var s = t.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(s) || s.Contains("MYMEMORY WARNING", StringComparison.OrdinalIgnoreCase))
            return null;
        return s;
    }

    private static McpDef? Parse(JsonElement server)
    {
        var name = GetStr(server, "name");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var title = GetStr(server, "title");
        var desc = GetStr(server, "description") ?? "";
        var github = "";
        if (server.TryGetProperty("repository", out var repo))
            github = GetStr(repo, "url") ?? "";

        string? npm = null;
        string? tokenEnv = null;
        if (server.TryGetProperty("packages", out var packages) && packages.ValueKind == JsonValueKind.Array)
        {
            foreach (var pkg in packages.EnumerateArray())
            {
                var type = GetStr(pkg, "registryType");
                if (!string.Equals(type, "npm", StringComparison.OrdinalIgnoreCase))
                    continue;
                npm = GetStr(pkg, "identifier");
                if (pkg.TryGetProperty("environmentVariables", out var envs) && envs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var env in envs.EnumerateArray())
                    {
                        var secret = env.TryGetProperty("isSecret", out var sec) && sec.ValueKind == JsonValueKind.True;
                        var required = env.TryGetProperty("isRequired", out var req) && req.ValueKind == JsonValueKind.True;
                        if ((secret || required) && env.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                        {
                            tokenEnv = n.GetString();
                            break;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(npm))
                    break;
            }
        }

        string? remote = null;
        if (server.TryGetProperty("remotes", out var remotes) && remotes.ValueKind == JsonValueKind.Array)
        {
            foreach (var r in remotes.EnumerateArray())
            {
                var url = GetStr(r, "url");
                if (string.IsNullOrWhiteSpace(url) || url.Contains('{'))
                    continue;
                if (url.Contains("smithery.ai", StringComparison.OrdinalIgnoreCase) && npm == null)
                    continue;
                remote = url;
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(npm) && string.IsNullOrWhiteSpace(remote))
            return null;

        var id = MakeId(name);
        var display = string.IsNullOrWhiteSpace(title) ? id : title;
        var hint = desc.Length > 320 ? desc[..317] + "…" : desc;
        var guide = string.IsNullOrWhiteSpace(tokenEnv) ? null : "GuideGeneric";
        return new McpDef(
            id,
            display.Trim(),
            "",
            github,
            npm ?? "",
            Hint: hint,
            GuideKey: guide,
            TokenEnvKey: tokenEnv,
            RemoteUrl: remote);
    }

    private static string? NextCursor(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("metadata", out var meta)
            || !meta.TryGetProperty("nextCursor", out var next)
            || next.ValueKind != JsonValueKind.String)
            return null;
        var c = next.GetString();
        return string.IsNullOrWhiteSpace(c) ? null : c;
    }

    private static string MakeId(string name)
    {
        var slash = name.LastIndexOf('/');
        var last = slash >= 0 ? name[(slash + 1)..] : name;
        var id = last.Trim().ToLowerInvariant();
        foreach (var c in new[] { '.', '_', ' ' })
            id = id.Replace(c, '-');
        if (id.Length == 0)
            id = name.Replace('/', '-').ToLowerInvariant();
        return id;
    }

    private static string? GetStr(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.String)
            return null;
        return p.GetString();
    }

    private static List<McpDef>? ReadCache(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            var list = JsonSerializer.Deserialize<List<McpDef>>(File.ReadAllText(path), CacheJson);
            return list is { Count: > 0 } ? list : null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TranslateOneAsync(HttpClient http, string text, CancellationToken ct)
    {
        var q = text.Length > 400 ? text[..400] : text;
        return await GetTranslateAsync(
            http,
            "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=ru&dt=t&q=" + Uri.EscapeDataString(q),
            ParseGoogleTranslate,
            ct).ConfigureAwait(false);
    }

    public static string? ParseLingva(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("translation", out var t) || t.ValueKind != JsonValueKind.String)
            return null;
        var s = t.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static async Task<string?> GetTranslateAsync(
        HttpClient http,
        string url,
        Func<string, string?> parse,
        CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(4));
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var resp = await http.SendAsync(req, cts.Token).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return null;
            var json = await resp.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
            var ru = parse(json);
            return string.IsNullOrWhiteSpace(ru) ? null : ru;
        }
        catch
        {
            return null;
        }
    }

    private static bool HasCyrillic(string s)
    {
        foreach (var c in s)
        {
            if (c is >= 'А' and <= 'я' or 'ё' or 'Ё')
                return true;
        }

        return false;
    }

    private static void WriteCache(string path, IReadOnlyList<McpDef> list)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(list, CacheJson));
        }
        catch
        {
        }
    }

    private static void WriteHintCache(string path, Dictionary<string, string> map)
    {
        try
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(path, JsonSerializer.Serialize(map, CacheJson));
        }
        catch
        {
        }
    }
}
