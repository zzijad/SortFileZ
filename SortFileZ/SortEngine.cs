using System.Globalization;
using System.Runtime.InteropServices;

namespace SortFileZ;

public static class SortEngine
{
    private static readonly HashSet<string> IgnoredNames =
        new(StringComparer.OrdinalIgnoreCase) { "desktop.ini", "thumbs.db", ".ds_store" };

    private static readonly char[] NameSeparators = { ' ', '_', '-', '.', '(', ',' };

    private static readonly Dictionary<string, string> Categories = BuildCategories();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    /// <summary>Poređenje imena kao u Windows Exploreru (2 pre 10).</summary>
    public static readonly IComparer<string> NaturalComparer =
        Comparer<string>.Create((a, b) => StrCmpLogicalW(a, b));

    private static Dictionary<string, string> BuildCategories()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Add(string folder, params string[] exts)
        {
            foreach (var e in exts) d["." + e] = folder;
        }

        Add("PDF", "pdf");
        Add("Word", "doc", "docx", "docm", "dot", "dotx", "odt", "rtf");
        Add("Excel", "xls", "xlsx", "xlsm", "xlsb", "ods", "csv");
        Add("PowerPoint", "ppt", "pptx", "pptm", "pps", "ppsx", "odp");
        Add("Cat_Text", "txt", "md", "log");
        Add("Cat_Images", "jpg", "jpeg", "png", "gif", "bmp", "webp", "heic", "tif", "tiff", "svg", "ico", "raw", "cr2", "nef");
        Add("Cat_Video", "mp4", "avi", "mkv", "mov", "wmv", "flv", "webm", "m4v", "mpg", "mpeg", "3gp");
        Add("Cat_Music", "mp3", "wav", "flac", "aac", "m4a", "ogg", "wma");
        Add("Cat_Archives", "zip", "rar", "7z", "tar", "gz", "bz2", "xz", "iso");
        Add("Cat_Apps", "exe", "msi", "msix", "appx", "bat", "cmd", "ps1");
        Add("Cat_Shortcuts", "lnk", "url");
        Add("Cat_Web", "html", "htm", "mhtml");
        Add("Cat_Ebooks", "epub", "mobi");
        Add("Cat_Fonts", "ttf", "otf", "woff", "woff2");
        return d;
    }

    public static string NormExt(FileInfo f) => f.Extension.ToLowerInvariant();

    public static string DefaultFolderFor(string ext)
    {
        if (string.IsNullOrEmpty(ext)) return Loc.T("FolderNoExt");
        if (!Categories.TryGetValue(ext, out var folder)) return ext.TrimStart('.').ToUpperInvariant();
        return folder.StartsWith("Cat_", StringComparison.Ordinal) ? Loc.T(folder) : folder;
    }

    // ---------------------------------------------------------------- Skeniranje

    /// <summary>Vraća samo fajlove direktno u folderu — podfolderi se preskaču.</summary>
    public static List<FileInfo> Scan(string folder)
    {
        var list = new List<FileInfo>();
        foreach (var fi in new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.TopDirectoryOnly))
        {
            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            if (IgnoredNames.Contains(fi.Name)) continue;
            if (fi.Name.StartsWith("~$", StringComparison.Ordinal)) continue; // privremeni Office fajlovi
            list.Add(fi);
        }
        return list;
    }

    // ---------------------------------------------------------------- Pomoćne

    public static DateTime DateFor(FileInfo f, DateSource source, DateTime now) => source switch
    {
        DateSource.Created => f.CreationTime,
        DateSource.Modified => f.LastWriteTime,
        _ => now
    };

    public static string FormatDate(DateTime d, DateGranularity g, bool iso)
    {
        string fmt = g switch
        {
            DateGranularity.Year => "yyyy",
            DateGranularity.Month => iso ? "yyyy-MM" : "MM.yyyy",
            _ => iso ? "yyyy-MM-dd" : "dd.MM.yyyy"
        };
        return d.ToString(fmt, CultureInfo.InvariantCulture);
    }

    public static string NameGroup(string stem, NameGrouping grouping)
    {
        var t = stem.Trim();
        if (grouping == NameGrouping.FirstLetter)
        {
            if (t.Length == 0) return Loc.T("FolderOther");
            char c = t[0];
            if (char.IsLetter(c)) return char.ToUpper(c, CultureInfo.CurrentCulture).ToString();
            if (char.IsDigit(c)) return "0-9";
            return Loc.T("FolderOther");
        }

        t = t.Trim(NameSeparators);
        int idx = t.IndexOfAny(NameSeparators);
        var prefix = idx > 0 ? t[..idx] : t;
        // "Racun2024" -> "Racun", ali "2024" ostaje "2024"
        var noDigits = prefix.TrimEnd('0', '1', '2', '3', '4', '5', '6', '7', '8', '9');
        if (noDigits.Length > 0) prefix = noDigits;
        return string.IsNullOrWhiteSpace(prefix) ? Loc.T("FolderOther") : prefix;
    }

    public static string ComposeName(string originalStem, string ext, SortOptions o, DateTime date, int number)
    {
        if (!o.Rename) return originalStem + ext;

        var parts = new List<string>();
        if (o.RenameUseName)
            parts.Add(string.IsNullOrWhiteSpace(o.RenameName) ? originalStem : o.RenameName.Trim());
        if (o.RenameUseDate)
            parts.Add(FormatDate(date, DateGranularity.Day, o.IsoDateFormat));
        if (o.RenameUseNumber)
            parts.Add(number.ToString(new string('0', Math.Max(1, o.Digits)), CultureInfo.InvariantCulture));

        if (parts.Count == 0) return originalStem + ext;
        return SanitizeName(string.Join(o.Separator, parts)) + ext;
    }

    public static string SanitizeName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim().TrimEnd('.', ' ');
        return result.Length == 0 ? "_" : result;
    }

    /// <summary>Dozvoljava podfoldere ("Slike\2026"), ali ne izlazak iz odredišta ("..").</summary>
    public static string SanitizeFolder(string? rel)
    {
        if (string.IsNullOrWhiteSpace(rel)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var segments = rel.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(seg => new string(seg.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.', ' '))
            .Where(seg => seg.Length > 0);
        return string.Join("\\", segments);
    }

    public static string ResolveRoot(string scannedFolder, SortOptions o) =>
        Path.GetFullPath(string.IsNullOrWhiteSpace(o.DestinationRoot) ? scannedFolder : o.DestinationRoot.Trim().Trim('"'));

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double v = bytes;
        int u = 0;
        while (v >= 1024 && u < units.Length - 1) { v /= 1024; u++; }
        return u == 0 ? $"{bytes} B" : $"{v:0.#} {units[u]}";
    }

    // ---------------------------------------------------------------- Plan (pregled)

    public static List<PreviewItem> BuildPlan(List<FileInfo> files, string scannedFolder, SortOptions o,
        IReadOnlyDictionary<string, ExtensionGroup> extensions)
    {
        var now = DateTime.Now;
        var root = ResolveRoot(scannedFolder, o);

        var selected = files.Where(f => extensions.TryGetValue(NormExt(f), out var g) && g.IsSelected);
        var ordered = o.NumberOrderByDate
            ? selected.OrderBy(f => f.CreationTime).ThenBy(f => f.Name, NaturalComparer).ToList()
            : selected.OrderBy(f => f.Name, NaturalComparer).ToList();

        // Pri premeštanju originali oslobađaju svoja imena; pri kopiranju ostaju.
        var leaving = o.Copy
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(ordered.Select(f => f.FullName), StringComparer.OrdinalIgnoreCase);

        var taken = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var canonicalGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<PreviewItem>(ordered.Count);

        foreach (var f in ordered)
        {
            var ext = f.Extension;
            var stem = Path.GetFileNameWithoutExtension(f.Name);

            string folder = o.Mode switch
            {
                SortMode.ByType => extensions[NormExt(f)].FolderName,
                SortMode.ByDate => FormatDate(DateFor(f, o.FolderDateSource, now), o.Granularity, o.IsoDateFormat),
                SortMode.ByName => Canonical(canonicalGroups, NameGroup(stem, o.NameGrouping)),
                // Setup.exe → Setup\Setup.exe; postojeći folder istog imena (npr. raspakovan zip) se koristi.
                // Fajl bez ekstenzije ne može u folder sa istim imenom kao on sam, pa dobija dodatak.
                SortMode.OwnFolder => string.IsNullOrEmpty(ext) ? stem + " (folder)" : stem,
                SortMode.SingleFolder => string.IsNullOrWhiteSpace(o.SingleFolderName) ? Loc.T("FolderSorted") : o.SingleFolderName,
                _ => ""
            };
            folder = SanitizeFolder(folder);

            var counterKey = o.NumberPerFolder ? folder : "*";
            if (!counters.TryGetValue(counterKey, out var number)) number = o.StartNumber;
            counters[counterKey] = number + 1;

            var name = ComposeName(stem, ext, o, DateFor(f, o.RenameDateSource, now), number);
            var dir = Path.GetFullPath(Path.Combine(root, folder));
            name = ReserveUnique(dir, name, taken, leaving);

            result.Add(new PreviewItem
            {
                SourcePath = f.FullName,
                OriginalName = f.Name,
                Created = f.CreationTime,
                NewName = name,
                TargetFolder = folder
            });
        }
        return result;
    }

    private static string Canonical(Dictionary<string, string> map, string value)
    {
        if (map.TryGetValue(value, out var existing)) return existing;
        map[value] = value;
        return value;
    }

    private static string ReserveUnique(string dir, string name, Dictionary<string, HashSet<string>> taken, HashSet<string> leaving)
    {
        if (!taken.TryGetValue(dir, out var set))
        {
            set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (Directory.Exists(dir))
            {
                foreach (var p in Directory.EnumerateFileSystemEntries(dir))
                    if (!leaving.Contains(p)) set.Add(Path.GetFileName(p));
            }
            taken[dir] = set;
        }

        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        var candidate = name;
        int i = 2;
        while (set.Contains(candidate)) candidate = $"{stem} ({i++}){ext}";
        set.Add(candidate);
        return candidate;
    }

    // ---------------------------------------------------------------- Izvršavanje

    public static UndoLog Execute(IReadOnlyList<PreviewItem> items, string root, bool copy, IProgress<int> progress)
    {
        var log = new UndoLog { Time = DateTime.Now, IsCopy = copy, Root = root };
        int done = 0;

        foreach (var item in items)
        {
            if (item.State == ItemState.Ready)
            {
                try
                {
                    ProcessItem(item, root, copy, log);
                }
                catch (Exception ex)
                {
                    item.SetError(ex.Message);
                }
            }
            progress.Report(++done);
        }
        return log;
    }

    private static void ProcessItem(PreviewItem item, string root, bool copy, UndoLog log)
    {
        if (!File.Exists(item.SourcePath))
        {
            item.SetError(Loc.T("StGone"));
            return;
        }

        var folder = SanitizeFolder(item.TargetFolder);
        var dir = Path.GetFullPath(Path.Combine(root, folder));
        var name = SanitizeName(string.IsNullOrWhiteSpace(item.NewName) ? item.OriginalName : item.NewName);
        var target = Path.Combine(dir, name);

        if (!copy && string.Equals(target, item.SourcePath, StringComparison.Ordinal))
        {
            item.State = ItemState.Skipped;
            return;
        }

        EnsureDirectory(dir, log.CreatedDirectories);

        bool caseOnlyRename = !copy && string.Equals(target, item.SourcePath, StringComparison.OrdinalIgnoreCase);
        if (!caseOnlyRename && (File.Exists(target) || Directory.Exists(target)))
            target = UniqueOnDisk(target);

        if (copy) File.Copy(item.SourcePath, target, overwrite: false);
        else File.Move(item.SourcePath, target);

        log.Entries.Add(new UndoEntry { From = item.SourcePath, To = target });
        item.NewName = Path.GetFileName(target);
        item.IsCopy = copy;
        item.State = ItemState.Done;
    }

    private static void EnsureDirectory(string dir, List<string> created)
    {
        var missing = new List<string>();
        string? d = dir;
        while (d != null && !Directory.Exists(d))
        {
            missing.Add(d);
            d = Path.GetDirectoryName(d);
        }
        if (missing.Count == 0) return;
        Directory.CreateDirectory(dir);
        created.AddRange(missing);
    }

    public static string UniqueOnDisk(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int i = 2;
        string candidate;
        do candidate = Path.Combine(dir, $"{stem} ({i++}){ext}");
        while (File.Exists(candidate) || Directory.Exists(candidate));
        return candidate;
    }

    // ---------------------------------------------------------------- Poništavanje

    /// <summary>Mora se pozvati na STA niti (koristi Windows korpu za otpatke).</summary>
    public static (int ok, int failed) Undo(UndoLog log)
    {
        int ok = 0, failed = 0;
        for (int i = log.Entries.Count - 1; i >= 0; i--)
        {
            var e = log.Entries[i];
            try
            {
                if (!File.Exists(e.To)) { failed++; continue; }

                if (log.IsCopy)
                {
                    // Kopija ide u korpu, original je netaknut
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(e.To,
                        Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs,
                        Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                }
                else
                {
                    var from = File.Exists(e.From) ? UniqueOnDisk(e.From) : e.From;
                    var parent = Path.GetDirectoryName(from);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                    File.Move(e.To, from);
                }
                ok++;
            }
            catch
            {
                failed++;
            }
        }

        // Brišu se samo prazni folderi koje je program napravio, najdublji prvi
        foreach (var d in log.CreatedDirectories.OrderByDescending(x => x.Length))
        {
            try
            {
                if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any())
                    Directory.Delete(d);
            }
            catch { /* folder je u upotrebi ili nije prazan — ostaje */ }
        }
        return (ok, failed);
    }
}
