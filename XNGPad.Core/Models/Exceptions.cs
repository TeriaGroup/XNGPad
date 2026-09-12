namespace XNGPad.Core.Models;

public class XNGPadException : Exception
{
    public string Key { get; }
    public object[] Args { get; }
    public string? Detail { get; init; }

    public XNGPadException(string key, params object[] args)
        : base(key)
    {
        Key = key;
        Args = args;
    }
}

public sealed class BrokenOpenCodeJsonException : XNGPadException
{
    public string ConfigPath { get; }
    public string BackupPath { get; }

    public BrokenOpenCodeJsonException(string configPath, string backupPath)
        : base("ErrorBrokenJson", backupPath)
    {
        ConfigPath = configPath;
        BackupPath = backupPath;
    }
}

public sealed class ContainerImageConflictException : XNGPadException
{
    public string ContainerName { get; }
    public string Image { get; }

    public ContainerImageConflictException(string containerName, string image)
        : base("ConfirmRecreate", containerName)
    {
        ContainerName = containerName;
        Image = image;
    }
}

public sealed class PortInUseException : XNGPadException
{
    public int Port { get; }

    public PortInUseException(int port)
        : base("ErrorPortBusy", port)
    {
        Port = port;
    }
}
