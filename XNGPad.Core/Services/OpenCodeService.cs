using System.Diagnostics;
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
            AppPaths.Current.UserProfile,
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
        changed |= RepairElasticsearchNode(mcp);
        changed |= RepairMermaidNode(mcp);
        if (!changed)
            return false;
        WriteBackup(path);
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return true;
    }

    public IReadOnlyList<string> DisableUnresponsive(string path, int timeoutMs = 8000)
    {
        if (!File.Exists(path) || !TryParseObject(ReadConfig(path), out var root) || root == null)
            return [];
        if (root["mcp"] is not JsonObject mcp)
            return [];
        var targets = new List<(string Id, JsonObject Node)>();
        foreach (var entry in mcp)
        {
            if (entry.Value is not JsonObject node || entry.Key.Length == 0)
                continue;
            if (node["enabled"] is JsonValue en && en.TryGetValue<bool>(out var on) && !on)
                continue;
            if (node["command"] is not JsonArray cmd || cmd.Count == 0)
                continue;
            if (SkipProbe(entry.Key, cmd))
                continue;
            targets.Add((entry.Key, node));
        }

        var failed = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.ForEach(targets, new ParallelOptions { MaxDegreeOfParallelism = 6 }, item =>
        {
            var kind = Probe(item.Node, timeoutMs);
            if (kind == ProbeKind.Dead)
                failed.Add(item.Id);
        });
        if (failed.IsEmpty)
            return [];
        foreach (var id in failed)
        {
            if (mcp[id] is JsonObject node)
                node["enabled"] = false;
        }

        WriteBackup(path);
        AtomicWrite(path, root.ToJsonString(WriteOptions) + Environment.NewLine);
        return failed.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private enum ProbeKind { Ok, Dead, Slow }

    private static bool IsNpx(JsonObject node)
    {
        var cmd = node["command"]!.AsArray().Select(x => x?.GetValue<string>() ?? "");
        return cmd.Any(x => x.Equals("npx", StringComparison.OrdinalIgnoreCase) || x.Contains("npx.cmd", StringComparison.OrdinalIgnoreCase));
    }

    private static ProbeKind Probe(JsonObject node, int timeoutMs)
    {
        Process? proc = null;
        try
        {
            proc = Process.Start(BuildProbe(node));
            if (proc == null)
                return ProbeKind.Dead;
            _ = proc.StandardError.ReadToEndAsync();
            proc.StandardInput.WriteLine("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-03-26","capabilities":{},"clientInfo":{"name":"xngpad","version":"1.2.0"}}}""");
            proc.StandardInput.Flush();
            var deadline = Environment.TickCount64 + timeoutMs;
            while (Environment.TickCount64 < deadline)
            {
                var remaining = (int)Math.Max(1, deadline - Environment.TickCount64);
                var read = proc.StandardOutput.ReadLineAsync();
                if (!read.Wait(remaining))
                    return proc.HasExited ? ProbeKind.Dead : ProbeKind.Slow;
                var line = read.Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    if (proc.HasExited)
                        return ProbeKind.Dead;
                    continue;
                }

                if (line.Contains("\"error\"", StringComparison.Ordinal))
                    return ProbeKind.Dead;
                if (line.Contains("\"result\"", StringComparison.Ordinal) || line.Contains("\"jsonrpc\"", StringComparison.Ordinal))
                    return ProbeKind.Ok;
            }

            return proc.HasExited ? ProbeKind.Dead : ProbeKind.Slow;
        }
        catch
        {
            return ProbeKind.Dead;
        }
        finally
        {
            if (proc != null)
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                proc.Dispose();
            }
        }
    }

    private static ProcessStartInfo BuildProbe(JsonObject node)
    {
        var cmd = node["command"]!.AsArray().Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0).ToArray();
        var psi = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        var file = cmd[0];
        if (file.Equals("npx", StringComparison.OrdinalIgnoreCase) || file.Equals("npm", StringComparison.OrdinalIgnoreCase) || file.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase))
        {
            psi.FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe";
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/s");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(file);
        }
        else
        {
            psi.FileName = file;
        }

        foreach (var arg in cmd.Skip(1))
            psi.ArgumentList.Add(arg);
        if (node["cwd"] is JsonValue cwd && cwd.TryGetValue<string>(out var dir) && Directory.Exists(dir))
            psi.WorkingDirectory = dir;
        if (node["environment"] is JsonObject env)
        {
            foreach (var pair in env)
            {
                if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    psi.Environment[pair.Key] = text;
            }
        }

        return psi;
    }

    private static bool RepairTelegramNode(JsonObject mcp)
    {
        if (mcp["telegram"] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        if (!text.Contains("mcp-telegram", StringComparison.OrdinalIgnoreCase)
            && !text.Contains("launch.mjs", StringComparison.OrdinalIgnoreCase))
            return false;
        TelegramLaunch.Ensure();
        var changed = false;
        if (node["environment"] is JsonObject env
            && env["TELEGRAM_API_ID"]?.GetValue<string>() == "17349")
        {
            env.Remove("TELEGRAM_API_ID");
            env.Remove("TELEGRAM_API_HASH");
            changed = true;
        }

        if (!text.Contains("launch.mjs", StringComparison.OrdinalIgnoreCase))
        {
            node["command"] = new JsonArray(NodeExe(), TelegramLaunch.ScriptPath());
            node["type"] = "local";
            node["enabled"] = true;
            node["timeout"] = 120000;
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

    private static bool SkipProbe(string id, JsonArray cmd)
    {
        if (id is "matlab" or "mathcad" or "kompas-3d")
            return true;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        return text.Contains("matlab-mcp-server", StringComparison.OrdinalIgnoreCase)
            || text.Contains("mathcad_prime_mcp", StringComparison.OrdinalIgnoreCase)
            || text.Contains("kompas-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RepairHuggingFaceNode(JsonObject mcp)
    {
        if (mcp["huggingface"] is not JsonObject node)
            return false;
        if (IsHuggingFaceReady(node))
            return false;
        var token = ReadHfToken(node);
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

    private static bool IsHuggingFaceReady(JsonObject node)
    {
        var url = node["url"]?.GetValue<string>() ?? "";
        if (string.Equals(node["type"]?.GetValue<string>(), "remote", StringComparison.OrdinalIgnoreCase)
            && url.Contains("hf.co/mcp", StringComparison.OrdinalIgnoreCase))
            return true;
        if (!UfpProxy.IsProxied(node) || node["environment"] is not JsonObject env)
            return false;
        var remote = env["MCP_REMOTE_URL"]?.GetValue<string>() ?? env["UFP_MCP_URL"]?.GetValue<string>() ?? "";
        return remote.Contains("hf.co/mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ReadHfToken(JsonObject node)
    {
        if (node["environment"] is JsonObject env)
        {
            if (env["HF_TOKEN"] is JsonValue tok && !string.IsNullOrWhiteSpace(tok.GetValue<string>()))
                return tok.GetValue<string>();
            if (env["MCP_REMOTE_HEADERS"] is JsonValue headers)
                return Bearer(headers.GetValue<string>());
        }

        if (node["headers"] is JsonObject raw && raw["Authorization"] is JsonValue auth)
            return Bearer(auth.GetValue<string>());
        return null;
    }

    private static string? Bearer(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        const string prefix = "Bearer ";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value[prefix.Length..].Trim().Trim('"');
        try
        {
            if (JsonNode.Parse(value) is JsonObject obj && obj["Authorization"] is JsonValue auth)
                return Bearer(auth.GetValue<string>());
        }
        catch (System.Text.Json.JsonException)
        {
        }

        return null;
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

    private static bool RepairElasticsearchNode(JsonObject mcp)
    {
        if (mcp["elasticsearch"] is not JsonObject node)
            return false;
        var url = (node["environment"] as JsonObject)?["ES_URL"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(url))
        {
            var env = node["environment"] as JsonObject ?? new JsonObject();
            if (env["OTEL_LOG_LEVEL"]?.GetValue<string>() == "none")
                return false;
            env["OTEL_LOG_LEVEL"] = "none";
            node["environment"] = env;
            return true;
        }

        mcp.Remove("elasticsearch");
        return true;
    }

    private static bool RepairMermaidNode(JsonObject mcp)
    {
        if (mcp["mermaid"] is not JsonObject node)
            return false;
        var entry = McpCatalog.MermaidEntry();
        if (!File.Exists(entry))
            return false;
        var text = node["command"] is JsonArray cmd
            ? string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""))
            : "";
        if (text.Contains(entry, StringComparison.OrdinalIgnoreCase) && !text.Contains("npx", StringComparison.OrdinalIgnoreCase))
            return false;
        node["type"] = "local";
        node["command"] = new JsonArray(NodeExe(), entry);
        node["enabled"] = true;
        node["timeout"] = 120000;
        return true;
    }

    private static bool RepairKompasNode(JsonObject mcp)
    {
        if (mcp["kompas-3d"] is not JsonObject node || node["command"] is not JsonArray cmd)
            return false;
        KompasConfig.EnsureProxy();
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
        var candidate = Path.Combine(AppPaths.Current.ProgramFiles, "nodejs", "node.exe");
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
        if (root["mcp"] is not JsonObject mcp)
            return false;
        if (!RepairTelegramNode(mcp))
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
