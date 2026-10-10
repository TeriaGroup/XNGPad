using System.Text;
using System.Text.Json.Nodes;

namespace XNGPad.Core.Services;

public static class UfpProxy
{
    public const string Url = "https://agenticfabricationnetwork.ai/mcp";

    public static string Dir() => Path.Combine(
        AppPaths.Current.ApplicationData,
        Constants.SettingsFolderName,
        "ufp");

    public static string ScriptPath() => Path.Combine(Dir(), "schema-proxy.mjs");

    public static void Ensure()
    {
        Directory.CreateDirectory(Dir());
        var path = ScriptPath();
        if (!File.Exists(path) || File.ReadAllText(path) != Script)
            File.WriteAllText(path, Script, new UTF8Encoding(false));
    }

    public static JsonObject Node() => Node(Url);

    public static JsonObject Node(string url, JsonObject? headers = null)
    {
        Ensure();
        var env = new JsonObject
        {
            ["MCP_REMOTE_URL"] = url,
            ["UFP_MCP_URL"] = url
        };
        if (headers is { Count: > 0 })
            env["MCP_REMOTE_HEADERS"] = headers.ToJsonString();
        return new JsonObject
        {
            ["type"] = "local",
            ["command"] = new JsonArray(NodeExe(), ScriptPath()),
            ["enabled"] = true,
            ["timeout"] = 120000,
            ["environment"] = env
        };
    }

    public static bool IsProxied(JsonObject node)
    {
        if (node["command"] is not JsonArray cmd)
            return false;
        var text = string.Join(' ', cmd.Select(x => x?.GetValue<string>() ?? ""));
        return text.Contains(ScriptPath(), StringComparison.OrdinalIgnoreCase);
    }

    private static string NodeExe()
    {
        var candidate = Path.Combine(AppPaths.Current.ProgramFiles, "nodejs", "node.exe");
        return File.Exists(candidate) ? candidate : "node";
    }

