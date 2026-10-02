using System.Text.Json;
using System.Text.Json.Nodes;

namespace GrokLauncher;

internal static class WindowsTerminalFont
{
    public const string ProfileName = "GrokLauncher";
    public const string ProfileGuid = "{a7e31c04-6b8f-4e2a-9d1c-8f3b2c1a0d77}";

    static readonly JsonSerializerOptions JsonWrite = new()
    {
        WriteIndented = true
    };

    public static string? FindSettingsPath()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string packaged = Path.Combine(
            localAppData,
            "Packages",
            "Microsoft.WindowsTerminal_8wekyb3d8bbwe",
            "LocalState",
            "settings.json");
        if (File.Exists(packaged)) return packaged;

        string unpackaged = Path.Combine(localAppData, "Microsoft", "Windows Terminal", "settings.json");
        if (File.Exists(unpackaged)) return unpackaged;
        return null;
    }

    public static string? FindWtExecutable()
    {
        string windowsApps = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Microsoft",
            "WindowsApps",
            "wt.exe");
        if (File.Exists(windowsApps)) return windowsApps;

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable)) return null;

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            string candidate = Path.Combine(directory.Trim(), "wt.exe");
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    public static bool TryEnsureProfile(int fontPoints)
    {
        fontPoints = AppSettings.NormalizeFontPoints(fontPoints);
        string? settingsPath = FindSettingsPath();
        if (settingsPath is null) return false;

        try
        {
            string json = File.ReadAllText(settingsPath);
            JsonNode? root = JsonNode.Parse(json);
            if (root is not JsonObject rootObject) return false;

            JsonArray list = GetProfileList(rootObject);
            JsonObject? profile = FindGrokLauncherProfile(list);
            if (profile is null)
            {
                profile = new JsonObject
                {
                    ["guid"] = ProfileGuid,
                    ["name"] = ProfileName,
                    ["hidden"] = true
                };
                list.Add(profile);
            }

            var font = profile["font"] as JsonObject;
            if (font is null)
            {
                font = new JsonObject();
                profile["font"] = font;
            }

            font["size"] = fontPoints;
            if (font["face"] is null) font["face"] = "Cascadia Mono";
            profile["hidden"] = true;
            profile["closeOnExit"] = "always";

            string updated = rootObject.ToJsonString(JsonWrite);
            File.WriteAllText(settingsPath, updated);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    static JsonArray GetProfileList(JsonObject rootObject)
    {
        JsonNode? profilesNode = rootObject["profiles"];
        if (profilesNode is JsonArray profilesArray) return profilesArray;

        if (profilesNode is JsonObject profilesObject)
        {
            if (profilesObject["list"] is JsonArray existing) return existing;
            var created = new JsonArray();
            profilesObject["list"] = created;
            return created;
        }

        var wrapper = new JsonObject();
        var list = new JsonArray();
        wrapper["list"] = list;
        rootObject["profiles"] = wrapper;
        return list;
    }

    static JsonObject? FindGrokLauncherProfile(JsonArray list)
    {
        foreach (JsonNode? node in list)
        {
            if (node is not JsonObject profile) continue;
            string guid = profile["guid"]?.GetValue<string>() ?? "";
            string name = profile["name"]?.GetValue<string>() ?? "";
            if (guid.Equals(ProfileGuid, StringComparison.OrdinalIgnoreCase)) return profile;
            if (name.Equals(ProfileName, StringComparison.OrdinalIgnoreCase)) return profile;
        }

        return null;
    }
}
