using System.Text.Json.Nodes;
using XNGPad.Core.Models;
using XNGPad.Core.Services;

namespace XNGPad.Tests;

public class McpRegistryTests
{
    private const string Sample = """
        {
          "servers": [
            {
              "server": {
                "name": "io.github.foo/bar-mcp",
                "title": "Bar",
                "description": "Does bar things",
                "repository": { "url": "https://github.com/foo/bar" },
                "packages": [
                  {
                    "registryType": "npm",
                    "identifier": "@foo/bar-mcp",
                    "environmentVariables": [
                      { "name": "BAR_KEY", "isSecret": true, "isRequired": true }
                    ]
                  }
                ]
              }
            },
            {
              "server": {
                "name": "com.example/remote-only",
                "description": "Remote server",
                "remotes": [{ "type": "streamable-http", "url": "https://example.com/mcp" }]
              }
            },
            {
              "server": {
                "name": "skip.smithery/hidden",
                "remotes": [{ "url": "https://server.smithery.ai/foo" }]
              }
            }
          ]
        }
        """;

    [Fact]
    public void ParseServers_ReadsNpmAndRemote_SkipsSmitheryOnly()
    {
        var list = McpRegistry.ParseServers(Sample);
        Assert.Equal(2, list.Count);
        var npm = Assert.Single(list, x => x.Id == "bar-mcp");
        Assert.Equal("Bar", npm.DisplayName);
        Assert.Equal("@foo/bar-mcp", npm.Package);
        Assert.Equal("BAR_KEY", npm.TokenEnvKey);
        Assert.Equal("https://github.com/foo/bar", npm.GitHubUrl);
        var remote = Assert.Single(list, x => x.Id == "remote-only");
        Assert.Equal("https://example.com/mcp", remote.RemoteUrl);
        Assert.Equal("", remote.Package);
    }

    [Fact]
    public void Merge_CuratedWinsOnIdAndPackage()
    {
        var curated = new[]
        {
            new McpDef("github", "GitHub", "McpGithubHint", "https://github.com/x", "@modelcontextprotocol/server-github")
        };
        var remote = new[]
        {
            new McpDef("github", "Other GitHub", "", "https://github.com/y", "other-github"),
            new McpDef("other", "Other", "", "", "@modelcontextprotocol/server-github"),
            new McpDef("exa", "Exa", "", "", "exa-mcp-server")
        };
        var merged = McpRegistry.Merge(curated, remote);
        Assert.Equal(2, merged.Count);
        Assert.Equal("GitHub", merged[0].DisplayName);
        Assert.Equal("exa", merged[1].Id);
    }

    [Fact]
    public void Matches_RequiresAllTokens()
    {
        var def = new McpDef("github", "GitHub", "", "https://github.com/modelcontextprotocol/servers", "@modelcontextprotocol/server-github", Hint: "repos");
        Assert.True(McpRegistry.Matches(def, "git hub"));
        Assert.True(McpRegistry.Matches(def, "server-github"));
        Assert.False(McpRegistry.Matches(def, "git slack"));
        Assert.True(McpRegistry.Matches(def, ""));
    }

    [Fact]
    public void CreateNode_Remote_WritesUrl()
    {
        var def = new McpDef("r", "R", "", "", "", RemoteUrl: "https://example.com/mcp");
        var node = McpCatalog.CreateNode(def, new AppSettings());
        Assert.Equal("remote", node["type"]!.GetValue<string>());
        Assert.Equal("https://example.com/mcp", node["url"]!.GetValue<string>());
        Assert.Null(node["command"]);
    }

    [Fact]
    public void CreateNode_Github_StillLocalNpx()
    {
        var node = McpCatalog.CreateNode("github", new AppSettings(), "ghp_test");
        Assert.Equal("local", node["type"]!.GetValue<string>());
        Assert.Equal("ghp_test", node["environment"]!["GITHUB_PERSONAL_ACCESS_TOKEN"]!.GetValue<string>());
        var cmd = node["command"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert.Equal(["npx", "-y", "@modelcontextprotocol/server-github"], cmd);
    }

    [Fact]
    public void FormatHint_ShowsWhatItDoes_NotInstallMethod()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("ru");
        var def = new McpDef(
            "bolthub",
            "bolthub",
            "",
            "",
            "bolthub",
            Hint: "The bolthub API marketplace, L402 gateways");
        var hint = McpRegistry.FormatHint(def, loc);
        Assert.Contains("marketplace", hint, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("npx", hint, StringComparison.OrdinalIgnoreCase);
        var ru = McpRegistry.FormatHint(def, loc, new Dictionary<string, string>
        {
            [def.Hint!] = "Маркетплейс API bolthub и шлюзы L402"
        });
        Assert.Contains("Маркетплейс", ru);
        Assert.DoesNotContain("marketplace", ru, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FormatHint_CuratedUsesResx()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("ru");
        var def = McpCatalog.All.First(x => x.Id == "github");
        Assert.Equal(loc.Get(def.HintKey), McpRegistry.FormatHint(def, loc));
    }

    [Fact]
    public void ParseGoogleTranslate_JoinsSegments()
    {
        var json = """[[["Маркетплейс API ","The bolthub API ",null,null,3],["шлюзы","gateways",null,null,3]]]""";
        Assert.Equal("Маркетплейс API шлюзы", McpRegistry.ParseGoogleTranslate(json));
    }

    [Fact]
    public void ParseMyMemory_ReadsTranslatedText()
    {
        var json = """{"responseData":{"translatedText":"Маркетплейс API bolthub"},"responseStatus":200}""";
        Assert.Equal("Маркетплейс API bolthub", McpRegistry.ParseMyMemory(json));
        Assert.Null(McpRegistry.ParseMyMemory("""{"responseData":{"translatedText":"MYMEMORY WARNING: wait"}}"""));
    }

    [Fact]
    public void ParseLingva_ReadsTranslation()
    {
        Assert.Equal("Маркетплейс", McpRegistry.ParseLingva("""{"translation":"Маркетплейс"}"""));
    }
}
