using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using XNGPad.Core.Services;

namespace XNGPad.App.ViewModels;

public partial class McpRow : ObservableObject
{
    public McpRow(McpDef def, Func<McpRow, Task> toggle, Action<McpRow> openGuide, Func<McpRow, Task> setToken, Action<McpRow> mute)
    {
        Def = def;
        Id = def.Id;
        HintKey = def.HintKey;
        GitHubUrl = def.GitHubUrl;
        GuideKey = def.GuideKey;
        TokenUrl = def.TokenUrl;
        TokenEnvKey = def.TokenEnvKey;
        Title = def.DisplayName;
        ShowGuide = def.GuideKey != null;
        ShowToken = def.TokenEnvKey != null || def.Extra == McpExtra.Kompas;
        ToggleCommand = new AsyncRelayCommand(() => toggle(this));
        OpenGitHubCommand = new RelayCommand(() =>
        {
            if (!string.IsNullOrEmpty(GitHubUrl))
                ShellOpen.Url(GitHubUrl);
        });
        OpenGuideCommand = new RelayCommand(() => openGuide(this));
        SetTokenCommand = new AsyncRelayCommand(() => setToken(this));
        MuteCommand = new RelayCommand(() => mute(this));
    }

    public McpDef Def { get; }
    public string Id { get; }
    public string HintKey { get; }
    public string GitHubUrl { get; }
    public string? GuideKey { get; }
    public string? TokenUrl { get; }
    public string? TokenEnvKey { get; }
    public bool ShowGuide { get; }
    public bool ShowToken { get; }
    public IAsyncRelayCommand ToggleCommand { get; }
    public IRelayCommand OpenGitHubCommand { get; }
    public IRelayCommand OpenGuideCommand { get; }
    public IAsyncRelayCommand SetTokenCommand { get; }
    public IRelayCommand MuteCommand { get; }

    [ObservableProperty] private string title = "";
    [ObservableProperty] private string hint = "";
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private string actionText = "";
    [ObservableProperty] private string detailsText = "";
    [ObservableProperty] private string tokenText = "";
    [ObservableProperty] private bool isOn;
    [ObservableProperty] private bool showMute;
    [ObservableProperty] private string muteText = "";
}
