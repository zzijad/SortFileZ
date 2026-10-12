using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SortFileZ;

public static class SortEngine
{
    /// <summary>Ključ u tabeli tipova pod kojim se vode folderi.</summary>
    public const string FolderKey = "<folder>";

    private static readonly HashSet<string> IgnoredNames =
        new(StringComparer.OrdinalIgnoreCase) { "desktop.ini", "thumbs.db", ".ds_store" };

    /// <summary>Unutrašnje ekstenzije koje su deo složene ekstenzije (Program.paf.exe, arhiva.tar.gz).</summary>
    private static readonly HashSet<string> CompoundInner =
        new(StringComparer.OrdinalIgnoreCase) { ".paf", ".tar" };

    private static readonly char[] NameSeparators = { ' ', '_', '-', '.', '(', ',' };

    private static readonly Dictionary<string, string> Categories = BuildCategories();

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int StrCmpLogicalW(string a, string b);

    /// <summary>Poređenje imena kao u Windows Exploreru (2 pre 10).</summary>
    public static readonly IComparer<string> NaturalComparer = OperatingSystem.IsWindows()
        ? Comparer<string>.Create((a, b) => StrCmpLogicalW(a, b))
        : StringComparer.OrdinalIgnoreCase;

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

    /// <summary>Ključ za tabelu tipova: ekstenzija malim slovima, ili <see cref="FolderKey"/> za foldere.</summary>
    public static string KeyFor(FileSystemInfo f) => f is DirectoryInfo ? FolderKey : f.Extension.ToLowerInvariant();

    public static string DefaultFolderFor(string key)
    {
        if (key == FolderKey) return Loc.T("Cat_Folders");
        if (string.IsNullOrEmpty(key)) return Loc.T("FolderNoExt");
        if (!Categories.TryGetValue(key, out var folder)) return key.TrimStart('.').ToUpperInvariant();
        return folder.StartsWith("Cat_", StringComparison.Ordinal) ? Loc.T(folder) : folder;
    }

    /// <summary>Deli ime na osnovu i ekstenziju. Folder nema ekstenziju; ".paf.exe" i ".tar.gz" su jedna ekstenzija.</summary>
    public static (string stem, string ext) SplitName(string name, bool isDirectory)
    {
        if (isDirectory) return (name, "");
        var ext = Path.GetExtension(name);
        var stem = name[..^ext.Length];
        var inner = Path.GetExtension(stem);
        if (inner.Length > 1 && CompoundInner.Contains(inner))
        {
            stem = stem[..^inner.Length];
            ext = inner + ext;
        }
        return (stem, ext);
    }

    // ---------------------------------------------------------------- Skeniranje

    /// <summary>Fajlovi i folderi direktno u folderu — u podfoldere se ne ulazi.</summary>
    public static List<FileSystemInfo> Scan(string folder)
    {
        var list = new List<FileSystemInfo>();
        foreach (var fi in new DirectoryInfo(folder).EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            if ((fi.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
            if (IgnoredNames.Contains(fi.Name)) continue;
            if (fi.Name.StartsWith("~$", StringComparison.Ordinal)) continue; // privremeni Office fajlovi
            list.Add(fi);
        }
        return list;
    }

    // ---------------------------------------------------------------- Čitljiva imena

    /// <summary>
    /// "CPU-ZPortable_2.16_English" → "CPU-Z Portable 2.16 English".
    /// Donje crte i tačke postaju razmaci; tačka u brojevima (2.16) i crtica između
    /// skraćenica ili brojeva (CPU-Z, 2026-10-13) ostaju; spojene reči se razdvajaju.
    /// </summary>
    public static string CleanName(string name, bool splitWords)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;

        var t = name.Replace('_', ' ');
        if (splitWords) t = SplitJoinedWords(t);

        var sb = new StringBuilder(t.Length + 8);
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            char prev = i > 0 ? t[i - 1] : ' ';
            char next = i + 1 < t.Length ? t[i + 1] : ' ';

            if (c == '.')
                sb.Append(IsNumberDot(t, i) ? '.' : ' ');
            else if (c == '-' && !char.IsWhiteSpace(prev) && !char.IsWhiteSpace(next))
                sb.Append(KeepHyphen(t, i) ? '-' : ' ');
            else
                sb.Append(c);
        }

        var result = Regex.Replace(sb.ToString(), @"\s+", " ").Trim().Trim('-').Trim();
        return result.Length == 0 ? name : result;
    }

