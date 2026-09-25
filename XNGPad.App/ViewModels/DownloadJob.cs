using CommunityToolkit.Mvvm.ComponentModel;

namespace XNGPad.App.ViewModels;

public partial class DownloadJob : ObservableObject
{
    public DownloadJob(string id, string title, string statusText)
    {
        Id = id;
        Title = title;
        StatusText = statusText;
    }

    public string Id { get; }
    [ObservableProperty] private string title = "";
    [ObservableProperty] private string statusText = "";
    [ObservableProperty] private bool isActive = true;
}
