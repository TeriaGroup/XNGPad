namespace XNGPad.Core.Models;

public sealed class AppSettings
{
    public int Port { get; set; } = Constants.Port;
    public string ContainerName { get; set; } = Constants.ContainerName;
    public string SearxngUrl { get; set; } = Constants.DefaultSearxngUrl;
    public string? OpenCodeConfigPath { get; set; }
    public string Language { get; set; } = "";
    public List<string> HiddenMcpIds { get; set; } = [];

    public string ResolvedUrl()
    {
        var url = string.IsNullOrWhiteSpace(SearxngUrl)
            ? $"http://127.0.0.1:{Port}"
            : SearxngUrl.Trim();
        return url.TrimEnd('/');
    }

    public string ResolvedContainerName()
    {
        var name = string.IsNullOrWhiteSpace(ContainerName) ? Constants.ContainerName : ContainerName.Trim();
        return name;
    }

    public int ResolvedPort()
    {
        return Port is > 0 and <= 65535 ? Port : Constants.Port;
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Port = Port,
            ContainerName = ContainerName,
            SearxngUrl = SearxngUrl,
            OpenCodeConfigPath = OpenCodeConfigPath,
            Language = Language,
            HiddenMcpIds = [.. HiddenMcpIds]
        };
    }
}
