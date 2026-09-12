namespace XNGPad.Core.Models;

public enum DockerKind
{
    Missing,
    InstalledNotRunning,
    Ok
}

public enum SearxngKind
{
    Off,
    Pulling,
    Starting,
    Running
}

public enum OpenCodeKind
{
    ConfigMissing,
    InvalidJson,
    McpMissing,
    Connected
}

public enum HealthKind
{
    Unreachable,
    JsonForbidden,
    Ok
}

public sealed class ContainerInfo
{
    public bool Exists { get; init; }
    public bool Running { get; init; }
    public string Image { get; init; } = "";
}

public sealed class HealthResult
{
    public HealthKind Kind { get; init; }
    public int? StatusCode { get; init; }
}

public sealed class DaemonInfo
{
    public bool Running { get; init; }
    public bool LooksLikeWslOrEngine { get; init; }
    public bool LooksLikeNotRunning { get; init; }
}
