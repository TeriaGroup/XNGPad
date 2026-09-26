using System.Text.Json.Nodes;
using XNGPad.Core;
using XNGPad.Core.Models;
using XNGPad.Core.Services;

namespace XNGPad.Tests;

public class OpenCodeServiceTests : IDisposable
{
    private readonly string _dir;
    private readonly OpenCodeService _svc = new();

    public OpenCodeServiceTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "xngpad-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_dir, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void Merge_DoesNotOverwriteForeignKeys()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "model": "keep-me",
              "agent": { "name": "demo" },
              "mcp": {
                "exa": { "type": "remote", "url": "https://exa.example" }
              }
            }
            """);

        _svc.Connect(path, "http://127.0.0.1:8888");

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("keep-me", root["model"]!.GetValue<string>());
        Assert.Equal("demo", root["agent"]!["name"]!.GetValue<string>());
        Assert.Equal("https://exa.example", root["mcp"]!["exa"]!["url"]!.GetValue<string>());
        Assert.Equal("local", root["mcp"]!["searxng"]!["type"]!.GetValue<string>());
        var cmd = root["mcp"]!["searxng"]!["command"]!.AsArray();
        Assert.Equal("npx", cmd[0]!.GetValue<string>());
        Assert.Equal("-y", cmd[1]!.GetValue<string>());
        Assert.Equal("mcp-searxng", cmd[2]!.GetValue<string>());
        Assert.Equal("http://127.0.0.1:8888", root["mcp"]!["searxng"]!["environment"]!["SEARXNG_URL"]!.GetValue<string>());
        Assert.Equal(Constants.SchemaUrl, root["$schema"]!.GetValue<string>());
    }

    [Fact]
    public void Connect_CreatesBackup()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """{ "model": "x" }""");

        var backup = _svc.Connect(path, "http://127.0.0.1:8888");

        Assert.True(File.Exists(backup));
        Assert.Matches(@"opencode\.json\.bak-xngpad-\d{8}-\d{6}$", Path.GetFileName(backup));
        Assert.Equal("""{ "model": "x" }""", File.ReadAllText(backup));
    }

    [Fact]
    public void BrokenJson_IsNotOverwritten()
    {
        var path = Path.Combine(_dir, "opencode.json");
        const string broken = "{ this is not json";
        File.WriteAllText(path, broken);

        var ex = Assert.Throws<BrokenOpenCodeJsonException>(() => _svc.Connect(path, "http://127.0.0.1:8888"));

        Assert.Equal(broken, File.ReadAllText(path));
        Assert.True(File.Exists(ex.BackupPath));
        Assert.Equal(broken, File.ReadAllText(ex.BackupPath));
        Assert.Equal(OpenCodeKind.InvalidJson, _svc.ReadStatus(path));
    }

    [Fact]
    public void Connect_CreatesFileWhenMissing()
    {
        var path = Path.Combine(_dir, "nested", "opencode.json");
        var backup = _svc.Connect(path, "http://127.0.0.1:9999");
        Assert.Equal("", backup);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("http://127.0.0.1:9999", root["mcp"]!["searxng"]!["environment"]!["SEARXNG_URL"]!.GetValue<string>());
        Assert.Equal(OpenCodeKind.Connected, _svc.ReadStatus(path));
    }

    [Fact]
    public void Disconnect_RemovesOnlySearxng()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "model": "keep-me",
              "mcp": {
                "exa": { "type": "remote" },
                "searxng": { "type": "local" }
              }
            }
            """);

        _svc.Disconnect(path);

        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.Equal("keep-me", root["model"]!.GetValue<string>());
        Assert.NotNull(root["mcp"]!["exa"]);
        Assert.Null(root["mcp"]!["searxng"]);
        Assert.Equal(OpenCodeKind.McpMissing, _svc.ReadStatus(path));
    }

    [Fact]
    public void CreateNode_WritesTokenToEnvironment()
    {
        var node = McpCatalog.CreateNode("github", new AppSettings(), "ghp_test");
        Assert.Equal("ghp_test", node["environment"]!["GITHUB_PERSONAL_ACCESS_TOKEN"]!.GetValue<string>());
    }

    [Fact]
    public void UpsertMcp_DoesNotRemoveOtherMcp()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """{ "mcp": { "exa": { "type": "remote" } } }""");
        _svc.UpsertMcp(path, "memory", McpCatalog.CreateNode("memory", new AppSettings()));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.NotNull(root["mcp"]!["exa"]);
        Assert.NotNull(root["mcp"]!["memory"]);
        Assert.True(_svc.HasMcp(path, "memory"));
        _svc.RemoveMcp(path, "memory");
        Assert.False(_svc.HasMcp(path, "memory"));
        Assert.True(_svc.HasMcp(path, "exa"));
    }

    [Fact]
    public void Disconnect_BrokenJson_DoesNotOverwrite()
    {
        var path = Path.Combine(_dir, "opencode.json");
        const string broken = "[1, 2";
        File.WriteAllText(path, broken);

        Assert.Throws<BrokenOpenCodeJsonException>(() => _svc.Disconnect(path));
        Assert.Equal(broken, File.ReadAllText(path));
    }

    [Fact]
    public void RepairTelegram_RemovesStart_LeavesOtherMcp()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "mcp": {
                "exa": { "type": "remote", "headers": { "x-api-key": "keep" } },
                "telegram": {
                  "type": "local",
                  "command": ["npx", "-y", "mcp-telegram", "start"]
                }
              }
            }
            """);
        Assert.True(_svc.RepairTelegram(path));
        Assert.False(_svc.RepairTelegram(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var cmd = root["mcp"]!["telegram"]!["command"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert.EndsWith("launch.mjs", cmd[^1]);
        Assert.DoesNotContain("start", cmd);
        Assert.Equal("keep", root["mcp"]!["exa"]!["headers"]!["x-api-key"]!.GetValue<string>());
    }

    [Fact]
    public void RepairMcp_FixesFetchAndHuggingFace_DisablesDeadRemotes()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "mcp": {
                "fetch": { "type": "local", "command": ["npx", "-y", "@modelcontextprotocol/server-fetch"], "enabled": true },
                "huggingface": { "type": "local", "command": ["npx", "-y", "@huggingface/mcp-server"], "environment": { "HF_TOKEN": "hf_test" } },
                "agentic-news": { "type": "remote", "url": "https://api.agentic-news.ai/mcp", "enabled": true },
                "memory": { "type": "local", "command": ["npx", "-y", "@modelcontextprotocol/server-memory"], "enabled": true }
              }
            }
            """);
        Assert.True(_svc.RepairMcp(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var fetch = root["mcp"]!["fetch"]!["command"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert.Contains("mcp-fetch", fetch);
        Assert.DoesNotContain(fetch, x => x.Contains("server-fetch"));
        Assert.Equal("local", root["mcp"]!["huggingface"]!["type"]!.GetValue<string>());
        Assert.Contains("Bearer hf_test", root["mcp"]!["huggingface"]!["environment"]!["MCP_REMOTE_HEADERS"]!.GetValue<string>());
        Assert.Null(root["mcp"]!["agentic-news"]);
        Assert.Equal(120000, root["mcp"]!["memory"]!["timeout"]!.GetValue<int>());
    }

    [Fact]
    public void RepairMcp_WrapsUfpInsteadOfRemoving()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "mcp": {
                "ufp": { "type": "remote", "url": "https://agenticfabricationnetwork.ai/mcp", "enabled": true }
              }
            }
            """);
        Assert.True(_svc.RepairMcp(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var ufp = root["mcp"]!["ufp"]!.AsObject();
        Assert.Equal("local", ufp["type"]!.GetValue<string>());
        var cmd = ufp["command"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray();
        Assert.Contains("schema-proxy.mjs", cmd[^1]);
        Assert.False(_svc.RepairMcp(path));
    }

    [Fact]
    public void RepairMcp_WrapsRemoteHeaders()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "mcp": {
                "exa": { "type": "remote", "url": "https://mcp.exa.ai/mcp", "headers": { "x-api-key": "keep" } }
              }
            }
            """);
        Assert.True(_svc.RepairMcp(path));
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var exa = root["mcp"]!["exa"]!.AsObject();
        Assert.Equal("local", exa["type"]!.GetValue<string>());
        Assert.Contains("schema-proxy.mjs", exa["command"]!.AsArray().Last()!.GetValue<string>());
        Assert.Contains("keep", exa["environment"]!["MCP_REMOTE_HEADERS"]!.GetValue<string>());
        Assert.Contains("exa", _svc.ListRemoteIds(path));
    }

    [Fact]
    public void DisableUnresponsive_TurnsOffDeadCommand()
    {
        var path = Path.Combine(_dir, "opencode.json");
        File.WriteAllText(path, """
            {
              "mcp": {
                "dead": { "type": "local", "command": ["node", "-e", "process.exit(1)"], "enabled": true },
                "kept": { "type": "local", "command": ["node", "-e", "process.exit(1)"], "enabled": false }
              }
            }
            """);
        var off = _svc.DisableUnresponsive(path, 4000);
        Assert.Contains("dead", off);
        Assert.DoesNotContain("kept", off);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        Assert.False(root["mcp"]!["dead"]!["enabled"]!.GetValue<bool>());
        Assert.False(root["mcp"]!["kept"]!["enabled"]!.GetValue<bool>());
    }
}
