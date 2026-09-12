using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

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
