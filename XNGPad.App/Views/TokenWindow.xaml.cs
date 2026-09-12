using System.Windows;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class TokenWindow : Window
{
    private readonly string? _url;

    public string? Secret { get; private set; }

    public TokenWindow(LocalizationService loc, string title, string hint, string? url)
    {
        InitializeComponent();
        Title = title;
        HintText.Text = hint;
        _url = url;
        SaveButton.Content = loc.Get("TokenSave");
        CancelButton.Content = loc.Get("SettingsCancel");
        SiteButton.Content = loc.Get("McpOpenTokenSite");
        SiteButton.Visibility = string.IsNullOrWhiteSpace(url) ? Visibility.Collapsed : Visibility.Visible;
        Loaded += (_, _) => SecretBox.Focus();
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_url))
            ShellOpen.Url(_url);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var value = SecretBox.Password.Trim();
        if (value.Length == 0)
        {
            SecretBox.Focus();
            return;
        }

        Secret = value;
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Secret = null;
        DialogResult = false;
        Close();
    }
}
