using System.Text.Json.Nodes;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public enum McpExtra
{
    None,
    Searxng,
    Filesystem,
    Sqlite,
    Telegram,
    Kompas,
    OfficeExcel,
    OfficeWord,
    OfficePpt,
    Ufp,
    Matlab,
    Mathcad,
    Mermaid
}

public sealed record McpDef(
    string Id,
    string DisplayName,
    string HintKey,
    string GitHubUrl,
    string Package,
    McpExtra Extra = McpExtra.None,
    string? GuideKey = null,
    string? TokenUrl = null,
    string? TokenEnvKey = null,
    bool TokenAsArg = false,
    string? Hint = null,
    string? RemoteUrl = null,
    string? TokenEnvKey2 = null,
    string? TokenHeader = null,
    string? TokenHeaderPrefix = null);

public static class McpCatalog
{
    public static IReadOnlyList<McpDef> All { get; } =
    [
        new("arxiv", "arxiv-mcp-server", "McpArxivHint", "https://github.com/blazickjp/arxiv-mcp-server", "arxiv-mcp-server"),
        new("brave-search", "Brave Search", "McpBraveHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/brave-search", "@modelcontextprotocol/server-brave-search", GuideKey: "GuideBrave", TokenUrl: "https://brave.com/search/api/", TokenEnvKey: "BRAVE_API_KEY"),
        new("context7", "Context7", "McpContext7Hint", "https://github.com/upstash/context7", "@upstash/context7-mcp"),
        new("desktop-commander", "Desktop Commander", "McpDesktopHint", "https://github.com/wonderwhy-er/DesktopCommanderMCP", "@wonderwhy-er/desktop-commander"),
        new("docker-mcp", "mcp-server-docker", "McpDockerMcpHint", "https://github.com/ckreiling/mcp-server-docker", "mcp-server-docker"),
        new("duckduckgo", "DuckDuckGo", "McpDdgHint", "https://github.com/nickclyde/duckduckgo-mcp-server", "duckduckgo-mcp-server"),
        new("everything", "Everything", "McpEverythingHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/everything", "@modelcontextprotocol/server-everything"),
        new("fetch", "Fetch", "McpFetchHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/fetch", "mcp-fetch"),
        new("filesystem", "Filesystem", "McpFilesystemHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem", "@modelcontextprotocol/server-filesystem", McpExtra.Filesystem),
        new("gdrive", "Google Drive", "McpGdriveHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/gdrive", "@modelcontextprotocol/server-gdrive", GuideKey: "GuideGdrive", TokenUrl: "https://console.cloud.google.com/apis/library/drive.googleapis.com", TokenEnvKey: "GOOGLE_APPLICATION_CREDENTIALS"),
        new("git", "Git", "McpGitHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/git", ""),
        new("github", "GitHub", "McpGithubHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/github", "@modelcontextprotocol/server-github", GuideKey: "GuideGithub", TokenUrl: "https://github.com/settings/tokens", TokenEnvKey: "GITHUB_PERSONAL_ACCESS_TOKEN"),
        new("gitlab", "GitLab", "McpGitlabHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/gitlab", "@modelcontextprotocol/server-gitlab", GuideKey: "GuideGitlab", TokenUrl: "https://gitlab.com/-/user_settings/personal_access_tokens", TokenEnvKey: "GITLAB_PERSONAL_ACCESS_TOKEN"),
        new("google-maps", "Google Maps", "McpMapsHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/google-maps", "@modelcontextprotocol/server-google-maps", GuideKey: "GuideMaps", TokenUrl: "https://console.cloud.google.com/apis/credentials", TokenEnvKey: "GOOGLE_MAPS_API_KEY"),
        new("kubernetes", "mcp-server-kubernetes", "McpK8sHint", "https://github.com/Flux159/mcp-server-kubernetes", "mcp-server-kubernetes"),

        new("memory", "Memory", "McpMemoryHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/memory", "@modelcontextprotocol/server-memory"),
        new("playwright", "Playwright", "McpPlaywrightHint", "https://github.com/microsoft/playwright-mcp", "@playwright/mcp"),
        new("postgres", "PostgreSQL", "McpPostgresHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/postgres", "@modelcontextprotocol/server-postgres", GuideKey: "GuidePostgres", TokenEnvKey: "POSTGRES_CONNECTION_STRING", TokenAsArg: true),
        new("puppeteer", "Puppeteer", "McpPuppeteerHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/puppeteer", "@modelcontextprotocol/server-puppeteer"),
        new("redis", "Redis", "McpRedisHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/redis", "@modelcontextprotocol/server-redis", GuideKey: "GuideRedis", TokenEnvKey: "REDIS_URL", TokenAsArg: true),
        new("searxng", "mcp-searxng", "McpSearxngHint", "https://github.com/ihor-sokoliuk/mcp-searxng", "mcp-searxng", McpExtra.Searxng),
        new("sentry", "Sentry", "McpSentryHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/sentry", "@modelcontextprotocol/server-sentry", GuideKey: "GuideSentry", TokenUrl: "https://sentry.io/settings/account/api/auth-tokens/", TokenEnvKey: "SENTRY_AUTH_TOKEN"),
        new("sequential-thinking", "Sequential Thinking", "McpSequentialHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/sequentialthinking", "@modelcontextprotocol/server-sequential-thinking"),
        new("slack", "Slack", "McpSlackHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/slack", "@modelcontextprotocol/server-slack", GuideKey: "GuideSlack", TokenUrl: "https://api.slack.com/apps", TokenEnvKey: "SLACK_BOT_TOKEN"),
        new("sqlite", "SQLite", "McpSqliteHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/sqlite", "@modelcontextprotocol/server-sqlite", McpExtra.Sqlite),
        new("wikipedia", "Wikipedia", "McpWikiHint", "https://github.com/Rudra-ravi/wikipedia-mcp", "wikipedia-mcp"),
        new("youtube", "YouTube", "McpYoutubeHint", "https://github.com/anaisbetts/mcp-youtube", "@anaisbetts/mcp-youtube"),
        new("firecrawl", "Firecrawl", "McpFirecrawlHint", "https://github.com/firecrawl/firecrawl-mcp-server", "firecrawl-mcp", GuideKey: "GuideGeneric", TokenUrl: "https://www.firecrawl.dev/app/api-keys", TokenEnvKey: "FIRECRAWL_API_KEY"),
        new("exa", "Exa", "McpExaHint", "https://github.com/exa-labs/exa-mcp-server", "exa-mcp-server", GuideKey: "GuideGeneric", TokenUrl: "https://dashboard.exa.ai/api-keys", TokenEnvKey: "EXA_API_KEY"),
        new("tavily", "Tavily", "McpTavilyHint", "https://github.com/tavily-ai/tavily-mcp", "tavily-mcp", GuideKey: "GuideGeneric", TokenUrl: "https://app.tavily.com/home", TokenEnvKey: "TAVILY_API_KEY"),
        new("notion", "Notion", "McpNotionHint", "https://github.com/makenotion/notion-mcp-server", "@notionhq/notion-mcp-server", GuideKey: "GuideGeneric", TokenUrl: "https://www.notion.so/my-integrations", TokenEnvKey: "NOTION_TOKEN"),
        new("chrome-devtools", "Chrome DevTools", "McpChromeHint", "https://github.com/ChromeDevTools/chrome-devtools-mcp", "chrome-devtools-mcp"),
        new("mongodb", "MongoDB", "McpMongoHint", "https://github.com/mongodb-js/mongodb-mcp-server", "mongodb-mcp-server"),
        new("figma", "Figma", "McpFigmaHint", "https://github.com/GLips/Figma-Context-MCP", "figma-developer-mcp", GuideKey: "GuideGeneric", TokenUrl: "https://www.figma.com/developers/api#access-tokens", TokenEnvKey: "FIGMA_API_KEY"),
        new("huggingface", "Hugging Face", "McpHfHint", "https://huggingface.co/settings/mcp", "", GuideKey: "GuideGeneric", TokenUrl: "https://huggingface.co/settings/tokens", TokenEnvKey: "HF_TOKEN", RemoteUrl: "https://hf.co/mcp", TokenHeader: "Authorization", TokenHeaderPrefix: "Bearer "),
        new("neon", "Neon", "McpNeonHint", "https://github.com/neondatabase/mcp-server-neon", "@neondatabase/mcp-server-neon", GuideKey: "GuideGeneric", TokenUrl: "https://console.neon.tech", TokenEnvKey: "NEON_API_KEY"),
        new("supabase", "Supabase", "McpSupabaseHint", "https://github.com/supabase-community/supabase-mcp", "@supabase/mcp-server-supabase", GuideKey: "GuideGeneric", TokenUrl: "https://supabase.com/dashboard/account/tokens", TokenEnvKey: "SUPABASE_ACCESS_TOKEN"),
        new("browserbase", "Browserbase", "McpBrowserbaseHint", "https://github.com/browserbase/mcp-server-browserbase", "@browserbasehq/mcp", GuideKey: "GuideGeneric", TokenUrl: "https://www.browserbase.com/", TokenEnvKey: "BROWSERBASE_API_KEY"),
        new("jina", "Jina", "McpJinaHint", "https://github.com/jina-ai/mcp", "jina-mcp-tools", GuideKey: "GuideGeneric", TokenUrl: "https://jina.ai/api-dashboard/", TokenEnvKey: "JINA_API_KEY"),
        new("mermaid", "Mermaid", "McpMermaidHint", "https://github.com/hustcc/mcp-mermaid", "mcp-mermaid", McpExtra.Mermaid),
        new("hackernews", "Hacker News", "McpHnHint", "https://github.com/pskill9/hn-server", "hn-server"),
        new("qdrant", "Qdrant", "McpQdrantHint", "https://github.com/qdrant/mcp-server-qdrant", "mcp-server-qdrant"),
        new("elasticsearch", "Elasticsearch", "McpEsHint", "https://github.com/elastic/mcp-server-elasticsearch", "@elastic/mcp-server-elasticsearch", GuideKey: "GuideElasticsearch", TokenUrl: "https://www.elastic.co/docs/deploy-manage/api-keys", TokenEnvKey: "ES_URL", TokenEnvKey2: "ES_API_KEY"),
        new("time", "Time", "McpTimeHint", "https://github.com/yokingma/time-mcp", "time-mcp"),
        new("telegram", "Telegram", "McpTelegramHint", "https://github.com/beautyfree/mcp-telegram", "mcp-telegram", McpExtra.Telegram, GuideKey: "GuideTelegram", TokenUrl: "https://my.telegram.org/apps", TokenEnvKey: "API_ID", TokenEnvKey2: "API_HASH"),
        new("kompas-3d", "КОМПАС-3D", "McpKompasHint", "https://github.com/dwnmf/KOMPAS-3D-MCP-bin", "", McpExtra.Kompas, GuideKey: "GuideKompas", TokenUrl: "https://kompasmcp.ru/"),
        new("office-excel", "Excel", "McpExcelHint", "https://github.com/dosev-ai/mcp-office", "mcp-office", McpExtra.OfficeExcel, GuideKey: "GuideOffice"),
        new("office-word", "Word", "McpWordHint", "https://github.com/dosev-ai/mcp-office", "mcp-office", McpExtra.OfficeWord, GuideKey: "GuideOffice"),
        new("office-ppt", "PowerPoint", "McpPptHint", "https://github.com/dosev-ai/mcp-office", "mcp-office", McpExtra.OfficePpt, GuideKey: "GuideOffice"),
        new("ufp", "UFP", "McpUfpHint", "https://agenticfabricationnetwork.ai/", "", McpExtra.Ufp, GuideKey: "GuideUfp", RemoteUrl: UfpProxy.Url),
        new("matlab", "MATLAB", "McpMatlabHint", "https://github.com/matlab/matlab-mcp-server", "", McpExtra.Matlab, GuideKey: "GuideMatlab"),
        new("mathcad", "Mathcad", "McpMathcadHint", "https://github.com/fqfqfqff/mathcad-prime-mcp", "", McpExtra.Mathcad, GuideKey: "GuideMathcad")
    ];