    /// <summary>Tačka između cifara ostaje (2.16, 1.0.3), osim kad posle broja sledi slovo (2019.1080p).</summary>
    private static bool IsNumberDot(string t, int i)
    {
        if (i == 0 || i + 1 >= t.Length) return false;
        if (!char.IsDigit(t[i - 1]) || !char.IsDigit(t[i + 1])) return false;
        int j = i + 1;
        while (j < t.Length && char.IsDigit(t[j])) j++;
        return j >= t.Length || !char.IsLetter(t[j]);
    }

    /// <summary>Crtica ostaje ako su reči sa obe strane bez malih slova: CPU-Z, MP3-PRO, 2026-10-13.</summary>
    private static bool KeepHyphen(string t, int i)
    {
        int a = i - 1;
        while (a >= 0 && !char.IsWhiteSpace(t[a]) && t[a] != '-') a--;
        int b = i + 1;
        while (b < t.Length && !char.IsWhiteSpace(t[b]) && t[b] != '-') b++;
        var left = t[(a + 1)..i];
        var right = t[(i + 1)..b];

        static bool NoLower(string w) => w.Any(char.IsLetterOrDigit) && !w.Any(char.IsLower);
        return NoLower(left) && NoLower(right);
    }

    /// <summary>"ZPortable" → "Z Portable", "ProgramName" → "Program Name", "HTMLParser" → "HTML Parser"; "iPhone", "eBook", "PDFs" ostaju.</summary>
    private static string SplitJoinedWords(string t)
    {
        var sb = new StringBuilder(t.Length + 8);
        for (int i = 0; i < t.Length; i++)
        {
            char c = t[i];
            if (i > 0 && char.IsUpper(c))
            {
                char p = t[i - 1];
                if (char.IsLower(p))
                {
                    int start = i - 1;
                    while (start > 0 && char.IsLetter(t[start - 1])) start--;
                    bool singleLowerPrefix = start == i - 1; // eBook, iPhone
                    if (!singleLowerPrefix) sb.Append(' ');
                }
                else if (char.IsUpper(p))
                {
                    int j = i + 1;
                    while (j < t.Length && char.IsLower(t[j])) j++;
                    if (j - (i + 1) >= 2) sb.Append(' '); // ZPortable → Z Portable, ali PDFs ostaje
                }
            }
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Pomoćne

    public static DateTime DateFor(FileSystemInfo f, DateSource source, DateTime now) => source switch
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

    /// <summary>Novo ime: (očišćeno) originalno ime, ili šablon ime + datum + broj. Ekstenzija ostaje.</summary>
    public static string ComposeName(string originalStem, string ext, SortOptions o, DateTime date, int number)
    {
        var baseStem = o.CleanNames ? CleanName(originalStem, o.SplitWords) : originalStem;
        if (!o.Rename)
            return o.CleanNames ? SanitizeName(baseStem + ext) : originalStem + ext;

        var parts = new List<string>();
        if (o.RenameUseName)
            parts.Add(string.IsNullOrWhiteSpace(o.RenameName) ? baseStem : o.RenameName.Trim());
        if (o.RenameUseDate)
            parts.Add(FormatDate(date, DateGranularity.Day, o.IsoDateFormat));
        if (o.RenameUseNumber)
            parts.Add(number.ToString(new string('0', Math.Max(1, o.Digits)), CultureInfo.InvariantCulture));

        parts.RemoveAll(string.IsNullOrWhiteSpace);
        if (parts.Count == 0) return SanitizeName(baseStem + ext);
        return SanitizeName(string.Join(o.Separator, parts)) + ext;
    }

    public static string SanitizeName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.Select(c => invalid.Contains(c) || c == '\\' || c == '/' ? '_' : c).ToArray();
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
            .Where(seg => seg.Length > 0 && seg != "." && seg != "..");
        return string.Join(Path.DirectorySeparatorChar, segments);
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

    /// <summary>Da li je <paramref name="path"/> isti kao <paramref name="folder"/> ili unutar njega.</summary>
    public static bool IsSameOrInside(string path, string folder)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (string.Equals(p, f, StringComparison.OrdinalIgnoreCase)) return true;
        return p.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- Plan (pregled)

    private sealed class PlanEntry
    {
        public required FileSystemInfo Item { get; init; }
        public required string Stem { get; init; }
        public required string Ext { get; init; }
        /// <summary>null = fajl u režimu „svaki u svoj folder“; folder se određuje kad su imena foldera poznata.</summary>
        public string? Folder { get; set; }
        public string? TargetDir { get; set; }
        public string? SkipKey { get; set; }
        public bool IsDirectory => Item is DirectoryInfo;
    }

    public static List<PreviewItem> BuildPlan(List<FileSystemInfo> items, string scannedFolder, SortOptions o,
        IReadOnlyDictionary<string, ExtensionGroup> groups)
    {
        var now = DateTime.Now;
        var root = ResolveRoot(scannedFolder, o);

        var selected = items.Where(f => groups.TryGetValue(KeyFor(f), out var g) && g.IsSelected);
        var ordered = o.NumberOrderByDate
            ? selected.OrderBy(f => f.CreationTime).ThenBy(f => f.Name, NaturalComparer).ToList()
            : selected.OrderBy(f => f.Name, NaturalComparer).ToList();

        // 1. prolaz: odredišni folder za svaku stavku
        var canonicalGroups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var plan = new List<PlanEntry>(ordered.Count);
        foreach (var f in ordered)
        {
            bool isDir = f is DirectoryInfo;
            var (stem, ext) = SplitName(f.Name, isDir);

            string? folder = o.Mode switch
            {
                SortMode.ByType => groups[KeyFor(f)].FolderName,
                SortMode.ByDate => FormatDate(DateFor(f, o.FolderDateSource, now), o.Granularity, o.IsoDateFormat),
                SortMode.ByName => Canonical(canonicalGroups, NameGroup(stem, o.NameGrouping)),
                // Folder ostaje na mestu; za fajl se folder određuje u 3. prolazu
                SortMode.OwnFolder => isDir ? "" : null,
                SortMode.SingleFolder => string.IsNullOrWhiteSpace(o.SingleFolderName) ? Loc.T("FolderSorted") : o.SingleFolderName,
                _ => ""
            };

            var entry = new PlanEntry { Item = f, Stem = stem, Ext = ext };
            if (folder != null)
            {
                entry.Folder = SanitizeFolder(folder);
                entry.TargetDir = Path.GetFullPath(Path.Combine(root, entry.Folder));
            }
            plan.Add(entry);
        }

        // 2. prolaz: folder u koji nešto ide (ili koji bi išao sam u sebe) ostaje gde jeste
        var targetDirs = plan.Where(p => p.TargetDir != null).Select(p => p.TargetDir!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var p in plan.Where(p => p.IsDirectory))
        {
            if (targetDirs.Any(t => IsSameOrInside(t, p.Item.FullName)))
                p.SkipKey = "StDestFolder";
        }

        // Pri premeštanju originali oslobađaju svoja imena; pri kopiranju ostaju.
        var leaving = o.Copy
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(plan.Where(p => p.SkipKey == null).Select(p => p.Item.FullName), StringComparer.OrdinalIgnoreCase);

        // „Svaki fajl u svoj folder“: postojeći folder istog imena (npr. raspakovan zip) prima fajl.
        // Ključ je sadašnje ime foldera, vrednost je ime koje će folder imati posle preimenovanja.
        var dirNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (o.Mode == SortMode.OwnFolder && IsSameOrInside(root, scannedFolder) && IsSameOrInside(scannedFolder, root))
        {
            foreach (var d in items.OfType<DirectoryInfo>()) dirNames[d.Name] = d.Name;
        }

        // 3. prolaz: imena — folderi prvo, da bi fajlovi znali nova imena foldera
        var taken = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byEntry = new Dictionary<PlanEntry, PreviewItem>(plan.Count);

        foreach (var p in plan.Where(p => p.IsDirectory).Concat(plan.Where(p => !p.IsDirectory)))
        {
            var item = new PreviewItem
            {
                SourcePath = p.Item.FullName,
                OriginalName = p.Item.Name,
                IsDirectory = p.IsDirectory,
                Created = p.Item.CreationTime
            };
            byEntry[p] = item;

            if (p.SkipKey != null)
            {
                item.NewName = p.Item.Name;
                item.TargetFolder = "";
                item.Skip(p.SkipKey);
                continue;
            }

            if (p.Folder == null)
            {
                // Setup.exe → Setup\Setup.exe; fajl bez ekstenzije ne može u folder sa sopstvenim imenom
                var wanted = o.CleanNames ? CleanName(p.Stem, o.SplitWords) : p.Stem;
                if (string.IsNullOrWhiteSpace(wanted)) wanted = p.Stem;
                string folder;
                if (string.IsNullOrEmpty(p.Ext)) folder = wanted + " (folder)";
                else if (dirNames.TryGetValue(p.Stem, out var existing)) folder = existing;
                else if (dirNames.TryGetValue(wanted, out existing)) folder = existing;
                else folder = wanted;

                p.Folder = SanitizeFolder(folder);
                p.TargetDir = Path.GetFullPath(Path.Combine(root, p.Folder));
            }

            var counterKey = o.NumberPerFolder ? p.Folder! : "*";
            if (!counters.TryGetValue(counterKey, out var number)) number = o.StartNumber;
            counters[counterKey] = number + 1;

            var name = ComposeName(p.Stem, p.Ext, o, DateFor(p.Item, o.RenameDateSource, now), number);
            item.NewName = ReserveUnique(p.TargetDir!, name, p.Ext, taken, leaving);
            item.TargetFolder = p.Folder!;

            if (p.IsDirectory && o.Mode == SortMode.OwnFolder)
                dirNames[p.Item.Name] = item.NewName;
        }

        // Pregled ostaje u originalnom redosledu
        return plan.Select(p => byEntry[p]).ToList();
    }

    private static string Canonical(Dictionary<string, string> map, string value)
    {
        if (map.TryGetValue(value, out var existing)) return existing;
        map[value] = value;
        return value;
    }

    private static string ReserveUnique(string dir, string name, string ext,
        Dictionary<string, HashSet<string>> taken, HashSet<string> leaving)
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

        var stem = ext.Length > 0 && name.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? name[..^ext.Length] : name;
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

        // Folderi prvo: fajl može da ide u folder koji se upravo preimenuje
        foreach (var item in items.Where(i => i.IsDirectory).Concat(items.Where(i => !i.IsDirectory)))
        {
            if (item.State == ItemState.Ready && item.IsIncluded)
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

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);

    private static void ProcessItem(PreviewItem item, string root, bool copy, UndoLog log)
    {
        bool isDir = item.IsDirectory;
        if (isDir ? !Directory.Exists(item.SourcePath) : !File.Exists(item.SourcePath))
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

        // Folder ne može u samog sebe (npr. ručno izmenjen folder u pregledu)
        if (isDir && IsSameOrInside(dir, item.SourcePath))
        {
            item.Skip("StDestFolder");
            return;
        }

        EnsureDirectory(dir, log.CreatedDirectories);

        bool caseOnlyRename = !copy && string.Equals(target, item.SourcePath, StringComparison.OrdinalIgnoreCase);
        if (!caseOnlyRename && Exists(target))
            target = UniqueOnDisk(target, isDir);

        if (isDir)
        {
            if (copy) CopyDirectory(item.SourcePath, target);
            else MoveDirectory(item.SourcePath, target);
        }
        else
        {
            if (copy) File.Copy(item.SourcePath, target, overwrite: false);
            else File.Move(item.SourcePath, target);
        }

        log.Entries.Add(new UndoEntry { From = item.SourcePath, To = target, IsDirectory = isDir });
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

    public static string UniqueOnDisk(string path, bool isDirectory)
    {
        var dir = Path.GetDirectoryName(path) ?? "";
        var (stem, ext) = SplitName(Path.GetFileName(path), isDirectory);
        int i = 2;
        string candidate;
        do candidate = Path.Combine(dir, $"{stem} ({i++}){ext}");
        while (Exists(candidate));
        return candidate;
    }

    private static void MoveDirectory(string source, string target)
    {
        if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
        {
            // Promena samo velikih/malih slova ide preko privremenog imena
            var temp = source + ".sfz-" + Guid.NewGuid().ToString("N")[..8];
            Directory.Move(source, temp);
            Directory.Move(temp, target);
        }
        else if (string.Equals(Path.GetPathRoot(source), Path.GetPathRoot(target), StringComparison.OrdinalIgnoreCase))
        {
            Directory.Move(source, target);
        }
        else
        {
            // Drugi disk: Directory.Move to ne podržava
            CopyDirectory(source, target);
            Directory.Delete(source, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: false);
        foreach (var sub in Directory.EnumerateDirectories(source))
            CopyDirectory(sub, Path.Combine(target, Path.GetFileName(sub)));
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
                if (e.IsDirectory ? !Directory.Exists(e.To) : !File.Exists(e.To)) { failed++; continue; }

                if (log.IsCopy)
                {
                    // Kopija ide u korpu, original je netaknut
                    if (e.IsDirectory) RecycleBin.DeleteDirectory(e.To);
                    else RecycleBin.DeleteFile(e.To);
                }
                else
                {
                    bool caseOnly = string.Equals(e.From, e.To, StringComparison.OrdinalIgnoreCase);
                    var from = !caseOnly && Exists(e.From) ? UniqueOnDisk(e.From, e.IsDirectory) : e.From;
                    var parent = Path.GetDirectoryName(from);
                    if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

                    if (e.IsDirectory) MoveDirectory(e.To, from);
                    else File.Move(e.To, from);
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
