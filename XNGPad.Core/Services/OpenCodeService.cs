using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed record McpSecret(string Id, string Key, string Value, string Place);

public sealed class OpenCodeService
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public static string DefaultConfigPath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config",
            "opencode",
            "opencode.json");

    public string ResolvePath(string? overridePath)
    {
        return string.IsNullOrWhiteSpace(overridePath) ? DefaultConfigPath : overridePath.Trim();
    }

    public OpenCodeKind ReadStatus(string path)
    {
        if (!File.Exists(path))
            return OpenCodeKind.ConfigMissing;

        try
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            if (JsonNode.Parse(text) is not JsonObject root)
                return OpenCodeKind.InvalidJson;
            return HasSearxng(root) ? OpenCodeKind.Connected : OpenCodeKind.McpMissing;
        }
        catch (JsonException)
        {
            return OpenCodeKind.InvalidJson;
        }
        catch (IOException)
        {
            return OpenCodeKind.ConfigMissing;
        }
    }

    public string Connect(string path, string searxngUrl)
    {
        var url = searxngUrl.Trim().TrimEnd('/');
        return UpsertMcp(path, Constants.McpName, CreateSearxngNode(url));
    }

    public string Disconnect(string path) => RemoveMcp(path, Constants.McpName);

    public bool HasMcp(string path, string id)
    {
        if (!File.Exists(path))
            return false;
        try
        {
            var text = File.ReadAllText(path, Encoding.UTF8);
            return JsonNode.Parse(text) is JsonObject root
                   && root["mcp"] is JsonObject mcp
                   && mcp[id] != null;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public string UpsertMcp(string path, string id, JsonObject node)
    {
        if (!File.Exists(path))
        {
            var created = new JsonObject
            {
                ["$schema"] = Constants.SchemaUrl,
                ["mcp"] = new JsonObject { [id] = node }
            };
            AtomicWrite(path, created.ToJsonString(WriteOptions) + Environment.NewLine);
            return "";
        }

        var text = ReadConfig(path);
        if (!TryParseObject(text, out var root) || root == null)
        {
            var backup = WriteBackup(path);
            throw new BrokenOpenCodeJsonException(path, backup);
        }

        var backupPath = WriteBackup(path);
        root["$schema"] = Constants.SchemaUrl;
        var mcp = root["mcp"] as JsonObject;
        if (mcp == null)
        {
            mcp = new JsonObject();
            root["mcp"] = mcp;
        }

        mcp[id] = node;
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return backupPath;
    }

    public IReadOnlyList<string> ListMcpIds(string path)
    {
        if (!File.Exists(path))
            return [];
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) is not JsonObject root
                || root["mcp"] is not JsonObject mcp)
                return [];
            return mcp.Select(x => x.Key).Where(x => x.Length > 0).ToList();
        }
        catch
        {
            return [];
        }
    }

    public bool RepairMcp(string path)
    {
        if (!File.Exists(path) || !TryParseObject(ReadConfig(path), out var root) || root == null)
            return false;
        if (root["mcp"] is not JsonObject mcp)
            return false;
        var changed = false;
        changed |= RepairTelegramNode(mcp);
        changed |= RepairFetchNode(mcp);
        changed |= RepairHuggingFaceNode(mcp);
        changed |= DisableDeadRemote(mcp, "agentic-news");
        changed |= EnsureLocalTimeout(mcp);
        changed |= RemoveMcpId(mcp, "git");
        changed |= RemoveMcpId(mcp, "markitdown");
        changed |= RemoveMcpId(mcp, "agentic-news");
        changed |= RepairRemoteNodes(mcp);
        changed |= RepairUfpNode(mcp);
        changed |= RepairKompasNode(mcp);
        if (!changed)
            return false;
        WriteBackup(path);
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return true;
    }

    private static bool RepairTelegramNode(JsonObject mcp)
    {
        if (mcp["telegram"] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        var hasPkg = cmd.Any(x => (x?.GetValue<string>() ?? "").Contains("mcp-telegram", StringComparison.OrdinalIgnoreCase));
        if (!hasPkg)
            return false;
        var changed = false;
        for (var i = cmd.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(cmd[i]?.GetValue<string>(), "start", StringComparison.OrdinalIgnoreCase))
                continue;
            cmd.RemoveAt(i);
            changed = true;
        }

        return changed;
    }

    private static bool RepairFetchNode(JsonObject mcp)
    {
        if (mcp["fetch"] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        if (!text.Contains("server-fetch", StringComparison.OrdinalIgnoreCase))
            return false;
        node["command"] = new JsonArray("npx", "-y", "mcp-fetch");
        node["timeout"] = 120000;
        node["enabled"] = true;
        return true;
    }

    private static bool RepairHuggingFaceNode(JsonObject mcp)
    {
        if (mcp["huggingface"] is not JsonObject node)
            return false;
        if (node["type"]?.GetValue<string>() == "remote" && (node["url"]?.GetValue<string>() ?? "").Contains("hf.co/mcp", StringComparison.OrdinalIgnoreCase))
            return false;
        string? token = null;
        if (node["environment"] is JsonObject env && env["HF_TOKEN"] is JsonValue tok)
            token = tok.GetValue<string>();
        var remote = new JsonObject
        {
            ["type"] = "remote",
            ["url"] = "https://hf.co/mcp",
            ["enabled"] = true,
            ["timeout"] = 60000
        };
        if (!string.IsNullOrWhiteSpace(token))
            remote["headers"] = new JsonObject { ["Authorization"] = "Bearer " + token };
        mcp["huggingface"] = remote;
        return true;
    }

    private static bool DisableDeadRemote(JsonObject mcp, string id)
    {
        if (mcp[id] is not JsonObject node)
            return false;
        if (node["type"]?.GetValue<string>() != "remote")
            return false;
        if (node["enabled"] is JsonValue en && en.TryGetValue<bool>(out var on) && !on)
            return false;
        node["enabled"] = false;
        return true;
    }

    private static bool RemoveMcpId(JsonObject mcp, string id)
    {
        if (mcp[id] == null)
            return false;
        mcp.Remove(id);
        return true;
    }

    private static bool DisableMissingPackage(JsonObject mcp, string id, string marker)
    {
        if (mcp[id] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        if (!text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            return false;
        if (node["enabled"] is JsonValue en && en.TryGetValue<bool>(out var on) && !on)
            return false;
        node["enabled"] = false;
        return true;
    }

    private static bool RepairRemoteNodes(JsonObject mcp)
    {
        var changed = false;
        foreach (var key in mcp.Select(e => e.Key).ToList())
        {
            if (mcp[key] is not JsonObject node)
                continue;
            if (!string.Equals(node["type"]?.GetValue<string>(), "remote", StringComparison.OrdinalIgnoreCase))
                continue;
            var url = node["url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(url))
                continue;
            JsonObject? headers = null;
            if (node["headers"] is JsonObject raw)
                headers = JsonNode.Parse(raw.ToJsonString())!.AsObject();
            mcp[key] = UfpProxy.Node(url, headers);
            changed = true;
        }

        return changed;
    }

    public IReadOnlyList<string> ListRemoteIds(string path)
    {
        if (!File.Exists(path))
            return [];
        try
        {
            if (JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)) is not JsonObject root
                || root["mcp"] is not JsonObject mcp)
                return [];
            var list = new List<string>();
            foreach (var entry in mcp)
            {
                if (entry.Value is not JsonObject node)
                    continue;
                if (string.Equals(node["type"]?.GetValue<string>(), "remote", StringComparison.OrdinalIgnoreCase)
                    || UfpProxy.IsProxied(node))
                    list.Add(entry.Key);
            }

            return list;
        }
        catch
        {
            return [];
        }
    }

    private static bool RepairUfpNode(JsonObject mcp)
    {
        if (mcp["ufp"] is not JsonObject node)
            return false;
        if (UfpProxy.IsProxied(node))
            return false;
        mcp["ufp"] = UfpProxy.Node();
        return true;
    }

    private static bool RepairKompasNode(JsonObject mcp)
    {
        KompasConfig.EnsureProxy();
        if (mcp["kompas-3d"] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        if (text.Contains("schema-proxy.mjs", StringComparison.OrdinalIgnoreCase))
            return false;
        if (!text.Contains("kompas-mcp.exe", StringComparison.OrdinalIgnoreCase))
            return false;
        var exe = McpCatalog.KompasExePath();
        var dir = McpCatalog.KompasDir();
        node["command"] = new JsonArray(NodeExe(), McpCatalog.KompasProxyPath());
        node["cwd"] = dir;
        node["timeout"] = 120000;
        node["enabled"] = true;
        var env = node["environment"] as JsonObject ?? new JsonObject();
        env["KOMPAS_MCP_EXE"] = exe;
        node["environment"] = env;
        return true;
    }

    private static string NodeExe()
    {
        var candidate = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe");
        return File.Exists(candidate) ? candidate : "node";
    }

    private static bool EnsureLocalTimeout(JsonObject mcp)
    {
        var changed = false;
        foreach (var entry in mcp)
        {
            if (entry.Value is not JsonObject node)
                continue;
            if (node["type"]?.GetValue<string>() != "local")
                continue;
            if (node["timeout"] != null)
                continue;
            node["timeout"] = 120000;
            changed = true;
        }

        return changed;
    }

    public bool RepairTelegram(string path)
    {
        if (!File.Exists(path) || !TryParseObject(ReadConfig(path), out var root) || root == null)
            return false;
        if (root["mcp"] is not JsonObject mcp || mcp["telegram"] is not JsonObject node)
            return false;
        if (node["command"] is not JsonArray cmd)
            return false;
        var hasPkg = cmd.Any(x => (x?.GetValue<string>() ?? "").Contains("mcp-telegram", StringComparison.OrdinalIgnoreCase));
        if (!hasPkg)
            return false;
        var changed = false;
        for (var i = cmd.Count - 1; i >= 0; i--)
        {
            if (!string.Equals(cmd[i]?.GetValue<string>(), "start", StringComparison.OrdinalIgnoreCase))
                continue;
            cmd.RemoveAt(i);
            changed = true;
        }

        if (!changed)
            return false;
        WriteBackup(path);
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return true;
    }

    public IReadOnlyList<McpSecret> ListSecrets(string path)
    {
        var list = new List<McpSecret>();
        if (!File.Exists(path) || !TryParseObject(ReadConfig(path), out var root) || root?["mcp"] is not JsonObject mcp)
            return list;
        foreach (var entry in mcp)
        {
            if (entry.Value is not JsonObject node)
                continue;
            CollectSecrets(list, entry.Key, "environment", node["environment"] as JsonObject);
            CollectSecrets(list, entry.Key, "headers", node["headers"] as JsonObject);
        }

        return list;
    }

    public void SetSecret(string path, string id, string key, string place, string value)
    {
        if (place is not ("environment" or "headers"))
            throw new XNGPadException("ErrorGeneric");
        if (!File.Exists(path) || !TryParseObject(ReadConfig(path), out var root) || root == null)
            throw new XNGPadException("ErrorOpenCodeUnavailable", path);
        if (root["mcp"] is not JsonObject mcp || mcp[id] is not JsonObject node)
            throw new XNGPadException("ErrorGeneric");
        var bag = node[place] as JsonObject;
        if (bag == null)
        {
            bag = new JsonObject();
            node[place] = bag;
        }

        WriteBackup(path);
        bag[key] = value;
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
    }

    private static void CollectSecrets(List<McpSecret> list, string id, string place, JsonObject? bag)
    {
        if (bag == null)
            return;
        foreach (var pair in bag)
        {
            if (pair.Value is not JsonValue val || !val.TryGetValue<string>(out var text))
                continue;
            if (string.IsNullOrWhiteSpace(text) || !LooksLikeSecret(pair.Key, text))
                continue;
            list.Add(new McpSecret(id, pair.Key, text, place));
        }
    }

    private static bool LooksLikeSecret(string key, string value)
    {
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return false;
        if (key.EndsWith("_ROOTS", StringComparison.OrdinalIgnoreCase)
            || key.EndsWith("_ENABLE_WRITE", StringComparison.OrdinalIgnoreCase)
            || key.Equals("SEARXNG_URL", StringComparison.OrdinalIgnoreCase))
            return false;
        return key.Contains("KEY", StringComparison.OrdinalIgnoreCase)
            || key.Contains("TOKEN", StringComparison.OrdinalIgnoreCase)
            || key.Contains("SECRET", StringComparison.OrdinalIgnoreCase)
            || key.Contains("HASH", StringComparison.OrdinalIgnoreCase)
            || key.Contains("PASSWORD", StringComparison.OrdinalIgnoreCase)
            || key.Contains("API_ID", StringComparison.OrdinalIgnoreCase)
            || key.Contains("AUTH", StringComparison.OrdinalIgnoreCase);
    }

    public string RemoveMcp(string path, string id)
    {
        if (!File.Exists(path))
            return "";

        var text = ReadConfig(path);
        if (!TryParseObject(text, out var root) || root == null)
        {
            var backup = WriteBackup(path);
            throw new BrokenOpenCodeJsonException(path, backup);
        }

        if (root["mcp"] is not JsonObject mcp || mcp[id] == null)
            return "";

        var backupPath = WriteBackup(path);
        mcp.Remove(id);
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return backupPath;
    }

    public static bool HasSearxng(JsonObject root)
    {
        return root["mcp"] is JsonObject mcp && mcp[Constants.McpName] != null;
    }

    private string ReadConfig(string path)
    {
        try
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
        catch (IOException)
        {
            throw new XNGPadException("ErrorOpenCodeUnavailable", path);
        }
    }

    private static JsonObject CreateSearxngNode(string url)
    {
        return new JsonObject
        {
            ["type"] = "local",
            ["command"] = new JsonArray("npx", "-y", "mcp-searxng"),
            ["environment"] = new JsonObject
            {
                ["SEARXNG_URL"] = url
            }
        };
    }

    private static bool TryParseObject(string text, out JsonObject? root)
    {
        root = null;
        try
        {
            root = JsonNode.Parse(text) as JsonObject;
            return root != null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string WriteBackup(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var backup = Path.Combine(dir, $"{Path.GetFileName(path)}.bak-xngpad-{stamp}");
        for (var i = 2; File.Exists(backup); i++)
            backup = Path.Combine(dir, $"{Path.GetFileName(path)}.bak-xngpad-{stamp}-{i}");
        File.Copy(path, backup, overwrite: false);
        return backup;
    }

    private static void AtomicWrite(string path, string contents)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var temp = path + ".xngpad.tmp";
        var utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        File.WriteAllText(temp, contents, utf8);
        if (File.Exists(path))
            File.Replace(temp, path, destinationBackupFileName: null);
        else
            File.Move(temp, path);
    }
}