    public static JsonObject CreateNode(string id, AppSettings settings, string? secret = null)
    {
        var def = All.FirstOrDefault(x => x.Id == id) ?? throw new XNGPadException("ErrorGeneric");
        return CreateNode(def, settings, secret);
    }

    public static JsonObject CreateNode(McpDef def, AppSettings settings, string? secret = null, string? secret2 = null)
    {
        JsonObject? env = null;
        if (!string.IsNullOrEmpty(def.TokenEnvKey) && !string.IsNullOrEmpty(secret))
            env = new JsonObject { [def.TokenEnvKey] = secret };
        if (!string.IsNullOrEmpty(def.TokenEnvKey2) && !string.IsNullOrEmpty(secret2))
        {
            env ??= new JsonObject();
            env[def.TokenEnvKey2] = secret2;
        }

        if (def.Id == "elasticsearch")
        {
            env ??= new JsonObject();
            env["OTEL_LOG_LEVEL"] = "none";
        }

        if (def.Extra == McpExtra.Telegram)
        {
            env = new JsonObject();
            if (!string.IsNullOrEmpty(secret))
                env["TELEGRAM_API_ID"] = secret;
            if (!string.IsNullOrEmpty(secret2))
                env["TELEGRAM_API_HASH"] = secret2;
            TelegramLaunch.Ensure();
            return Local([NodeExe(), TelegramLaunch.ScriptPath()], env);
        }

        if (def.Extra == McpExtra.Kompas)
            return KompasNode(settings);

        if (def.Extra == McpExtra.Ufp)
            return UfpProxy.Node();

        if (def.Extra == McpExtra.Matlab)
            return MatlabNode(settings);

        if (def.Extra == McpExtra.Mathcad)
            return MathcadNode();

        if (def.Extra == McpExtra.Mermaid)
            return MermaidNode();

        if (def.Extra is McpExtra.OfficeExcel or McpExtra.OfficeWord or McpExtra.OfficePpt)
            return OfficeNode(def.Extra, env);

        if (string.IsNullOrEmpty(def.Package) && !string.IsNullOrEmpty(def.RemoteUrl))
            return Remote(def, secret, env);

        if (string.IsNullOrEmpty(def.Package))
            throw new XNGPadException("ErrorMcpNoPackage", def.DisplayName);

        var args = new List<string> { "npx", "-y", def.Package };
        switch (def.Extra)
        {
            case McpExtra.Searxng:
                env ??= new JsonObject();
                env["SEARXNG_URL"] = settings.ResolvedUrl();
                break;
            case McpExtra.Filesystem:
                args.Add(FilesystemRoot());
                break;
            case McpExtra.Sqlite:
                args.Add(SqlitePath());
                break;
        }

        if (def.TokenAsArg && !string.IsNullOrEmpty(secret))
            args.Add(secret);

        return Local(args, env);
    }

