using XNGPad.Core.Models;

namespace XNGPad.Tests;

public class SettingsRoundTripTests
{
    [Fact]
    public void WithEditor_KeepsInstallFolders()
    {
        var settings = new AppSettings
        {
            MatlabInstallDir = @"D:\MATLAB\R2025b",
            KompasInstallDir = @"D:\KOMPAS",
            HiddenMcpIds = ["git"],
            UsedMcpIds = ["matlab"]
        };

        var saved = settings.WithEditor(8888, "box", "http://127.0.0.1:8888", null, "ru");

        Assert.Equal(@"D:\MATLAB\R2025b", saved.MatlabInstallDir);
        Assert.Equal(@"D:\KOMPAS", saved.KompasInstallDir);
        Assert.Equal(["git"], saved.HiddenMcpIds);
        Assert.Equal(["matlab"], saved.UsedMcpIds);
        Assert.Equal(8888, saved.Port);
        Assert.Equal("ru", saved.Language);
    }
}
