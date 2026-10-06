using System.Text.Json;

namespace SmaliPatcherReborn.Core;

/// <summary>The one thing the app remembers between runs: whether the user chose "Never" on the donation prompt.</summary>
public static class Prefs
{
    static bool _neverThisRun;   // if the settings file cannot be written, "Never" still holds until the app closes

    // SPR_SETTINGS_DIR lets tests keep their own settings instead of the user's.
    static string? Dir
    {
        get
        {
            var d = Environment.GetEnvironmentVariable("SPR_SETTINGS_DIR");
            if (!string.IsNullOrEmpty(d)) return d;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);   // %APPDATA%, ~/.config, ~/Library/Application Support
            return string.IsNullOrEmpty(appData) ? null : Path.Combine(appData, "SmaliPatcherReborn");
        }
    }

    public static bool DonateNever
    {
        get
        {
            if (_neverThisRun) return true;
            try
            {
                var dir = Dir;
                if (dir == null) return false;
                var file = Path.Combine(dir, "settings.json");
                if (!File.Exists(file)) return false;
                using var doc = JsonDocument.Parse(File.ReadAllText(file));
                return doc.RootElement.TryGetProperty("donate", out var v) && v.GetString() == "never";
            }
            catch { return false; }   // unreadable or damaged file: behave as if nothing was saved
        }
    }

    public static void SetDonateNever()
    {
        _neverThisRun = true;
        try
        {
            var dir = Dir;
            if (dir == null) return;
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, "settings.json");
            var tmp = file + ".tmp";
            File.WriteAllText(tmp, "{ \"donate\": \"never\" }\n");
            File.Move(tmp, file, true);
        }
        catch { }
    }
}
