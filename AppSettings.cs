using System.Text.Json;

namespace GrokLauncher;

internal sealed class AppSettings
{
    public const int MaxRecentFolders = 10;

    public List<string> RecentFolders { get; set; } = new();
    public string? LastNewProjectRoot { get; set; }

    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string SettingsFilePath
    {
        get
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "GrokLauncher", "settings.json");
        }
    }

    public static AppSettings Load()
    {
        try
        {
            string filePath = SettingsFilePath;
            if (!File.Exists(filePath)) return new AppSettings();

            string json = File.ReadAllText(filePath);
            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is null) return new AppSettings();

            loaded.RecentFolders = NormalizeRecentFolders(loaded.RecentFolders);
            return loaded;
        }
        catch (Exception)
        {
            return new AppSettings();
        }
    }

    public void RememberFolder(string folderPath)
    {
        string fullPath = Path.GetFullPath(folderPath);
        RecentFolders = NormalizeRecentFolders(
            new[] { fullPath }.Concat(RecentFolders));
    }

    public void RemoveFolder(string folderPath)
    {
        string fullPath = Path.GetFullPath(folderPath);
        RecentFolders.RemoveAll(entry =>
            string.Equals(entry, fullPath, StringComparison.OrdinalIgnoreCase));
    }

    public void Save()
    {
        string filePath = SettingsFilePath;
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        RecentFolders = NormalizeRecentFolders(RecentFolders);
        string json = JsonSerializer.Serialize(this, JsonOptions);
        File.WriteAllText(filePath, json);
    }

    static List<string> NormalizeRecentFolders(IEnumerable<string> folders)
    {
        var uniqueFolders = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string folder in folders)
        {
            if (string.IsNullOrWhiteSpace(folder)) continue;

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(folder.Trim());
            }
            catch (Exception)
            {
                continue;
            }

            if (!seen.Add(fullPath)) continue;
            uniqueFolders.Add(fullPath);
            if (uniqueFolders.Count >= MaxRecentFolders) break;
        }

        return uniqueFolders;
    }
}
