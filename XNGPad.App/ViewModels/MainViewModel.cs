using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XNGPad.App.Services;
using XNGPad.Core;
using XNGPad.Core.Models;
using XNGPad.Core.Services;

namespace XNGPad.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DockerService _docker;
    private readonly SearxngService _searxng;
    private readonly OpenCodeService _opencode;
    private readonly SettingsService _settingsService;
    private readonly NodeService _node;
    private readonly InstallService _install;
    private readonly IAppDialogs _dialogs;
    private AppSettings _settings;
    private DockerKind _dockerKind = DockerKind.Missing;
    private SearxngKind _searxngKind = SearxngKind.Off;
    private OpenCodeKind _openCodeKind = OpenCodeKind.ConfigMissing;
    private bool _containerExists;
    private bool _wslHint;
    private bool _cliMissing;
    private bool _desktopRunning;
    private bool _registryLoaded;
    private Dictionary<string, string> _hintRu = new(StringComparer.Ordinal);
    private int _hintRuBusy;

    public MainViewModel(
        DockerService docker,
        SearxngService searxng,
        OpenCodeService opencode,
        LocalizationService loc,
        SettingsService settingsService,
        NodeService node,
        InstallService install,
        IAppDialogs dialogs,
        AppSettings settings)
    {
        _docker = docker;
        _searxng = searxng;
        _opencode = opencode;
        Loc = loc;
        _settingsService = settingsService;
        _node = node;
        _install = install;
        _dialogs = dialogs;
        _settings = settings;
        IsRu = loc.Language == "ru";
        IsEn = loc.Language == "en";
        foreach (var def in McpCatalog.All.OrderBy(d => d.DisplayName, StringComparer.OrdinalIgnoreCase))
            McpRows.Add(new McpRow(def, ToggleMcpAsync, OpenMcpGuide, SetMcpTokenAsync, MuteMcp));
        _hintRu = McpRegistry.ReadHintCache(HintCachePath);
        ApplyStatusTexts();
        RefreshInstallStatus();
    }

    public LocalizationService Loc { get; }

    public CancellationTokenSource Lifetime { get; } = new();

    [ObservableProperty] private bool isBusy;
    [ObservableProperty] private string busyText = "";
    [ObservableProperty] private string banner = "";
    [ObservableProperty] private string bannerLevel = "info";
    [ObservableProperty] private bool showDownloadNode;
    [ObservableProperty] private string dockerStatusText = "";
    [ObservableProperty] private string searxngStatusText = "";
    [ObservableProperty] private string openCodeStatusText = "";
    [ObservableProperty] private string dockerLevel = "mute";
    [ObservableProperty] private string searxngLevel = "mute";
    [ObservableProperty] private string openCodeLevel = "mute";
    [ObservableProperty] private bool showDownloadDocker;
    [ObservableProperty] private bool showStartDocker;
    [ObservableProperty] private bool showEnableSearxng;
    [ObservableProperty] private bool showDisableSearxng;
    [ObservableProperty] private bool showConnect;
    [ObservableProperty] private bool showDisconnect;
    [ObservableProperty] private bool showOpenSearxng;
    [ObservableProperty] private bool showLog;
    [ObservableProperty] private bool showDockerHint;
    [ObservableProperty] private bool isRu;
    [ObservableProperty] private bool isEn;
    [ObservableProperty] private bool isSettingsOpen;
    [ObservableProperty] private string editPort = "";
    [ObservableProperty] private string editContainerName = "";
    [ObservableProperty] private string editSearxngUrl = "";
    [ObservableProperty] private string editOpenCodePath = "";
    [ObservableProperty] private int editLanguageIndex;
    [ObservableProperty] private bool isInstallOpen;
    [ObservableProperty] private string nodeInstallStatus = "";
    [ObservableProperty] private string dockerInstallStatus = "";
    [ObservableProperty] private string openCodeInstallStatus = "";
    [ObservableProperty] private bool showInstallNode;
    [ObservableProperty] private bool showInstallDocker;
    [ObservableProperty] private bool showInstallOpenCode;

    public bool IsHome => !IsSettingsOpen;

    public ObservableCollection<McpRow> McpRows { get; } = [];
    public ObservableCollection<McpRow> AddedMcpRows { get; } = [];
    public ObservableCollection<McpRow> CatalogMcpRows { get; } = [];
    public ObservableCollection<McpRow> HiddenMcpRows { get; } = [];
    public ObservableCollection<DownloadJob> DownloadJobs { get; } = [];
    public ObservableCollection<McpRow> UsedMcpRows { get; } = [];

    [ObservableProperty] private string installPage = "catalog";
    [ObservableProperty] private bool showUsedEmpty;
    public bool InstallIsCatalog => InstallPage == "catalog";
    public bool InstallIsUsed => InstallPage == "used";

    partial void OnInstallPageChanged(string value)
    {
        OnPropertyChanged(nameof(InstallIsCatalog));
        OnPropertyChanged(nameof(InstallIsUsed));
    }

    [RelayCommand]
    private void SetInstallPage(string? page)
    {
        if (page is "catalog" or "used")
            InstallPage = page;
    }

    private readonly Dictionary<string, McpRow> _extraUsed = new(StringComparer.OrdinalIgnoreCase);

    [ObservableProperty] private bool showDownloadQueue;
    [ObservableProperty] private bool queueBusy;

    public bool ShowInstallProgress => IsBusy || QueueBusy;

    private readonly Queue<(McpRow Row, string? Secret, string? Secret2, DownloadJob Job)> _mcpQueue = new();
    private int _mcpPump;

    [ObservableProperty] private bool showAddedMcps;
    [ObservableProperty] private bool showCatalogMcps;
    [ObservableProperty] private bool showHiddenMcps;
    [ObservableProperty] private bool addedMcpsExpanded = true;
    [ObservableProperty] private bool catalogMcpsExpanded = true;
    [ObservableProperty] private bool hiddenMcpsExpanded;

    public string AddedMcpsArrow => AddedMcpsExpanded ? "▼" : "▶";
    public string CatalogMcpsArrow => CatalogMcpsExpanded ? "▼" : "▶";
    public string HiddenMcpsArrow => HiddenMcpsExpanded ? "▼" : "▶";

    partial void OnAddedMcpsExpandedChanged(bool value) => OnPropertyChanged(nameof(AddedMcpsArrow));
    partial void OnCatalogMcpsExpandedChanged(bool value) => OnPropertyChanged(nameof(CatalogMcpsArrow));
    partial void OnHiddenMcpsExpandedChanged(bool value) => OnPropertyChanged(nameof(HiddenMcpsArrow));

    [RelayCommand]
    private void ToggleAddedMcps() => AddedMcpsExpanded = !AddedMcpsExpanded;

    [RelayCommand]
    private void ToggleCatalogMcps() => CatalogMcpsExpanded = !CatalogMcpsExpanded;

    [RelayCommand]
    private void ToggleHiddenMcps() => HiddenMcpsExpanded = !HiddenMcpsExpanded;

    [ObservableProperty] private string mcpSearch = "";
    [ObservableProperty] private string mcpFilter = "all";

    public bool FilterIsAll => McpFilter == "all";
    public bool FilterIsKey => McpFilter == "key";
    public bool FilterIsNoKey => McpFilter == "nokey";
    public bool FilterIsNpm => McpFilter == "npm";
    public bool FilterIsRemote => McpFilter == "remote";

    partial void OnMcpSearchChanged(string value) => ApplyMcpFilter();

    partial void OnMcpFilterChanged(string value)
    {
        OnPropertyChanged(nameof(FilterIsAll));
        OnPropertyChanged(nameof(FilterIsKey));
        OnPropertyChanged(nameof(FilterIsNoKey));
        OnPropertyChanged(nameof(FilterIsNpm));
        OnPropertyChanged(nameof(FilterIsRemote));
        ApplyMcpFilter();
    }

    [RelayCommand]
    private void SetMcpFilter(string? filter)
    {
        if (!string.IsNullOrWhiteSpace(filter))
            McpFilter = filter;
    }

    public string DefaultOpenCodePath => OpenCodeService.DefaultConfigPath;

    partial void OnIsSettingsOpenChanged(bool value)
    {
        if (value)
            IsInstallOpen = false;
        OnPropertyChanged(nameof(IsHome));
    }

    partial void OnIsInstallOpenChanged(bool value)
    {
        if (value)
            IsSettingsOpen = false;
        OnPropertyChanged(nameof(IsHome));
        if (value)
            RefreshInstallStatus();
    }

    private bool CanRun() => !IsBusy;

    partial void OnQueueBusyChanged(bool value) => OnPropertyChanged(nameof(ShowInstallProgress));

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstallProgress));
        RefreshCommand.NotifyCanExecuteChanged();
        DownloadDockerCommand.NotifyCanExecuteChanged();
        StartDockerCommand.NotifyCanExecuteChanged();
        EnableSearxngCommand.NotifyCanExecuteChanged();
        DisableSearxngCommand.NotifyCanExecuteChanged();
        ConnectOpenCodeCommand.NotifyCanExecuteChanged();
        DisconnectOpenCodeCommand.NotifyCanExecuteChanged();
        OpenSearxngCommand.NotifyCanExecuteChanged();
        ShowLogCommand.NotifyCanExecuteChanged();
        DownloadNodeCommand.NotifyCanExecuteChanged();
        InstallNodeCommand.NotifyCanExecuteChanged();
        InstallDockerCommand.NotifyCanExecuteChanged();
        InstallOpenCodeCommand.NotifyCanExecuteChanged();
    }

    public async Task InitializeAsync()
    {
        await RefreshAsync();
        _ = IdleRefreshLoop();
    }

    private async Task IdleRefreshLoop()
    {
        var ct = Lifetime.Token;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(8000, ct);
                if (!IsBusy && !IsSettingsOpen && !IsInstallOpen)
                    await LoadStatusAsync(ct);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RefreshAsync()
    {
        Banner = "";
        ShowDownloadNode = false;
        try
        {
            await LoadStatusAsync(Lifetime.Token);
            if (_dockerKind == DockerKind.Missing)
            {
                Banner = Loc.Get(_cliMissing ? "DockerNotInPath" : "ErrorDockerNotInstalled");
                BannerLevel = "err";
            }
            else if (_dockerKind == DockerKind.InstalledNotRunning)
            {
                Banner = Loc.Get(_desktopRunning ? "ErrorDockerStarting" : (_wslHint ? "ErrorWsl" : "ErrorDaemon"));
                BannerLevel = "warn";
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    [RelayCommand]
    private void SetLanguage(string? lang)
    {
        if (string.IsNullOrWhiteSpace(lang))
            return;
        _settings.Language = lang;
        _settingsService.Save(_settings);
        ApplyLanguage(lang);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void DownloadDocker()
    {
        ShellOpen.Url(Constants.DockerInstallerUrl);
        Banner = Loc.Get("DownloadDockerHint");
        BannerLevel = "info";
        if (_cliMissing)
            Banner = Loc.Get("RestartPcAfterDocker");
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void DownloadNode()
    {
        ShellOpen.Url(Constants.NodeJsUrl);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task StartDockerAsync()
    {
        await RunBusyAsync("BusyStartingDocker", async ct =>
        {
            var ok = await _docker.StartDesktopAsync(ct);
            await LoadStatusAsync(ct);
            if (ok)
            {
                Banner = Loc.Get("DockerStarted");
                BannerLevel = "ok";
            }
            else
            {
                Banner = Loc.Get(_docker.IsDesktopProcessRunning() ? "ErrorDockerStarting" : "DockerStartFailed");
                BannerLevel = "err";
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task EnableSearxngAsync()
    {
        await RunBusyAsync("BusyStartingSearxng", async ct =>
        {
            _searxngKind = SearxngKind.Pulling;
            ApplyStatusTexts();
            var progress = new Progress<string>(key => BusyText = Loc.Get(key));
            try
            {
                await _searxng.EnsureContainerAsync(_settings, recreate: false, progress, ct);
            }
            catch (ContainerImageConflictException ex)
            {
                var msg = Loc.Get(ex.Key, ex.Args);
                if (!_dialogs.Confirm(msg))
                    return;
                BusyText = Loc.Get("BusyStartingSearxng");
                await _searxng.EnsureContainerAsync(_settings, recreate: true, progress, ct);
            }

            BusyText = Loc.Get("BusyRefreshing");
            var health = await _searxng.WaitHealthyAsync(_settings.ResolvedUrl(), TimeSpan.FromSeconds(30), ct);
            if (health.Kind == HealthKind.JsonForbidden)
            {
                BusyText = Loc.Get("BusyEnablingJson");
                await _searxng.EnableJsonFormatAsync(_settings.ResolvedContainerName(), ct);
                health = await _searxng.WaitHealthyAsync(_settings.ResolvedUrl(), TimeSpan.FromSeconds(25), ct);
            }

            await LoadStatusAsync(ct);
            if (health.Kind == HealthKind.Ok)
            {
                Banner = Loc.Get("SearxngReady", _settings.ResolvedUrl());
                BannerLevel = "ok";
            }
            else if (health.Kind == HealthKind.JsonForbidden)
            {
                Banner = Loc.Get("ErrorJson403");
                BannerLevel = "warn";
            }
            else
            {
                Banner = Loc.Get("ErrorHealth");
                BannerLevel = "warn";
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DisableSearxngAsync()
    {
        await RunBusyAsync("BusyStopping", async ct =>
        {
            await _searxng.StopAsync(_settings.ResolvedContainerName(), ct);
            await LoadStatusAsync(ct);
            Banner = Loc.Get("SearxngStopped");
            BannerLevel = "info";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ConnectOpenCodeAsync()
    {
        await RunBusyAsync("BusyConnecting", async ct =>
        {
            ShowDownloadNode = false;
            if (!_node.IsNpxAvailable())
            {
                ShowDownloadNode = true;
                Banner = Loc.Get("ErrorNodeMissing");
                BannerLevel = "err";
                return;
            }

            if (_dockerKind != DockerKind.Ok)
            {
                Banner = Loc.Get(_dockerKind == DockerKind.Missing ? "ErrorDockerNotInstalled" : "ErrorDaemon");
                BannerLevel = "err";
                return;
            }

            var info = await _docker.InspectAsync(_settings.ResolvedContainerName(), ct);
            if (!info.Exists || !info.Running)
            {
                Banner = Loc.Get("EnableSearxngFirst");
                BannerLevel = "warn";
                return;
            }

            var health = await _searxng.HealthCheckAsync(_settings.ResolvedUrl(), ct);
            if (health.Kind == HealthKind.JsonForbidden)
            {
                BusyText = Loc.Get("BusyEnablingJson");
                await _searxng.EnableJsonFormatAsync(_settings.ResolvedContainerName(), ct);
                health = await _searxng.WaitHealthyAsync(_settings.ResolvedUrl(), TimeSpan.FromSeconds(25), ct);
            }

            if (health.Kind == HealthKind.Unreachable)
            {
                Banner = Loc.Get("ErrorHealth");
                BannerLevel = "err";
                return;
            }

            if (health.Kind == HealthKind.JsonForbidden)
            {
                Banner = Loc.Get("ErrorJson403");
                BannerLevel = "err";
                return;
            }

            var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
            _opencode.Connect(path, _settings.ResolvedUrl());
            await LoadStatusAsync(ct);
            Banner = Loc.Get("ConnectedOk");
            BannerLevel = "ok";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task DisconnectOpenCodeAsync()
    {
        await RunBusyAsync("BusyConnecting", async ct =>
        {
            var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
            _opencode.Disconnect(path);
            await LoadStatusAsync(ct);
            Banner = Loc.Get("DisconnectedOk");
            BannerLevel = "info";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private void OpenSearxng()
    {
        ShellOpen.Url(_settings.ResolvedUrl());
    }

    [RelayCommand]
    private void OpenOpenCodeFolder()
    {
        var file = _opencode.ResolvePath(IsSettingsOpen ? (string.IsNullOrWhiteSpace(EditOpenCodePath) ? null : EditOpenCodePath) : _settings.OpenCodeConfigPath);
        ShellOpen.Folder(file);
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task ShowLogAsync()
    {
        await RunBusyAsync("BusyRefreshing", async ct =>
        {
            if (_dockerKind != DockerKind.Ok)
            {
                Banner = Loc.Get(_wslHint ? "ErrorWsl" : "ErrorDaemon");
                BannerLevel = "err";
                return;
            }

            var name = _settings.ResolvedContainerName();
            var info = await _docker.InspectAsync(name, ct);
            if (!info.Exists)
            {
                Banner = Loc.Get("ErrorNoContainer");
                BannerLevel = "warn";
                return;
            }

            var text = await _docker.LogsAsync(name, 80, ct);
            if (string.IsNullOrWhiteSpace(text))
                text = Loc.Get("LogEmpty");
            _dialogs.ShowLog(Loc.Get("LogTitle"), text);
        });
    }

    [RelayCommand]
    private void OpenSettings()
    {
        EditPort = _settings.ResolvedPort().ToString();
        EditContainerName = _settings.ResolvedContainerName();
        EditSearxngUrl = _settings.ResolvedUrl();
        EditOpenCodePath = _settings.OpenCodeConfigPath ?? "";
        var lang = _settings.Language ?? "";
        EditLanguageIndex = lang.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? 1
            : lang.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 2
            : 0;
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings()
    {
        IsSettingsOpen = false;
    }

    [RelayCommand]
    private async Task OpenInstallAsync()
    {
        if (!_registryLoaded)
        {
            await RunBusyAsync("BusyLoadingMcps", async ct =>
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(12));
                try
                {
                    await EnsureRegistryAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                }

                RefreshInstallStatus();
            });
        }
        else
            RefreshInstallStatus();

        _ = FillRuHintsAsync(Lifetime.Token, TimeSpan.FromSeconds(25));
        IsInstallOpen = true;
        _dialogs.ShowInstall();
        IsInstallOpen = false;
    }

    [RelayCommand]
    private void ClosePanels()
    {
        IsSettingsOpen = false;
        IsInstallOpen = false;
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task InstallNodeAsync()
    {
        await RunBusyAsync("BusyInstallingNode", async ct =>
        {
            await _install.InstallNodeAsync(ct);
            RefreshInstallStatus();
            Banner = Loc.Get("InstallNodeOk");
            BannerLevel = "ok";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task InstallDockerAsync()
    {
        await RunBusyAsync("BusyInstallingDocker", async ct =>
        {
            await _install.InstallDockerAsync(ct);
            RefreshInstallStatus();
            Banner = Loc.Get("InstallDockerOk");
            BannerLevel = "ok";
        });
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task InstallOpenCodeAsync()
    {
        await RunBusyAsync("BusyInstallingOpenCode", async ct =>
        {
            await _install.InstallOpenCodeAsync(ct);
            RefreshInstallStatus();
            Banner = Loc.Get("InstallOpenCodeOk");
            BannerLevel = "ok";
        });
    }

    private async Task ToggleMcpAsync(McpRow row)
    {
        var remoteOnly = string.IsNullOrEmpty(row.Def.Package) && !string.IsNullOrEmpty(row.Def.RemoteUrl);
        if (!remoteOnly && McpCatalog.NeedsNpx(row.Def) && !_install.IsNodeInstalled() && !_node.IsNpxAvailable())
        {
            Banner = Loc.Get("NeedNodeFirst");
            BannerLevel = "warn";
            return;
        }

        var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
        string? secret = null;
        string? secret2 = null;
        if (!_opencode.HasMcp(path, row.Id) && row.Def.TokenEnvKey2 != null)
        {
            var pair = _dialogs.AskTwoSecrets(row.Id, Loc.Get("TokenHintTelegramId"), Loc.Get("TokenHintTelegramHash"), row.TokenUrl);
            if (pair == null)
                return;
            secret = pair.Value.First;
            secret2 = pair.Value.Second;
        }
        else if (!_opencode.HasMcp(path, row.Id) && row.TokenEnvKey != null)
        {
            secret = _dialogs.AskSecret(row.Id, Loc.Get("TokenHint"), row.TokenUrl);
            if (secret == null)
                return;
        }

        if (_opencode.HasMcp(path, row.Id))
        {
            _opencode.RemoveMcp(path, row.Id);
            RefreshInstallStatus();
            Banner = Loc.Get("RestartOpenCodeHint");
            BannerLevel = "ok";
            return;
        }

        EnqueueMcp(row, secret, secret2);
    }

    private void EnqueueMcp(McpRow row, string? secret, string? secret2)
    {
        lock (_mcpQueue)
        {
            if (_mcpQueue.Any(x => x.Row.Id == row.Id))
                return;
        }

        if (DownloadJobs.Any(j => j.Id == row.Id && j.IsActive))
            return;
        var job = new DownloadJob(row.Id, row.Title, Loc.Get("QueueQueued"));
        DownloadJobs.Add(job);
        ShowDownloadQueue = true;
        lock (_mcpQueue)
            _mcpQueue.Enqueue((row, secret, secret2, job));
        _ = PumpMcpQueueAsync();
    }

    private async Task PumpMcpQueueAsync()
    {
        if (Interlocked.Exchange(ref _mcpPump, 1) == 1)
            return;
        QueueBusy = true;
        try
        {
            while (true)
            {
                (McpRow Row, string? Secret, string? Secret2, DownloadJob Job) item;
                lock (_mcpQueue)
                {
                    if (_mcpQueue.Count == 0)
                        break;
                    item = _mcpQueue.Dequeue();
                }

                item.Job.StatusText = Loc.Get("QueueRunning");
                try
                {
                    var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
                    if (McpCatalog.NeedsKompasBin(item.Row.Def))
                        await _install.EnsureKompasMcpAsync(Lifetime.Token);
                    if (McpCatalog.NeedsPython(item.Row.Def))
                        await _install.EnsureOfficeMcpAsync(Lifetime.Token);
                    UnhideMcpId(item.Row.Id);
                    _opencode.UpsertMcp(path, item.Row.Id, McpCatalog.CreateNode(item.Row.Def, _settings, item.Secret, item.Secret2));
                    RememberUsed(item.Row.Id);
                    item.Job.StatusText = Loc.Get("QueueDone");
                    item.Job.IsActive = false;
                    Banner = Loc.Get(item.Secret != null ? "TokenSaved" : "RestartOpenCodeHint");
                    BannerLevel = "ok";
                }
                catch (Exception ex)
                {
                    item.Job.StatusText = Loc.Get("QueueError");
                    item.Job.IsActive = false;
                    ShowError(ex);
                }

                RefreshInstallStatus();
                TrimDownloadJobs();
            }
        }
        finally
        {
            QueueBusy = false;
            Interlocked.Exchange(ref _mcpPump, 0);
            var more = false;
            lock (_mcpQueue)
                more = _mcpQueue.Count > 0;
            if (more)
                _ = PumpMcpQueueAsync();
        }
    }

    private void TrimDownloadJobs()
    {
        while (DownloadJobs.Count > 6)
        {
            var first = DownloadJobs[0];
            if (first.IsActive)
                break;
            DownloadJobs.RemoveAt(0);
        }

        ShowDownloadQueue = DownloadJobs.Count > 0;
    }

    private async Task SetMcpTokenAsync(McpRow row)
    {
        string? secret;
        string? secret2 = null;
        if (row.Def.Extra == McpExtra.Kompas)
        {
            secret = _dialogs.AskSecret(row.Id, Loc.Get("TokenHintKompas"), row.TokenUrl);
            if (secret == null)
                return;
            await RunBusyAsync("BusyInstallingMcp", async ct =>
            {
                await _install.SaveKompasConfigAsync(secret, ct);
                var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
                _opencode.UpsertMcp(path, row.Id, McpCatalog.CreateNode(row.Def, _settings));
                RefreshInstallStatus();
                Banner = Loc.Get("TokenSaved");
                BannerLevel = "ok";
            });
            return;
        }

        if (row.Def.TokenEnvKey2 != null)
        {
            var pair = _dialogs.AskTwoSecrets(row.Id, Loc.Get("TokenHintTelegramId"), Loc.Get("TokenHintTelegramHash"), row.TokenUrl);
            if (pair == null)
                return;
            secret = pair.Value.First;
            secret2 = pair.Value.Second;
        }
        else
        {
            secret = _dialogs.AskSecret(row.Id, Loc.Get("TokenHint"), row.TokenUrl);
            if (secret == null)
                return;
        }

        await RunBusyAsync("BusyInstallingMcp", async _ =>
        {
            var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
            _opencode.UpsertMcp(path, row.Id, McpCatalog.CreateNode(row.Def, _settings, secret, secret2));
            RefreshInstallStatus();
            Banner = Loc.Get("TokenSaved");
            BannerLevel = "ok";
        });
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!int.TryParse(EditPort.Trim(), out var port) || port is < 1 or > 65535)
            return;
        var name = EditContainerName.Trim();
        if (name.Length == 0)
            return;
        var url = EditSearxngUrl.Trim();
        if (url.Length == 0)
            url = $"http://127.0.0.1:{port}";
        var lang = EditLanguageIndex switch
        {
            1 => "ru",
            2 => "en",
            _ => ""
        };
        _settings = new AppSettings
        {
            Port = port,
            ContainerName = name,
            SearxngUrl = url.TrimEnd('/'),
            OpenCodeConfigPath = string.IsNullOrWhiteSpace(EditOpenCodePath) ? null : EditOpenCodePath.Trim(),
            Language = lang,
            HiddenMcpIds = [.. _settings.HiddenMcpIds],
            UsedMcpIds = [.. _settings.UsedMcpIds]
        };
        _settingsService.Save(_settings);
        ApplyLanguage(SettingsService.ResolveLanguage(_settings));
        IsSettingsOpen = false;
        Banner = Loc.Get("SettingsSaved");
        BannerLevel = "ok";
        await RefreshAsync();
    }

    private void ApplyLanguage(string lang)
    {
        Loc.SetLanguage(lang);
        IsRu = Loc.Language == "ru";
        IsEn = Loc.Language == "en";
        OnPropertyChanged(nameof(Loc));
        ApplyStatusTexts();
        RefreshInstallStatus();
        if (Loc.Language == "ru")
            _ = FillRuHintsAsync(Lifetime.Token, null);
    }

    private void RefreshInstallStatus()
    {
        var nodeOk = _node.IsNpxAvailable() || _install.IsNodeInstalled();
        var dockerOk = _docker.IsInstalled();
        var ocOk = _install.IsOpenCodeInstalled();
        NodeInstallStatus = Loc.Get(nodeOk ? "StatusInstalled" : "StatusMissing");
        DockerInstallStatus = Loc.Get(dockerOk ? "StatusInstalled" : "StatusMissing");
        OpenCodeInstallStatus = Loc.Get(ocOk ? "StatusInstalled" : "StatusMissing");
        ShowInstallNode = !nodeOk;
        ShowInstallDocker = !dockerOk;
        ShowInstallOpenCode = !ocOk;
        var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
        try
        {
            _opencode.RepairMcp(path);
        }
        catch
        {
        }

        foreach (var row in McpRows)
        {
            row.Hint = McpRegistry.FormatHint(row.Def, Loc, _hintRu);
            row.DetailsText = row.ShowGuide ? Loc.Get("McpDetails") : "";
            row.TokenText = row.ShowToken ? Loc.Get("McpToken") : "";
            var on = _opencode.HasMcp(path, row.Id);
            row.IsOn = on;
            row.StatusText = Loc.Get(on ? "StatusInstalled" : "StatusMissing");
            row.ActionText = Loc.Get(on ? "RemoveMcp" : "AddMcp");
        }

        ApplyMcpFilter();
        RebuildUsed(path);
    }

    private void RememberUsed(string id)
    {
        _settings.UsedMcpIds ??= [];
        if (_settings.UsedMcpIds.Contains(id))
            return;
        _settings.UsedMcpIds.Add(id);
        _settingsService.Save(_settings);
    }

    private void RebuildUsed(string path)
    {
        _settings.UsedMcpIds ??= [];
        var changed = false;
        foreach (var id in _opencode.ListMcpIds(path))
        {
            if (_settings.UsedMcpIds.Contains(id))
                continue;
            _settings.UsedMcpIds.Add(id);
            changed = true;
        }

        if (changed)
            _settingsService.Save(_settings);

        UsedMcpRows.Clear();
        foreach (var id in _settings.UsedMcpIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
        {
            var row = McpRows.FirstOrDefault(r => r.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (row == null)
            {
                if (!_extraUsed.TryGetValue(id, out row))
                {
                    row = new McpRow(new McpDef(id, id, "", "", ""), ToggleMcpAsync, OpenMcpGuide, SetMcpTokenAsync, MuteMcp);
                    _extraUsed[id] = row;
                }

                var on = _opencode.HasMcp(path, id);
                row.IsOn = on;
                row.Hint = id;
                row.StatusText = Loc.Get(on ? "StatusInstalled" : "StatusMissing");
                row.ActionText = Loc.Get("McpForget");
            }

            UsedMcpRows.Add(row);
        }

        ShowUsedEmpty = UsedMcpRows.Count == 0;
    }

    [RelayCommand]
    private void ForgetMcp(McpRow? row)
    {
        if (row == null)
            return;
        var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
        try
        {
            if (_opencode.HasMcp(path, row.Id))
                _opencode.RemoveMcp(path, row.Id);
        }
        catch (Exception ex)
        {
            ShowError(ex);
            return;
        }

        _settings.UsedMcpIds?.Remove(row.Id);
        _settingsService.Save(_settings);
        RefreshInstallStatus();
    }

    [RelayCommand]
    private void OpenKeys()
    {
        var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
        var rows = _opencode.ListSecrets(path).Select(s => new KeyRow(
            s.Id,
            TitleFor(s.Id),
            s.Key,
            s.Place,
            s.Value,
            UrlFor(s.Id))).ToList();
        _dialogs.ShowKeys(rows, (row, value) =>
        {
            _opencode.SetSecret(path, row.Id, row.KeyName, row.Place, value);
            Banner = Loc.Get("KeysSaved");
            BannerLevel = "ok";
        });
    }

    private string TitleFor(string id) =>
        McpCatalog.All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase))?.DisplayName ?? id;

    private static string? UrlFor(string id)
    {
        var def = McpCatalog.All.FirstOrDefault(x => x.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(def?.TokenUrl))
            return def.TokenUrl;
        return id.ToLowerInvariant() switch
        {
            "exa" => "https://dashboard.exa.ai/api-keys",
            "context7" => "https://context7.com/dashboard",
            "huggingface" => "https://huggingface.co/settings/tokens",
            "github" => "https://github.com/settings/tokens",
            _ => null
        };
    }

    private void MuteMcp(McpRow row)
    {
        _settings.HiddenMcpIds ??= [];
        if (_settings.HiddenMcpIds.Contains(row.Id))
            _settings.HiddenMcpIds.Remove(row.Id);
        else
            _settings.HiddenMcpIds.Add(row.Id);
        _settingsService.Save(_settings);
        ApplyMcpFilter();
    }

    private void UnhideMcpId(string id)
    {
        if (_settings.HiddenMcpIds.Remove(id))
            _settingsService.Save(_settings);
    }

    private bool IsHidden(string id) => _settings.HiddenMcpIds.Contains(id);

    private async Task EnsureRegistryAsync(CancellationToken ct)
    {
        if (_registryLoaded)
            return;
        var dir = Path.GetDirectoryName(_settingsService.FilePath) ?? "";
        var cache = Path.Combine(dir, "mcp-registry.json");
        IReadOnlyList<McpDef> extra;
        try
        {
            extra = await _install.LoadRegistryAsync(cache, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            extra = [];
        }

        var have = new HashSet<string>(McpRows.Select(r => r.Id), StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var def in extra)
        {
            if (!have.Add(def.Id))
                continue;
            if (def.Package.Length > 0 && McpRows.Any(r => r.Def.Package.Equals(def.Package, StringComparison.OrdinalIgnoreCase)))
                continue;
            McpRows.Add(new McpRow(def, ToggleMcpAsync, OpenMcpGuide, SetMcpTokenAsync, MuteMcp));
            added++;
            if (added >= 200)
                break;
        }

        _registryLoaded = true;
    }

    private string HintCachePath =>
        Path.Combine(Path.GetDirectoryName(_settingsService.FilePath) ?? "", "mcp-hints-ru.json");

    private async Task FillRuHintsAsync(CancellationToken ct, TimeSpan? budget)
    {
        if (Loc.Language != "ru")
            return;
        if (Interlocked.Exchange(ref _hintRuBusy, 1) == 1)
            return;
        CancellationTokenSource? budgetCts = null;
        try
        {
            var token = ct;
            if (budget is { } span)
            {
                budgetCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                budgetCts.CancelAfter(span);
                token = budgetCts.Token;
            }

            var texts = McpRows
                .Where(r => string.IsNullOrEmpty(r.HintKey) && !string.IsNullOrWhiteSpace(r.Def.Hint))
                .Select(r => r.Def.Hint!)
                .Take(40)
                .ToList();
            var map = await _install.TranslateHintsToRuAsync(texts, HintCachePath, token);
            ApplyHintMap(map);
        }
        catch (OperationCanceledException)
        {
            ApplyHintMap(McpRegistry.ReadHintCache(HintCachePath));
        }
        catch
        {
        }
        finally
        {
            budgetCts?.Dispose();
            Interlocked.Exchange(ref _hintRuBusy, 0);
        }
    }

    private void ApplyHintMap(Dictionary<string, string> map)
    {
        _hintRu = map;
        foreach (var row in McpRows)
            row.Hint = McpRegistry.FormatHint(row.Def, Loc, _hintRu);
    }

    private void ApplyMcpFilter()
    {
        AddedMcpRows.Clear();
        CatalogMcpRows.Clear();
        HiddenMcpRows.Clear();
        var q = (McpSearch ?? "").Trim();
        var catalogCap = q.Length == 0 ? 80 : 200;
        foreach (var row in McpRows)
        {
            if (!McpRegistry.Matches(row.Def with { Hint = row.Hint }, q))
                continue;
            if (McpFilter == "key" && row.TokenEnvKey == null)
                continue;
            if (McpFilter == "nokey" && row.TokenEnvKey != null)
                continue;
            if (McpFilter == "npm" && string.IsNullOrEmpty(row.Def.Package))
                continue;
            if (McpFilter == "remote" && string.IsNullOrEmpty(row.Def.RemoteUrl))
                continue;
            if (row.IsOn)
            {
                row.ShowMute = false;
                row.MuteText = "";
                AddedMcpRows.Add(row);
            }
            else if (IsHidden(row.Id))
            {
                row.ShowMute = true;
                row.MuteText = Loc.Get("McpRestore");
                HiddenMcpRows.Add(row);
            }
            else if (CatalogMcpRows.Count < catalogCap)
            {
                row.ShowMute = true;
                row.MuteText = Loc.Get("McpHide");
                CatalogMcpRows.Add(row);
            }
        }

        ShowAddedMcps = AddedMcpRows.Count > 0;
        ShowCatalogMcps = CatalogMcpRows.Count > 0;
        ShowHiddenMcps = HiddenMcpRows.Count > 0;
    }

    private void OpenMcpGuide(McpRow row)
    {
        if (row.GuideKey == null)
            return;
        _dialogs.ShowGuide(row.Id, Loc.Get(row.GuideKey), row.TokenUrl);
    }

    private async Task LoadStatusAsync(CancellationToken ct)
    {
        _cliMissing = _docker.DesktopPresentButCliMissing();
        _desktopRunning = _docker.IsDesktopProcessRunning();
        if (!_docker.IsInstalled())
        {
            _dockerKind = DockerKind.Missing;
            _searxngKind = SearxngKind.Off;
            _containerExists = false;
            _wslHint = false;
            ReadOpenCode();
            ApplyStatusTexts();
            return;
        }

        var daemon = await _docker.GetDaemonInfoAsync(ct);
        _wslHint = daemon.LooksLikeWslOrEngine;
        if (!daemon.Running)
        {
            _dockerKind = DockerKind.InstalledNotRunning;
            _searxngKind = SearxngKind.Off;
            _containerExists = false;
            ReadOpenCode();
            ApplyStatusTexts();
            return;
        }

        _dockerKind = DockerKind.Ok;
        var info = await _docker.InspectAsync(_settings.ResolvedContainerName(), ct);
        _containerExists = info.Exists;
        if (info.Exists && info.Running)
            _searxngKind = SearxngKind.Running;
        else
            _searxngKind = SearxngKind.Off;
        ReadOpenCode();
        ApplyStatusTexts();
    }

    private void ReadOpenCode()
    {
        var path = _opencode.ResolvePath(_settings.OpenCodeConfigPath);
        _openCodeKind = _opencode.ReadStatus(path);
    }

    private void ApplyStatusTexts()
    {
        DockerStatusText = _dockerKind switch
        {
            DockerKind.Ok => Loc.Get("DockerOk"),
            DockerKind.InstalledNotRunning => Loc.Get("DockerInstalledNotRunning"),
            _ => Loc.Get("DockerMissing")
        };
        DockerLevel = _dockerKind switch
        {
            DockerKind.Ok => "ok",
            DockerKind.InstalledNotRunning => "warn",
            _ => "err"
        };

        SearxngStatusText = _searxngKind switch
        {
            SearxngKind.Running => Loc.Get("SearxngRunning", _settings.ResolvedUrl()),
            SearxngKind.Pulling => Loc.Get("SearxngPulling"),
            SearxngKind.Starting => Loc.Get("SearxngStarting"),
            _ => Loc.Get("SearxngOff")
        };
        SearxngLevel = _searxngKind switch
        {
            SearxngKind.Running => "ok",
            SearxngKind.Pulling or SearxngKind.Starting => "busy",
            _ => "mute"
        };

        OpenCodeStatusText = _openCodeKind switch
        {
            OpenCodeKind.Connected => Loc.Get("OpenCodeConnected"),
            OpenCodeKind.McpMissing => Loc.Get("OpenCodeMcpMissing"),
            OpenCodeKind.InvalidJson => Loc.Get("OpenCodeInvalidJson"),
            _ => Loc.Get("OpenCodeConfigMissing")
        };
        OpenCodeLevel = _openCodeKind switch
        {
            OpenCodeKind.Connected => "ok",
            OpenCodeKind.InvalidJson => "err",
            OpenCodeKind.McpMissing => "warn",
            _ => "mute"
        };

        ShowDownloadDocker = _dockerKind == DockerKind.Missing || _cliMissing;
        ShowStartDocker = _dockerKind == DockerKind.InstalledNotRunning;
        ShowEnableSearxng = _dockerKind == DockerKind.Ok && _searxngKind != SearxngKind.Running && _searxngKind != SearxngKind.Pulling;
        ShowDisableSearxng = _searxngKind == SearxngKind.Running;
        ShowConnect = _dockerKind == DockerKind.Ok && _openCodeKind != OpenCodeKind.Connected;
        ShowDisconnect = _openCodeKind == OpenCodeKind.Connected;
        ShowOpenSearxng = _searxngKind == SearxngKind.Running;
        ShowLog = _dockerKind == DockerKind.Ok && _containerExists;
        ShowDockerHint = _dockerKind == DockerKind.Missing || _cliMissing;
    }

    private async Task RunBusyAsync(string busyKey, Func<CancellationToken, Task> action)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        BusyText = Loc.Get(busyKey);
        try
        {
            await action(Lifetime.Token);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ShowError(ex);
            try
            {
                await LoadStatusAsync(Lifetime.Token);
            }
            catch
            {
            }
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
    }

    private void ShowError(Exception ex)
    {
        ShowDownloadNode = false;
        BannerLevel = "err";
        if (ex is BrokenOpenCodeJsonException broken)
            Banner = Loc.Get(broken.Key, broken.Args);
        else if (ex is XNGPadException sd)
        {
            Banner = Loc.Get(sd.Key, sd.Args);
            if (!string.IsNullOrWhiteSpace(sd.Detail))
                Banner += Environment.NewLine + sd.Detail;
        }
        else
            Banner = Loc.Get("ErrorGeneric");
    }
}
