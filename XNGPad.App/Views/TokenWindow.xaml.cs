using System.Windows;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class TokenWindow : Window
{
    private readonly string? _url;

    public string? Secret { get; private set; }
    public string? Secret2 { get; private set; }

    public TokenWindow(LocalizationService loc, string title, string hint, string? url, string? hint2 = null)
    {
        InitializeComponent();
        Title = title;
        HintText.Text = hint;
        _url = url;
        SaveButton.Content = loc.Get("TokenSave");
        CancelButton.Content = loc.Get("SettingsCancel");
        SiteButton.Content = loc.Get("McpOpenTokenSite");
        SiteButton.Visibility = string.IsNullOrWhiteSpace(url) ? Visibility.Collapsed : Visibility.Visible;
        if (!string.IsNullOrWhiteSpace(hint2))
        {
            HintText2.Text = hint2;
            HintText2.Visibility = Visibility.Visible;
            SecretBox2.Visibility = Visibility.Visible;
        }

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

        if (SecretBox2.Visibility == Visibility.Visible)
        {
            var value2 = SecretBox2.Password.Trim();
            if (value2.Length == 0)
            {
                SecretBox2.Focus();
                return;
            }

            Secret2 = value2;
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
