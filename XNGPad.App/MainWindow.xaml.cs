using System.Windows;
using XNGPad.App.ViewModels;

namespace XNGPad.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel vm)
    {
        _vm = vm;
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;
        Closed += (_, _) => _vm.Lifetime.Cancel();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await _vm.InitializeAsync();
    }
}
