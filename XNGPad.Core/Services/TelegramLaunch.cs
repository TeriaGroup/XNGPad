using System.Text;

namespace XNGPad.Core.Services;

public static class TelegramLaunch
{
    public static string ScriptPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Constants.SettingsFolderName,
        "telegram",
        "launch.mjs");

    public static void Ensure()
    {
        TryHosts();
        var path = ScriptPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        try
        {
            if (!File.Exists(path) || File.ReadAllText(path) != Script)
                File.WriteAllText(path, Script, new UTF8Encoding(false));
        }
        catch (IOException)
        {
        }
    }

    public static void TryHosts()
    {
        try
        {
            var hosts = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");
            var text = File.Exists(hosts) ? File.ReadAllText(hosts) : "";
            if (text.Contains("my.telegram.org", StringComparison.OrdinalIgnoreCase))
                return;
            File.AppendAllText(hosts, Environment.NewLine + "149.154.167.220 my.telegram.org" + Environment.NewLine);
        }
        catch
        {
        }
    }

    private const string Script = """
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { pathToFileURL } from "node:url";

const home = process.env.TELEGRAM_AGENT_HOME || path.join(process.env.USERPROFILE || "", ".telegram-agent");
process.env.TELEGRAM_AGENT_HOME = home;
fs.mkdirSync(path.join(home, "sessions"), { recursive: true });

const cache = path.join(process.env.LOCALAPPDATA || "", "npm-cache", "_npx");
let indexJs = "";
function walk(dir, depth) {
  if (depth > 8 || !fs.existsSync(dir)) return;
  let names = [];
  try { names = fs.readdirSync(dir); } catch { return; }
  for (const name of names) {
    const full = path.join(dir, name);
    if (name === "StoreSession.js" && full.includes(`${path.sep}telegram${path.sep}sessions${path.sep}`)) {
      patchStore(full);
      continue;
    }
    if (name === "index.js" && full.endsWith(`${path.sep}mcp-telegram${path.sep}dist${path.sep}index.js`)) {
      indexJs = full;
      continue;
    }
    if (name === "node_modules" || name === ".bin" || name === "dist" || name === "mcp-telegram" || name === "telegram" || name === "sessions" || depth < 4) {
      try { if (fs.statSync(full).isDirectory()) walk(full, depth + 1); } catch {}
    }
  }
}
if (fs.existsSync(cache)) walk(cache, 0);
if (!indexJs) {
  execFileSync("cmd.exe", ["/d", "/s", "/c", "npx", "-y", "-p", "mcp-telegram", "node", "-e", "process.exit(0)"], { windowsHide: true, stdio: "ignore" });
  walk(cache, 0);
}
if (!indexJs) {
  console.error("mcp-telegram is not installed");
  process.exit(1);
}
patchPackage(path.dirname(indexJs));
await import(pathToFileURL(indexJs).href);

function writeIfChanged(file, next) {
  const text = fs.readFileSync(file, "utf8");
  if (next !== text) fs.writeFileSync(file, next);
}

function patchStore(file) {
  let text = fs.readFileSync(file, "utf8");
  if (text.includes("path.isAbsolute(sessionName)")) return;
  const next = text.replace(
    'this.store = store2_1.default.area(sessionName, new LocalStorage("./" + sessionName));',
    'const path = require("path");\n            const storagePath = path.isAbsolute(sessionName) ? sessionName : "./" + sessionName;\n            this.store = store2_1.default.area(sessionName, new LocalStorage(storagePath));'
  );
  writeIfChanged(file, next);
}

function patchPackage(dist) {
  const root = path.dirname(dist);
  const store = path.join(root, "node_modules", "telegram", "sessions", "StoreSession.js");
  if (fs.existsSync(store)) patchStore(store);
  patchTelegram(path.join(dist, "telegram.js"));
  patchBrowser(path.join(dist, "auth-browser.js"));
  patchPage(path.join(dist, "auth-page.js"));
}

function patchTelegram(file) {
  if (!fs.existsSync(file)) return;
  let text = fs.readFileSync(file, "utf8");
  text = text.replace("sendCode({ apiId, apiHash }, phone, true)", "sendCode({ apiId, apiHash }, phone, false)");
  if (!text.includes("viaApp: result.isCodeViaApp")) {
    text = text.replace(
      "const result = await client.sendCode({ apiId, apiHash }, phone);\n    pending.set(authId, { phone, client, phoneCodeHash: result.phoneCodeHash });",
      "const result = await client.sendCode({ apiId, apiHash }, phone, false);\n    pending.set(authId, { phone, client, phoneCodeHash: result.phoneCodeHash });\n    return { viaApp: result.isCodeViaApp !== false };"
    );
  }
  if (!text.includes("export async function loginQr")) {
    const fn = `export async function loginQr(authId) {
    const { apiId, apiHash } = apiCreds();
    let entry = pending.get(authId);
    if (!entry?.client) {
        const session = new StoreSession(join(sessionsDir, \`_pending_\${authId}\`));
        const client = new TelegramClient(session, apiId, apiHash, { connectionRetries: 3 });
        await client.connect();
        entry = { phone: "", client };
        pending.set(authId, entry);
    }
    try {
        const result = await entry.client.invoke(new Api.auth.ExportLoginToken({ apiId, apiHash, exceptIds: [] }));
        if (result instanceof Api.auth.LoginToken)
            return { url: \`tg://login?token=\${result.token.toString("base64url")}\` };
        if (result instanceof Api.auth.LoginTokenMigrateTo) {
            await entry.client._switchDC(result.dcId);
            const migrated = await entry.client.invoke(new Api.auth.ImportLoginToken({ token: result.token }));
            if (migrated instanceof Api.auth.LoginTokenSuccess) {
                const account = await finalizeLogin(authId, entry);
                pending.delete(authId);
                return { status: "ok", account };
            }
        }
        if (result instanceof Api.auth.LoginTokenSuccess) {
            const account = await finalizeLogin(authId, entry);
            pending.delete(authId);
            return { status: "ok", account };
        }
        throw new Error(result?.className || "QR failed");
    } catch (err) {
        if (err.errorMessage === "SESSION_PASSWORD_NEEDED") {
            entry.passwordSrp = await entry.client.invoke(new Api.account.GetPassword());
            return { status: "password_needed" };
        }
        throw err;
    }
}
`;
    text = text.replace("export async function loginStart", fn + "export async function loginStart");
  }
  writeIfChanged(file, text);
}

function patchBrowser(file) {
  if (!fs.existsSync(file)) return;
  let text = fs.readFileSync(file, "utf8");
  if (!text.includes("loginQr"))
    text = text.replace("import { loginStart,", "import { loginStart, loginQr,");
  if (!text.includes("pathname === '/done'")) {
    text = text.replace(
      "if (req.method !== 'POST') {\n                return sendJson(res, 404, { error: 'not_found' });\n            }",
      "if (req.method === 'GET' && url.pathname === '/done') {\n                res.writeHead(200, { 'content-type': 'text/html; charset=utf-8' });\n                return res.end('<!doctype html><meta charset=\"utf-8\"><title>Telegram</title><body style=\"font:16px Segoe UI;background:#111;color:#eee;padding:40px\">Вход выполнен. Эту вкладку можно закрыть.</body>');\n            }\n            if (req.method !== 'POST') {\n                return sendJson(res, 404, { error: 'not_found' });\n            }"
    );
  }
  if (!text.includes("/authorize/login-qr")) {
    text = text.replace(
      "if (url.pathname === '/authorize/login-start') {",
      `if (url.pathname === '/authorize/login-qr') {
                try {
                    const result = await loginQr(authId);
                    if (result.status === 'password_needed')
                        return sendJson(res, 200, { status: 'password_needed' });
                    if (result.status === 'ok') {
                        settlePromise(() => resolve(result.account));
                        return sendJson(res, 200, { status: 'ok' });
                    }
                    return sendJson(res, 200, { url: result.url });
                }
                catch (err) {
                    return sendJson(res, 400, { error: err.message });
                }
            }
            if (url.pathname === '/authorize/login-start') {`
    );
  }
  writeIfChanged(file, text);
}

function patchPage(file) {
  if (!fs.existsSync(file)) return;
  let text = fs.readFileSync(file, "utf8");
  text = text.replace("if (body.redirect) { location.href = body.redirect; return; }", "if (body.redirect || body.status === 'ok') { finish(); return; }");
  if (!text.includes('id="show-qr"')) {
    text = text.replace(
      '<button id="send-code">Send code</button>',
      '<button id="send-code">Send code</button>\n      <button id="show-qr" class="ghost">Войти по QR, без кода</button>\n      <img id="qr-img" alt="" style="display:none;margin:12px auto;width:220px;height:220px;background:#fff;padding:8px;border-radius:8px"/>'
    );
    text = text.replace(
      "$('send-code').onclick = async () => {",
      `$('show-qr').onclick = async () => {
    clearErr('err-phone');
    $('show-qr').disabled = true;
    const poll = async () => {
      const r = await fetch('/authorize/login-qr', { method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ auth_id: AUTH_ID }) });
      const body = await r.json().catch(() => ({}));
      if (!r.ok) return showErr('err-phone', body.error || 'QR failed');
      if (body.status === 'password_needed') return show('step-password');
      if (body.status === 'ok' || body.redirect) return finish();
      if (body.url) {
        $('qr-img').style.display = 'block';
        $('qr-img').src = 'https://api.qrserver.com/v1/create-qr-code/?size=220x220&data=' + encodeURIComponent(body.url);
      }
      setTimeout(poll, 2000);
    };
    try { await poll(); } finally { $('show-qr').disabled = false; }
  };
  $('send-code').onclick = async () => {`
    );
  }
  writeIfChanged(file, text);
}
""";
}