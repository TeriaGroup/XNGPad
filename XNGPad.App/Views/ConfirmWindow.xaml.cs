using System.Windows;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class ConfirmWindow : Window
{
    public ConfirmWindow(LocalizationService loc, string message)
    {
        InitializeComponent();
        MessageText.Text = message;
        YesButton.Content = loc.Get("Yes");
        NoButton.Content = loc.Get("No");
    }

    private void Yes_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void No_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
