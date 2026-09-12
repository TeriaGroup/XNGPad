using System.Net.Http;
using System.Windows;
using XNGPad.App.Services;
using XNGPad.App.ViewModels;
using XNGPad.Core.Services;

namespace XNGPad.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((_, args) =>
            {
                if (args.Source is Window window)
                    DarkCaption.Apply(window);
            }));
        base.OnStartup(e);

        var settingsService = new SettingsService();
        var settings = settingsService.Load();
        var loc = new LocalizationService();
        loc.SetLanguage(SettingsService.ResolveLanguage(settings));

        var runner = new ProcessRunner();
        var docker = new DockerService(runner);
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        var searxng = new SearxngService(docker, http);
        var opencode = new OpenCodeService();
        var node = new NodeService();
        var installHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
        var install = new InstallService(runner, installHttp);
        var dialogs = new AppDialogs(loc);
        var vm = new MainViewModel(docker, searxng, opencode, loc, settingsService, node, install, dialogs, settings);
        var window = new MainWindow(vm);
        dialogs.Owner = window;
        window.Show();
    }
}
