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

    public static void EnsureProxy()
    {
        var path = McpCatalog.KompasProxyPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path))
            File.WriteAllText(path, ProxyScript);
    }

    private const string ProxyScript = """
        import { spawn } from "node:child_process";
        import path from "node:path";
        const exe = process.env.KOMPAS_MCP_EXE;
        if (!exe) { console.error("KOMPAS_MCP_EXE is not set"); process.exit(1); }
        function fixSchema(node) {
          if (!node || typeof node !== "object") return;
          if (Array.isArray(node)) { for (const item of node) fixSchema(item); return; }
          if (Array.isArray(node.items)) { node.prefixItems = node.items; node.items = false; }
          for (const value of Object.values(node)) fixSchema(value);
        }
        function frame(text) {
          const body = Buffer.from(text, "utf8");
          return Buffer.concat([Buffer.from(`Content-Length: ${body.length}\r\n\r\n`, "utf8"), body]);
        }
        function pump(input, output, rewrite) {
          let buf = Buffer.alloc(0);
          input.on("data", (chunk) => {
            buf = Buffer.concat([buf, chunk]);
            while (true) {
              const headerEnd = buf.indexOf("\r\n\r\n");
              if (headerEnd < 0) break;
              const header = buf.subarray(0, headerEnd).toString("utf8");
              const match = /Content-Length:\s*(\d+)/i.exec(header);
              if (!match) break;
              const length = Number(match[1]);
              const start = headerEnd + 4;
              if (buf.length < start + length) break;
              let body = buf.subarray(start, start + length).toString("utf8");
              buf = buf.subarray(start + length);
              if (rewrite) {
                try { const msg = JSON.parse(body); if (msg.result) fixSchema(msg.result); body = JSON.stringify(msg); } catch {}
              }
              output.write(frame(body));
            }
          });
          input.on("end", () => output.end());
        }
        const child = spawn(exe, [], { cwd: path.dirname(exe), stdio: ["pipe", "pipe", "inherit"], windowsHide: true });
        pump(process.stdin, child.stdin, false);
        pump(child.stdout, process.stdout, true);
        child.on("exit", (code) => process.exit(code ?? 1));
        """;

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
