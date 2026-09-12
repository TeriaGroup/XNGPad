namespace XNGPad.Core;

public static class Constants
{
    public const string Image = "docker.io/searxng/searxng:latest";
    public const string ContainerName = "xngpad-searxng";
    public const string VolumeName = "xngpad-searxng-config";
    public const int Port = 8888;
    public const int ContainerPort = 8080;
    public const string SchemaUrl = "https://opencode.ai/config.json";
    public const string DockerInstallerUrl = "https://desktop.docker.com/win/main/amd64/Docker%20Desktop%20Installer.exe";
    public const string NodeJsUrl = "https://nodejs.org";
    public const string McpName = "searxng";
    public const string DefaultSearxngUrl = "http://127.0.0.1:8888";
    public const string SettingsFolderName = "XNGPad";
}
