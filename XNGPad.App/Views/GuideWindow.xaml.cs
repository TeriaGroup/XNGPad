using System.Windows;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class GuideWindow : Window
{
    private readonly string? _url;

    public GuideWindow(LocalizationService loc, string title, string body, string? url)
    {
        InitializeComponent();
        Title = title;
        BodyText.Text = body;
        _url = url;
        CloseButton.Content = loc.Get("Close");
        OpenSiteButton.Content = loc.Get("McpOpenTokenSite");
        OpenSiteButton.Visibility = string.IsNullOrWhiteSpace(url) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OpenSite_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_url))
            ShellOpen.Url(_url);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