    public static string KompasDir() => Path.GetDirectoryName(KompasExePath())!;

    public static string KompasProxyPath() => Path.Combine(KompasDir(), "schema-proxy.mjs");

    public static string? ResolveStudyExe(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return null;
        var direct = Path.Combine(folder, "kStudy.exe");
        if (File.Exists(direct))
            return direct;
        var bin = Path.Combine(folder, "Bin", "kStudy.exe");
        return File.Exists(bin) ? bin : null;
    }

    private static JsonObject KompasNode(AppSettings settings)
    {
        var dir = KompasDir();
        var env = new JsonObject
        {
            ["KOMPAS_MCP_EXE"] = KompasExePath()
        };
        var study = ResolveStudyExe(settings.KompasInstallDir) ?? ResolveStudyExe(@"C:\APPs\ascon\kompas") ?? ResolveStudyExe(@"C:\APPs\kompas");
        if (study != null)
            env["KOMPAS_EXE"] = study;
        var node = Local([NodeExe(), KompasProxyPath()], env);
        node["cwd"] = dir;
        return node;
    }

    private static string NodeExe()
    {
        var candidate = Path.Combine(AppPaths.Current.ProgramFiles, "nodejs", "node.exe");
        return File.Exists(candidate) ? candidate : "node";
    }

