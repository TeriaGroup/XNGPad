using System.Windows;
using System.Windows.Navigation;
using XNGPad.Core.Services;

namespace XNGPad.App.Views;

public partial class InstallWindow : Window
{
    public InstallWindow()
    {
        InitializeComponent();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Link_Click(object sender, RequestNavigateEventArgs e)
    {
        ShellOpen.Url(e.Uri.AbsoluteUri);
        e.Handled = true;
    }
}
