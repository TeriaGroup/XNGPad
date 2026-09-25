namespace XNGPad.App.ViewModels;

public sealed class KeyRow
{
    public KeyRow(string id, string title, string keyName, string place, string value, string? url)
    {
        Id = id;
        Title = title;
        KeyName = keyName;
        Place = place;
        Value = value;
        Url = url;
    }

    public string Id { get; }
    public string Title { get; }
    public string KeyName { get; }
    public string Place { get; }
    public string Value { get; }
    public string? Url { get; }
    public string NewValue { get; set; } = "";
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}
