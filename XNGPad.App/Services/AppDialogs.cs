using System.Windows;
using XNGPad.App.ViewModels;
using XNGPad.App.Views;
using XNGPad.Core.Services;

namespace XNGPad.App.Services;

public sealed class AppDialogs : IAppDialogs
{
    private readonly LocalizationService _loc;

    public AppDialogs(LocalizationService loc)
    {
        _loc = loc;
    }

    public Window? Owner { get; set; }

    private Window? DialogOwner =>
        Application.Current.Windows.OfType<InstallWindow>().LastOrDefault() ?? Owner;

    public bool Confirm(string message)
    {
        var w = new ConfirmWindow(_loc, message) { Owner = DialogOwner };
        return w.ShowDialog() == true;
    }

    public void ShowLog(string title, string text)
    {
        var w = new LogWindow(_loc, title, text) { Owner = DialogOwner };
        w.ShowDialog();
    }

    public void ShowGuide(string title, string body, string? url)
    {
        var w = new GuideWindow(_loc, title, body, url) { Owner = DialogOwner };
        w.ShowDialog();
    }

    public string? AskSecret(string title, string hint, string? url)
    {
        var w = new TokenWindow(_loc, title, hint, url) { Owner = DialogOwner };
        return w.ShowDialog() == true ? w.Secret : null;
    }

    public (string First, string Second)? AskTwoSecrets(string title, string hint1, string hint2, string? url)
    {
        var w = new TokenWindow(_loc, title, hint1, url, hint2) { Owner = DialogOwner };
        if (w.ShowDialog() != true || string.IsNullOrEmpty(w.Secret) || string.IsNullOrEmpty(w.Secret2))
            return null;
        return (w.Secret, w.Secret2);
    }

    public void ShowKeys(IReadOnlyList<KeyRow> rows, Action<KeyRow, string> save)
    {
        var w = new KeysWindow(_loc, rows, save) { Owner = DialogOwner };
        w.ShowDialog();
    }

    public void ShowInstall()
    {
        var w = new InstallWindow
        {
            Owner = Owner,
            DataContext = Owner?.DataContext
        };
        w.ShowDialog();
    }
}
