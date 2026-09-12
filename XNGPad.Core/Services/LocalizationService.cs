using System.Collections;
using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace XNGPad.Core.Services;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = LoadTables();
    private Dictionary<string, string> _table = Tables["en"];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "en";

    public string this[string key] => Get(key);

    public void SetLanguage(string language)
    {
        Language = language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
        _table = Tables[Language];
        var culture = CultureInfo.GetCultureInfo(Language);
        CultureInfo.CurrentUICulture = culture;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }

    public string Get(string key, params object[] args)
    {
        if (!_table.TryGetValue(key, out var value) && !Tables["en"].TryGetValue(key, out value))
            value = key;
        if (args.Length == 0)
            return value;
        try
        {
            return string.Format(CultureInfo.GetCultureInfo(Language), value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    private static Dictionary<string, Dictionary<string, string>> LoadTables()
    {
        return new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = Load("XNGPad.i18n.en.resx"),
            ["ru"] = Load("XNGPad.i18n.ru.resx")
        };
    }

    private static Dictionary<string, string> Load(string resourceName)
    {
        var asm = typeof(LocalizationService).Assembly;
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null)
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new ResourceReader(stream);
        foreach (DictionaryEntry entry in reader)
        {
            if (entry.Key is string key)
                dict[key] = entry.Value?.ToString() ?? "";
        }

        return dict;
    }
}
