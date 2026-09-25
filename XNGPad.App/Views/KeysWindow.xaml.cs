using System.Windows;
using System.Windows.Controls;
using XNGPad.App.ViewModels;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class KeysWindow : Window
{
    private readonly IReadOnlyList<KeyRow> _rows;
    private readonly Action<KeyRow, string> _save;

    public KeysWindow(LocalizationService loc, IReadOnlyList<KeyRow> rows, Action<KeyRow, string> save)
    {
        InitializeComponent();
        _rows = rows;
        _save = save;
        Title = loc.Get("KeysTitle");
        TitleText.Text = loc.Get("KeysTitle");
        HintText.Text = rows.Count == 0 ? loc.Get("KeysEmpty") : loc.Get("KeysHint");
        OpenAllButton.Content = loc.Get("KeysOpenAll");
        CloseButton.Content = loc.Get("Close");
        List.ItemsSource = rows;
        Loaded += (_, _) => Dispatcher.BeginInvoke(() => StampButtons(loc));
    }

    private void StampButtons(LocalizationService loc)
    {
        foreach (var button in FindVisuals<Button>(List))
        {
            if (button.Name == "SiteButton")
            {
                button.Content = loc.Get("McpOpenTokenSite");
                if (button.Tag is KeyRow row && string.IsNullOrWhiteSpace(row.Url))
                    button.Visibility = Visibility.Collapsed;
            }
            else if (button.Name == "SaveButton")
                button.Content = loc.Get("TokenSave");
        }
    }

    private void Site_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: KeyRow row } && !string.IsNullOrWhiteSpace(row.Url))
            ShellOpen.Url(row.Url);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: KeyRow row })
            return;
        var value = row.NewValue.Trim();
        if (value.Length == 0)
            return;
        _save(row, value);
    }

    private void OpenAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var url in _rows.Select(r => r.Url).Where(u => !string.IsNullOrWhiteSpace(u)).Distinct())
            ShellOpen.Url(url!);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private static IEnumerable<T> FindVisuals<T>(DependencyObject root) where T : DependencyObject
    {
        var count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T match)
                yield return match;
            foreach (var nested in FindVisuals<T>(child))
                yield return nested;
        }
    }
}
