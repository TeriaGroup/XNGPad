using XNGPad.Core.Services;

namespace XNGPad.Tests;

public class SearxngSettingsPatcherTests
{
    [Fact]
    public void AddsJsonToList()
    {
        var yaml = """
            search:
              safe_search: 0
              formats:
                - html
            """;
        Assert.False(SearxngSettingsPatcher.HasJsonFormat(yaml));
        var patched = SearxngSettingsPatcher.EnableJsonFormat(yaml);
        Assert.True(SearxngSettingsPatcher.HasJsonFormat(patched));
        Assert.Contains("- json", patched);
        Assert.Contains("- html", patched);
    }

    [Fact]
    public void IdempotentWhenJsonPresent()
    {
        var yaml = """
            search:
              formats:
                - html
                - json
            """;
        var patched = SearxngSettingsPatcher.EnableJsonFormat(yaml);
        Assert.Equal(yaml, patched);
    }

    [Fact]
    public void IgnoresCommentedJson()
    {
        var yaml = """
            search:
              # formats: [html, csv, json, rss]
              formats:
                - html
            """;
        Assert.False(SearxngSettingsPatcher.HasJsonFormat(yaml));
        var patched = SearxngSettingsPatcher.EnableJsonFormat(yaml);
        Assert.True(SearxngSettingsPatcher.HasJsonFormat(patched));
    }

    [Fact]
    public void InlineList()
    {
        var yaml = "search:\n  formats: [html]\n";
        var patched = SearxngSettingsPatcher.EnableJsonFormat(yaml);
        Assert.True(SearxngSettingsPatcher.HasJsonFormat(patched));
        Assert.Contains("json", patched);
    }
}
