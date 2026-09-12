using System.Text;
using System.Text.Json;
using XNGPad.Core.Models;

namespace XNGPad.Core.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string FilePath { get; }

    public SettingsService(string? filePath = null)
    {
        FilePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Constants.SettingsFolderName,
            "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(FilePath))
                return new AppSettings();
            var text = File.ReadAllText(FilePath, Encoding.UTF8);
            return JsonSerializer.Deserialize<AppSettings>(text, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(FilePath, json, new UTF8Encoding(false));
    }

    public static string DetectOsLanguage()
    {
        var two = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        return two.Equals("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    }

    public static string ResolveLanguage(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Language))
            return DetectOsLanguage();
        return settings.Language.StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? "ru" : "en";
    }
}