    private const string Script = """
        import https from "node:https";
        import { URL } from "node:url";
        for (const key of ["HTTP_PROXY", "HTTPS_PROXY", "http_proxy", "https_proxy", "ALL_PROXY", "all_proxy"]) delete process.env[key];
        process.env.NODE_USE_ENV_PROXY = "0";
        const url = process.env.MCP_REMOTE_URL || process.env.UFP_MCP_URL || "https://agenticfabricationnetwork.ai/mcp";
        let extraHeaders = {};
        try { if (process.env.MCP_REMOTE_HEADERS) extraHeaders = JSON.parse(process.env.MCP_REMOTE_HEADERS); } catch {}
        let session = null;
        let listening = false;
        let mode = null;
        let buf = Buffer.alloc(0);
        let chain = Promise.resolve();

        function fixSchema(node) {
          if (!node || typeof node !== "object") return;
          if (Array.isArray(node)) { for (const item of node) fixSchema(item); return; }
          if (Array.isArray(node.items)) {
            node.prefixItems = node.items;
            node.items = false;
            if (typeof node.$schema === "string" && node.$schema.includes("draft-07")) delete node.$schema;
          }
          for (const value of Object.values(node)) fixSchema(value);
        }

        function emit(text) {
          let body = text;
          try {
            const msg = JSON.parse(text);
            if (msg.result) fixSchema(msg.result);
            body = JSON.stringify(msg);
          } catch {}
          if (mode === "lsp") {
            const raw = Buffer.from(body, "utf8");
            process.stdout.write(`Content-Length: ${raw.length}\r\n\r\n`);
            process.stdout.write(raw);
          } else {
            process.stdout.write(body + "\n");
          }
        }

        function listen() {
          if (listening || !session) return;
          listening = true;
          (async () => {
            try {
              const listenHeaders = { Accept: "text/event-stream", "Mcp-Session-Id": session, ...extraHeaders };
              const res = await fetch(url, { headers: listenHeaders });
              if (res.ok && res.body) await readBody(res);
            } catch {}
            listening = false;
          })();
        }

        async function readBody(res) {
          const ctype = res.headers.get("content-type") || "";
          if (!ctype.includes("text/event-stream")) {
            const text = await res.text();
            if (text.trim()) emit(text);
            return;
          }
          const reader = res.body.getReader();
          const dec = new TextDecoder();
          let pending = "";
          while (true) {
            const { done, value } = await reader.read();
            if (done) break;
            pending += dec.decode(value, { stream: true });
            let sep;
            while ((sep = pending.indexOf("\n\n")) >= 0) {
              const block = pending.slice(0, sep);
              pending = pending.slice(sep + 2);
              const data = block.split("\n").filter((l) => l.startsWith("data:")).map((l) => l.slice(5).trim()).join("\n");
              if (data && data !== "[DONE]") emit(data);
            }
          }
        }

        function postJson(target, headers, body) {
          const u = new URL(target);
          const payload = Buffer.from(body, "utf8");
          return new Promise((resolve, reject) => {
            let settled = false;
            const finish = (value) => { if (!settled) { settled = true; resolve(value); } };
            const req = https.request({
              protocol: u.protocol,
              hostname: u.hostname,
              port: u.port || 443,
              path: u.pathname + u.search,
              method: "POST",
              family: 4,
              timeout: 20000,
              headers: { ...headers, Host: u.host, "Content-Length": payload.length }
            }, (res) => {
              const ctype = String(res.headers["content-type"] || "");
              if (ctype.includes("text/event-stream")) {
                let pending = "";
                const take = () => {
                  let sep;
                  while ((sep = pending.indexOf("\n\n")) >= 0) {
                    const block = pending.slice(0, sep);
                    pending = pending.slice(sep + 2);
                    const data = block.split("\n").filter((l) => l.startsWith("data:")).map((l) => l.slice(5).trim()).join("\n");
                    if (data && data !== "[DONE]") {
                      emit(data);
                      finish({ status: res.statusCode || 0, headers: res.headers, text: "", streamed: true });
                    }
                  }
                };
                res.on("data", (c) => { pending += c.toString("utf8"); take(); });
                res.on("end", () => {
                  if (pending.trim()) { pending += "\n\n"; take(); }
                  finish({ status: res.statusCode || 0, headers: res.headers, text: "", streamed: true });
                });
                return;
              }
              const chunks = [];
              res.on("data", (c) => chunks.push(c));
              res.on("end", () => finish({
                status: res.statusCode || 0,
                headers: res.headers,
                text: Buffer.concat(chunks).toString("utf8")
              }));
            });
            req.on("timeout", () => req.destroy(new Error("timeout")));
            req.on("error", (err) => { if (!settled) reject(err); });
            req.end(payload);
          });
        }
        async function handle(line) {
          let msg;
          try { msg = JSON.parse(line); } catch { return; }
          const headers = { "Content-Type": "application/json", Accept: "application/json, text/event-stream", ...extraHeaders };
          if (session) headers["Mcp-Session-Id"] = session;
          let res;
          let lastErr;
          for (let attempt = 0; attempt < 3; attempt++) {
            try {
              res = await postJson(url, headers, JSON.stringify(msg));
              lastErr = null;
              break;
            } catch (err) {
              lastErr = err;
              await new Promise((r) => setTimeout(r, 400 * (attempt + 1)));
            }
          }
          if (!res) {
            const cause = lastErr && lastErr.cause ? (lastErr.cause.code || lastErr.cause.message) : "";
            if (msg.id != null) emit(JSON.stringify({ jsonrpc: "2.0", id: msg.id, error: { code: -32000, message: String(lastErr && lastErr.message || lastErr) + (cause ? " (" + cause + ")" : "") } }));
            return;
          }
          const sid = res.headers["mcp-session-id"];
          if (sid) session = Array.isArray(sid) ? sid[0] : sid;
          if (res.status === 202) return;
          if (res.streamed) return;
          if (res.status < 200 || res.status >= 300) {
            if (res.text.trim()) emit(res.text);
            else if (msg.id != null) emit(JSON.stringify({ jsonrpc: "2.0", id: msg.id, error: { code: -32000, message: "ufp HTTP " + res.status } }));
            return;
          }
          if (res.text.trim()) emit(res.text);
        }

        function enqueue(line) {
          if (!line.trim()) return;
          chain = chain.then(() => handle(line)).catch((err) => {
            process.stderr.write(String(err) + "\n");
          });
        }

        function pump() {
          if (!mode) {
            const head = buf.subarray(0, Math.min(buf.length, 24)).toString("utf8").trimStart();
            if (!head) return;
            mode = head.startsWith("{") || head.startsWith("[") ? "line" : "lsp";
          }
          if (mode === "line") {
            while (true) {
              const i = buf.indexOf(0x0a);
              if (i < 0) break;
              const line = buf.subarray(0, i).toString("utf8").replace(/\r$/, "");
              buf = buf.subarray(i + 1);
              enqueue(line);
            }
            return;
          }
          while (true) {
            const headerEnd = buf.indexOf("\r\n\r\n");
            if (headerEnd < 0) break;
            const header = buf.subarray(0, headerEnd).toString("utf8");
            const match = /Content-Length:\s*(\d+)/i.exec(header);
            if (!match) break;
            const length = Number(match[1]);
            const start = headerEnd + 4;
            if (buf.length < start + length) break;
            const body = buf.subarray(start, start + length).toString("utf8");
            buf = buf.subarray(start + length);
            enqueue(body);
          }
        }

        process.stdin.on("data", (chunk) => { buf = Buffer.concat([buf, chunk]); pump(); });
        process.stdin.on("end", () => { chain.then(() => process.exit(0)); });
        """;
}
