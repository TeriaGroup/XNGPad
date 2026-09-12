using System.Text.Json.Nodes;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public enum McpExtra
{
    None,
    Searxng,
    Filesystem,
    Sqlite
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
    string? RemoteUrl = null);

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
        new("fetch", "Fetch", "McpFetchHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/fetch", "@modelcontextprotocol/server-fetch"),
        new("filesystem", "Filesystem", "McpFilesystemHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/filesystem", "@modelcontextprotocol/server-filesystem", McpExtra.Filesystem),
        new("gdrive", "Google Drive", "McpGdriveHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/gdrive", "@modelcontextprotocol/server-gdrive", GuideKey: "GuideGdrive", TokenUrl: "https://console.cloud.google.com/apis/library/drive.googleapis.com", TokenEnvKey: "GOOGLE_APPLICATION_CREDENTIALS"),
        new("git", "Git", "McpGitHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/git", "@modelcontextprotocol/server-git"),
        new("github", "GitHub", "McpGithubHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/github", "@modelcontextprotocol/server-github", GuideKey: "GuideGithub", TokenUrl: "https://github.com/settings/tokens", TokenEnvKey: "GITHUB_PERSONAL_ACCESS_TOKEN"),
        new("gitlab", "GitLab", "McpGitlabHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/gitlab", "@modelcontextprotocol/server-gitlab", GuideKey: "GuideGitlab", TokenUrl: "https://gitlab.com/-/user_settings/personal_access_tokens", TokenEnvKey: "GITLAB_PERSONAL_ACCESS_TOKEN"),
        new("google-maps", "Google Maps", "McpMapsHint", "https://github.com/modelcontextprotocol/servers/tree/main/src/google-maps", "@modelcontextprotocol/server-google-maps", GuideKey: "GuideMaps", TokenUrl: "https://console.cloud.google.com/apis/credentials", TokenEnvKey: "GOOGLE_MAPS_API_KEY"),
        new("kubernetes", "mcp-server-kubernetes", "McpK8sHint", "https://github.com/Flux159/mcp-server-kubernetes", "mcp-server-kubernetes"),
        new("markitdown", "MarkItDown", "McpMarkitdownHint", "https://github.com/microsoft/markitdown", "@microsoft/markitdown-mcp"),
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
        new("huggingface", "Hugging Face", "McpHfHint", "https://github.com/huggingface/huggingface.js", "@huggingface/mcp-server", GuideKey: "GuideGeneric", TokenUrl: "https://huggingface.co/settings/tokens", TokenEnvKey: "HF_TOKEN"),
        new("neon", "Neon", "McpNeonHint", "https://github.com/neondatabase/mcp-server-neon", "@neondatabase/mcp-server-neon", GuideKey: "GuideGeneric", TokenUrl: "https://console.neon.tech", TokenEnvKey: "NEON_API_KEY"),
        new("supabase", "Supabase", "McpSupabaseHint", "https://github.com/supabase-community/supabase-mcp", "@supabase/mcp-server-supabase", GuideKey: "GuideGeneric", TokenUrl: "https://supabase.com/dashboard/account/tokens", TokenEnvKey: "SUPABASE_ACCESS_TOKEN"),
        new("browserbase", "Browserbase", "McpBrowserbaseHint", "https://github.com/browserbase/mcp-server-browserbase", "@browserbasehq/mcp", GuideKey: "GuideGeneric", TokenUrl: "https://www.browserbase.com/", TokenEnvKey: "BROWSERBASE_API_KEY"),
        new("jina", "Jina", "McpJinaHint", "https://github.com/jina-ai/mcp", "jina-mcp-tools", GuideKey: "GuideGeneric", TokenUrl: "https://jina.ai/api-dashboard/", TokenEnvKey: "JINA_API_KEY"),
        new("mermaid", "Mermaid", "McpMermaidHint", "https://github.com/hustcc/mcp-mermaid", "mcp-mermaid"),
        new("hackernews", "Hacker News", "McpHnHint", "https://github.com/pskill9/hn-server", "hn-server"),
        new("qdrant", "Qdrant", "McpQdrantHint", "https://github.com/qdrant/mcp-server-qdrant", "mcp-server-qdrant"),
        new("elasticsearch", "Elasticsearch", "McpEsHint", "https://github.com/elastic/mcp-server-elasticsearch", "@elastic/mcp-server-elasticsearch"),
        new("time", "Time", "McpTimeHint", "https://github.com/yokingma/time-mcp", "time-mcp")
    ];

    public static JsonObject CreateNode(string id, AppSettings settings, string? secret = null)
    {
        var def = All.FirstOrDefault(x => x.Id == id) ?? throw new XNGPadException("ErrorGeneric");
        return CreateNode(def, settings, secret);
    }

    public static JsonObject CreateNode(McpDef def, AppSettings settings, string? secret = null)
    {
        JsonObject? env = null;
        if (!string.IsNullOrEmpty(def.TokenEnvKey) && !string.IsNullOrEmpty(secret))
        {
            env = new JsonObject { [def.TokenEnvKey] = secret };
        }

        if (string.IsNullOrEmpty(def.Package) && !string.IsNullOrEmpty(def.RemoteUrl))
        {
            var remote = new JsonObject
            {
                ["type"] = "remote",
                ["url"] = def.RemoteUrl,
                ["enabled"] = true
            };
            if (env != null)
                remote["environment"] = env;
            return remote;
        }

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

    private static JsonObject Local(List<string> command, JsonObject? env)
    {
        var cmd = new JsonArray();
        foreach (var s in command)
            cmd.Add(s);
        var node = new JsonObject
        {
            ["type"] = "local",
            ["command"] = cmd,
            ["enabled"] = true
        };
        if (env != null)
            node["environment"] = env;
        return node;
    }

    private static string FilesystemRoot()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var projects = Path.Combine(desktop, "projects");
        return Directory.Exists(projects)
            ? projects
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string SqlitePath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.SettingsFolderName);
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "mcp.sqlite");
    }
}
