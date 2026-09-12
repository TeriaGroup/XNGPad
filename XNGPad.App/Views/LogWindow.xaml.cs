using System.Windows;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class LogWindow : Window
{
    public LogWindow(LocalizationService loc, string title, string text)
    {
        InitializeComponent();
        Title = title;
        LogText.Text = text;
        CloseButton.Content = loc.Get("Close");
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