    public static string KompasExePath()
    {
        return Path.Combine(
            AppPaths.Current.ApplicationData,
            Constants.SettingsFolderName,
            "kompas-mcp",
            "kompas-mcp.exe");
    }

    public static bool NeedsNpx(McpDef def) =>
        def.Extra is not (McpExtra.Kompas or McpExtra.OfficeExcel or McpExtra.OfficeWord or McpExtra.OfficePpt or McpExtra.Ufp or McpExtra.Matlab or McpExtra.Mathcad or McpExtra.Mermaid)
        && (def.Package.Length > 0 || def.Extra == McpExtra.Telegram);

    public static bool NeedsPython(McpDef def) =>
        def.Extra is McpExtra.OfficeExcel or McpExtra.OfficeWord or McpExtra.OfficePpt;

    public static bool NeedsKompasBin(McpDef def) => def.Extra == McpExtra.Kompas;

    public static bool NeedsMatlabBin(McpDef def) => def.Extra == McpExtra.Matlab;

    public static bool NeedsMathcadMcp(McpDef def) => def.Extra == McpExtra.Mathcad;

    public static bool NeedsMermaid(McpDef def) => def.Extra == McpExtra.Mermaid;

    public static string MermaidDir() => Path.Combine(
        AppPaths.Current.ApplicationData,
        Constants.SettingsFolderName,
        "mermaid");

