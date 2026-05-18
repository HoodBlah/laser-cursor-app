using System.IO;
using System.Text.Json;
using LaserCursorAppPro.Models;

namespace LaserCursorAppPro.Services;

public static class SettingsService
{
    private static readonly string AppDataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LaserCursorPro");

    private static readonly string SettingsFile = Path.Combine(AppDataDir, "settings.json");
    private static readonly string ProfilesFile  = Path.Combine(AppDataDir, "profiles.json");

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    public static LaserSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<LaserSettings>(File.ReadAllText(SettingsFile), JsonOpts)
                       ?? new LaserSettings();
        }
        catch { }
        return new LaserSettings();
    }

    public static void SaveSettings(LaserSettings s)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(SettingsFile, JsonSerializer.Serialize(s, JsonOpts));
        }
        catch { }
    }

    public static List<LaserProfile> LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesFile))
                return JsonSerializer.Deserialize<List<LaserProfile>>(File.ReadAllText(ProfilesFile), JsonOpts)
                       ?? new List<LaserProfile>();
        }
        catch { }
        return new List<LaserProfile>();
    }

    public static void SaveProfiles(List<LaserProfile> profiles)
    {
        try
        {
            Directory.CreateDirectory(AppDataDir);
            File.WriteAllText(ProfilesFile, JsonSerializer.Serialize(profiles, JsonOpts));
        }
        catch { }
    }
}
