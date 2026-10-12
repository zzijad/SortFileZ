using System.Text.Json;

namespace SortFileZ;

public sealed class AppSettings
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SortFileZ");

    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string FilePath => Path.Combine(Folder, "podesavanja.json");

    public string? LastFolder { get; set; }

    /// <summary>"sr" ili "en".</summary>
    public string Language { get; set; } = "sr";

    // Zapamćene opcije za imena
    public bool CleanNames { get; set; }
    public bool SplitWords { get; set; } = true;
    public bool CapitalizeFirst { get; set; }
    public bool CapitalizeWords { get; set; }

    /// <summary>Korisnički nazivi foldera po ekstenziji (npr. ".exe" → "App").</summary>
    public Dictionary<string, string> ExtensionFolders { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (s != null)
                {
                    s.ExtensionFolders = new Dictionary<string, string>(
                        s.ExtensionFolders ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
                    return s;
                }
            }
        }
        catch { /* oštećen fajl — kreće se od podrazumevanih */ }
        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch { /* podešavanja nisu kritična */ }
    }
}

public sealed class UndoEntry
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public bool IsDirectory { get; set; }
}

public sealed class UndoLog
{
    public DateTime Time { get; set; }
    public bool IsCopy { get; set; }
    public string Root { get; set; } = "";
    public List<UndoEntry> Entries { get; set; } = new();
    public List<string> CreatedDirectories { get; set; } = new();

    private static string FilePath => Path.Combine(AppSettings.Folder, "poslednja_operacija.json");

    public static bool Exists() => File.Exists(FilePath);

    public void Save()
    {
        Directory.CreateDirectory(AppSettings.Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, AppSettings.JsonOptions));
    }

    public static UndoLog? Load()
    {
        try
        {
            return File.Exists(FilePath) ? JsonSerializer.Deserialize<UndoLog>(File.ReadAllText(FilePath)) : null;
        }
        catch
        {
            return null;
        }
    }

    public static void Delete()
    {
        try { File.Delete(FilePath); } catch { }
    }
}
