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

    public static bool Exists => File.Exists(ConfigFilePath);

    public static void EnsureProxy()
    {
        var path = McpCatalog.KompasProxyPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(path) || File.ReadAllText(path) != ProxyScript)
            File.WriteAllText(path, ProxyScript, new UTF8Encoding(false));
    }

    private const string ProxyScript = """
        import { spawn, execFileSync } from "node:child_process";
        import fs from "node:fs";
        import path from "node:path";
        const exe = process.env.KOMPAS_MCP_EXE;
        if (!exe) { console.error("KOMPAS_MCP_EXE is not set"); process.exit(1); }
        function kompasExe() {
          const candidates = [
            process.env.KOMPAS_EXE,
            "C:\\APPs\\ascon\\kompas\\Bin\\kStudy.exe",
            "C:\\APPs\\kompas\\Bin\\kStudy.exe",
            "C:\\Program Files\\ASCON\\KOMPAS-3D v24\\Bin\\kStudy.exe",
            "C:\\Program Files\\ASCON\\KOMPAS-3D v23\\Bin\\kStudy.exe"
          ].filter(Boolean);
          return candidates.find((c) => fs.existsSync(c)) || null;
        }
        function ensureInstallLink(kompas) {
          const stale = "C:\\APPs\\kompas";
          if (fs.existsSync(path.join(stale, "Bin", "kStudy.exe"))) return;
          const root = path.resolve(path.dirname(kompas), "..");
          if (path.resolve(root).toLowerCase() === path.resolve(stale).toLowerCase()) return;
          try { execFileSync("cmd.exe", ["/d", "/c", "mklink", "/J", stale, root], { windowsHide: true }); } catch (err) { console.error(String(err)); }
        }
        function kompasRunning() {
          try {
            const out = execFileSync("tasklist.exe", ["/FI", "IMAGENAME eq kStudy.exe", "/NH"], { encoding: "utf8", windowsHide: true });
            return /kStudy\.exe/i.test(out);
          } catch { return false; }
        }
        function sleep(ms) { Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms); }
        function launchKompas() {
          const kompas = kompasExe();
          if (!kompas) { console.error("KOMPAS-3D exe not found"); return; }
          ensureInstallLink(kompas);
          if (kompasRunning()) return;
          console.error("starting KOMPAS-3D");
          spawn(kompas, [], { detached: true, stdio: "ignore" }).unref();
          for (let i = 0; i < 20 && !kompasRunning(); i++) sleep(250);
        }
        function fixSchema(node) {
          if (!node || typeof node !== "object") return;
          if (Array.isArray(node)) { for (const item of node) fixSchema(item); return; }
          if (Array.isArray(node.items)) { node.prefixItems = node.items; node.items = false; }
          for (const value of Object.values(node)) fixSchema(value);
        }
        let clientMode = null;
        function writeClient(text) {
          let body = text;
          try {
            const msg = JSON.parse(text);
            if (msg.result) fixSchema(msg.result);
            body = JSON.stringify(msg);
          } catch {}
          if (clientMode === "lsp") {
            const raw = Buffer.from(body, "utf8");
            process.stdout.write(`Content-Length: ${raw.length}\r\n\r\n`);
            process.stdout.write(raw);
          } else {
            process.stdout.write(body + "\n");
          }
        }
        launchKompas();
        const child = spawn(exe, [], { cwd: path.dirname(exe), stdio: ["pipe", "pipe", "inherit"], windowsHide: true });
        let childBuf = Buffer.alloc(0);
        child.stdout.on("data", (chunk) => {
          childBuf = Buffer.concat([childBuf, chunk]);
          const head = childBuf.subarray(0, Math.min(childBuf.length, 20)).toString("utf8");
          if (head.includes("Content-Length")) {
            while (true) {
              const headerEnd = childBuf.indexOf("\r\n\r\n");
              if (headerEnd < 0) break;
              const header = childBuf.subarray(0, headerEnd).toString("utf8");
              const match = /Content-Length:\s*(\d+)/i.exec(header);
              if (!match) break;
              const length = Number(match[1]);
              const start = headerEnd + 4;
              if (childBuf.length < start + length) break;
              const body = childBuf.subarray(start, start + length).toString("utf8");
              childBuf = childBuf.subarray(start + length);
              writeClient(body);
            }
            return;
          }
          while (true) {
            const i = childBuf.indexOf(0x0a);
            if (i < 0) break;
            const line = childBuf.subarray(0, i).toString("utf8").replace(/\r$/, "");
            childBuf = childBuf.subarray(i + 1);
            if (line.trim()) writeClient(line);
          }
        });
        function writeChild(text) {
          if (text.trim()) child.stdin.write(text.trim() + "\n");
        }
        let inBuf = Buffer.alloc(0);
        function pump() {
          if (!clientMode) {
            const head = inBuf.subarray(0, Math.min(inBuf.length, 24)).toString("utf8").trimStart();
            if (!head) return;
            clientMode = head.startsWith("{") || head.startsWith("[") ? "line" : "lsp";
          }
          if (clientMode === "line") {
            while (true) {
              const i = inBuf.indexOf(0x0a);
              if (i < 0) break;
              const line = inBuf.subarray(0, i).toString("utf8").replace(/\r$/, "");
              inBuf = inBuf.subarray(i + 1);
              writeChild(line);
            }
            return;
          }
          while (true) {
            const headerEnd = inBuf.indexOf("\r\n\r\n");
            if (headerEnd < 0) break;
            const header = inBuf.subarray(0, headerEnd).toString("utf8");
            const match = /Content-Length:\s*(\d+)/i.exec(header);
            if (!match) break;
            const length = Number(match[1]);
            const start = headerEnd + 4;
            if (inBuf.length < start + length) break;
            const body = inBuf.subarray(start, start + length).toString("utf8");
            inBuf = inBuf.subarray(start + length);
            writeChild(body);
          }
        }
        process.stdin.on("data", (chunk) => { inBuf = Buffer.concat([inBuf, chunk]); pump(); });
        child.on("exit", (code) => process.exit(code ?? 1));
        """;

    public static void Write(string json)
    {
        var dir = Path.GetDirectoryName(ConfigFilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(ConfigFilePath, json.Trim() + Environment.NewLine, new UTF8Encoding(false));
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

    /// <summary>
    /// %USERPROFILE%\.kompasmcp\config.json, resolved on each call through <see cref="AppPaths"/> so tests can redirect it.
    /// <see cref="FilePath"/> keeps the value captured at type load for existing callers.
    /// </summary>
    public static string ConfigFilePath => Path.Combine(AppPaths.Current.UserProfile, ".kompasmcp", "config.json");
}
