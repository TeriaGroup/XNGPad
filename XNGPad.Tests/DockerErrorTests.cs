using XNGPad.Core.Services;

namespace XNGPad.Tests;

public class DockerErrorTests
{
    [Fact]
    public void PipeError_IsWslHint()
    {
        const string text = "failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine";
        Assert.True(DockerError.IsDaemonDown(text));
        Assert.Equal("ErrorWsl", DockerError.Key(text));
    }

    [Fact]
    public void Timeout_IsMapped()
    {
        Assert.Equal("ErrorDockerTimeout", DockerError.Key("Timed out."));
    }

    [Fact]
    public void PullNetwork_IsMapped()
    {
        Assert.Equal("ErrorPull", DockerError.Key("error pulling image: i/o timeout"));
    }

    [Fact]
    public void ShortMessage_TakesLastUsefulLine()
    {
        var msg = DockerError.ShortMessage("Downloading 1 B\r\nError response from daemon: toomanyrequests");
        Assert.Equal("Error response from daemon: toomanyrequests", msg);
    }
}
