namespace XNGPad.App.Services;

public interface IAppDialogs
{
    bool Confirm(string message);
    void ShowLog(string title, string text);
    void ShowGuide(string title, string body, string? url);
    string? AskSecret(string title, string hint, string? url);
    void ShowInstall();
}