    public static string MermaidEntry() => Path.Combine(MermaidDir(), "node_modules", "mcp-mermaid", "build", "index.js");

    public static string MathcadDir() => Path.Combine(
        AppPaths.Current.ApplicationData,
        Constants.SettingsFolderName,
        "mathcad-prime");

    public static string MathcadPython() => Path.Combine(MathcadDir(), "venv", "Scripts", "python.exe");

    public static string MathcadSrc()
    {
        var fallback = Path.Combine(MathcadDir(), "src");
        if (!Directory.Exists(MathcadDir()))
            return fallback;
        try
        {
            var server = Directory.EnumerateFiles(MathcadDir(), "server.py", SearchOption.AllDirectories)
                .FirstOrDefault(p => p.Contains($"{Path.DirectorySeparatorChar}mathcad_prime_mcp{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));
            var src = server == null ? null : Directory.GetParent(server)?.Parent?.FullName;
            return string.IsNullOrWhiteSpace(src) ? fallback : src;
        }
        catch (IOException)
        {
            return fallback;
        }
        catch (UnauthorizedAccessException)
        {
            return fallback;
        }
    }

    public static string MatlabDir() => Path.Combine(
        AppPaths.Current.ApplicationData,
        Constants.SettingsFolderName,
        "matlab");

    public static string MatlabExePath() => Path.Combine(MatlabDir(), "matlab-mcp-server.exe");

    public static string MatlabSetupStampPath() => Path.Combine(MatlabDir(), "setup.ok");

    public static IReadOnlyList<string> MatlabSetupArgs(string root) =>
    [
        "--setup-matlab",
        "--disable-telemetry=true",
        "--matlab-display-mode=desktop",
        "--matlab-root=" + root
    ];

    public static bool IsMatlabMcpToolboxInstalled()
    {
        foreach (var root in MatlabAddonRoots())
        {
            if (ContainsShareSession(root))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> MatlabAddonRoots()
    {
        var appData = AppPaths.Current.ApplicationData;
        var docs = AppPaths.Current.MyDocuments;
        yield return Path.Combine(appData, "MathWorks", "MATLAB Add-Ons");
        yield return Path.Combine(docs, "MATLAB", "Add-Ons");
        yield return Path.Combine(docs, "MATLAB Add-Ons");
    }

    private static bool ContainsShareSession(string root)
    {
        if (!Directory.Exists(root))
            return false;
        try
        {
            return Directory.EnumerateFiles(root, "shareMATLABSession.m", SearchOption.AllDirectories).Any();
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public static string? FindMatlabInstall(string? preferred)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
        {
            var exact = ResolveMatlabRoot(preferred);
            if (exact != null)
                return exact;
        }

        return ResolveMatlabRoot(null);
    }

    public static string? ResolveMatlabRoot(string? folder)
    {
        if (IsMatlabRoot(folder))
            return folder;
        if (!string.IsNullOrWhiteSpace(folder))
        {
            var parent = Directory.GetParent(folder)?.FullName;
            if (IsMatlabRoot(parent))
                return parent;
            return null;
        }

        var bases = new[]
        {
            Path.Combine(AppPaths.Current.ProgramFiles, "MATLAB"),
            Path.Combine(AppPaths.Current.ProgramFilesX86, "MATLAB")
        };
        return bases
            .Where(Directory.Exists)
            .SelectMany(Directory.GetDirectories)
            .Where(IsMatlabRoot)
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool IsMatlabRoot(string? folder) =>
        !string.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder, "bin", "matlab.exe"));

    private static JsonObject MatlabNode(AppSettings settings)
    {
        var root = FindMatlabInstall(settings.MatlabInstallDir);
        var cmd = new List<string>
        {
            MatlabExePath(),
            "--disable-telemetry=true",
            "--matlab-display-mode=desktop",
            "--initial-working-folder=" + FilesystemRoot()
        };
        if (root != null)
            cmd.Add("--matlab-root=" + root);
        return Local(cmd, null);
    }

    private static JsonObject MathcadNode()
    {
        var env = new JsonObject
        {
            ["PYTHONPATH"] = MathcadSrc(),
            ["PYTHONIOENCODING"] = "utf-8",
            ["PYTHONUNBUFFERED"] = "1"
        };
        return Local([MathcadPython(), "-m", "mathcad_prime_mcp.server"], env);
    }

    private static JsonObject MermaidNode() => Local([NodeExe(), MermaidEntry()], null);

    private static JsonObject OfficeNode(McpExtra extra, JsonObject? env)
    {
        var root = FilesystemRoot();
        string module;
        string allow;
        string write;
        switch (extra)
        {
            case McpExtra.OfficeWord:
                module = "wordmcp.server";
                allow = "WORD_ALLOWLIST_ROOTS";
                write = "WORD_ENABLE_WRITE";
                break;
            case McpExtra.OfficePpt:
                module = "pptmcp.server";
                allow = "PPT_ALLOWLIST_ROOTS";
                write = "PPT_ENABLE_WRITE";
                break;
            default:
                module = "excelmcp.server";
                allow = "EXCEL_ALLOWLIST_ROOTS";
                write = "EXCEL_ENABLE_WRITE";
                break;
        }

        env ??= new JsonObject();
        env[allow] = root;
        env[write] = "true";
        var cmd = new List<string>();
        cmd.AddRange(InstallService.PythonCommand());
        cmd.Add("-m");
        cmd.Add(module);
        return Local(cmd, env);
    }

    private static JsonObject Remote(McpDef def, string? secret, JsonObject? env)
    {
        JsonObject? headers = null;
        if (!string.IsNullOrEmpty(def.TokenHeader) && !string.IsNullOrEmpty(secret))
            headers = new JsonObject { [def.TokenHeader] = (def.TokenHeaderPrefix ?? "") + secret };
        var node = UfpProxy.Node(def.RemoteUrl!, headers);
        if (headers == null && env != null)
        {
            var merged = node["environment"] as JsonObject ?? new JsonObject();
            foreach (var pair in env)
                merged[pair.Key] = pair.Value?.DeepClone();
            node["environment"] = merged;
        }

        return node;
    }

    private static JsonObject Local(List<string> command, JsonObject? env)
    {
        var cmd = new JsonArray();
        foreach (var s in command)
            cmd.Add(s);
        var node = new JsonObject
        {
            ["type"] = "local",
            ["command"] = cmd,
            ["enabled"] = true,
            ["timeout"] = 120000
        };
        if (env != null)
            node["environment"] = env;
        return node;
    }

    private static string FilesystemRoot()
    {
        var desktop = AppPaths.Current.Desktop;
        var projects = Path.Combine(desktop, "projects");
        return Directory.Exists(projects)
            ? projects
            : AppPaths.Current.UserProfile;
    }

    private static string SqlitePath()
    {
        var dir = Path.Combine(
            AppPaths.Current.ApplicationData,
            Constants.SettingsFolderName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "mcp.sqlite");
    }
}
