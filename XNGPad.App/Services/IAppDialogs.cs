using XNGPad.App.ViewModels;

namespace XNGPad.App.Services;

public interface IAppDialogs
{
    bool Confirm(string message);
    void ShowLog(string title, string text);
    void ShowGuide(string title, string body, string? url);
    string? AskSecret(string title, string hint, string? url);
    string? AskFolder(string title, string? initialDirectory);
    (string First, string Second)? AskTwoSecrets(string title, string hint1, string hint2, string? url);
    void ShowInstall();
    void ShowKeys(IReadOnlyList<KeyRow> rows, Action<KeyRow, string> save);
}
