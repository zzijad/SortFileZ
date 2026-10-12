using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace SortFileZ;

public partial class MainWindow : Window
{
    private static readonly string[] Separators = { "_", "-", " ", "." };
    private const string SampleName = "CPU-ZPortable_2.16_English.paf.exe";

    private readonly List<ExtensionGroup> _extensions = new();
    private List<PreviewItem> _preview = new();
    private List<FileSystemInfo> _files = new();
    private string? _scannedFolder;

    private readonly DispatcherTimer _refreshTimer;
    private readonly AppSettings _settings;
    private bool _ready;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _refreshTimer.Tick += (_, _) =>
        {
            _refreshTimer.Stop();
            RefreshPreview();
        };

        // Svaka promena opcije automatski osvežava pregled
        OptionsPanel.AddHandler(ToggleButton.CheckedEvent, new RoutedEventHandler(OnOptionChanged));
        OptionsPanel.AddHandler(ToggleButton.UncheckedEvent, new RoutedEventHandler(OnOptionChanged));
        OptionsPanel.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnOptionChanged));
        OptionsPanel.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnOptionChanged));

        if (!string.IsNullOrEmpty(_settings.LastFolder) && Directory.Exists(_settings.LastFolder))
            FolderBox.Text = _settings.LastFolder;

        ApplyLanguage(_settings.Language);
        _ready = true;
        UndoBtn.IsEnabled = UndoLog.Exists();
        UpdateExample();
    }

    // ================================================================ Opcije

    private void OnOptionChanged(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        UpdateExample();
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (!_ready) return;
        _refreshTimer.Stop();
        _refreshTimer.Start();
    }

    private SortOptions ReadOptions()
    {
        var mode = RbDate.IsChecked == true ? SortMode.ByDate
            : RbName.IsChecked == true ? SortMode.ByName
            : RbOwn.IsChecked == true ? SortMode.OwnFolder
            : RbSingle.IsChecked == true ? SortMode.SingleFolder
            : RbNone.IsChecked == true ? SortMode.None
            : SortMode.ByType;

        int start = int.TryParse(StartBox.Text.Trim(), out var n) && n >= 0 ? n : 1;
        int sepIndex = CbSep.SelectedIndex is >= 0 and < 4 ? CbSep.SelectedIndex : 0;

        return new SortOptions
        {
            Mode = mode,
            FolderDateSource = (DateSource)Math.Max(0, CbFolderDate.SelectedIndex),
            Granularity = (DateGranularity)Math.Max(0, CbGranularity.SelectedIndex),
            IsoDateFormat = CbDateFormat.SelectedIndex != 1,
            NameGrouping = RbFirstLetter.IsChecked == true ? NameGrouping.FirstLetter : NameGrouping.Prefix,
            SingleFolderName = SingleFolderBox.Text,

            CleanNames = ChkClean.IsChecked == true,
            SplitWords = ChkSplit.IsChecked == true,

            Rename = ChkRename.IsChecked == true,
            RenameUseName = ChkRnName.IsChecked == true,
            RenameName = RnNameBox.Text,
            RenameUseDate = ChkRnDate.IsChecked == true,
            RenameDateSource = (DateSource)Math.Max(0, CbRnDate.SelectedIndex),
            RenameUseNumber = ChkRnNumber.IsChecked == true,
            StartNumber = start,
            Digits = Math.Max(0, CbDigits.SelectedIndex) + 1,
            Separator = Separators[sepIndex],
            NumberPerFolder = ChkNumPerFolder.IsChecked == true,
            NumberOrderByDate = CbOrder.SelectedIndex == 1,

            Copy = RbCopy.IsChecked == true,
            DestinationRoot = DestBox.Text
        };
    }

    private void UpdateExample()
    {
        var o = ReadOptions();
        var (stem, ext) = SortEngine.SplitName(SampleName, isDirectory: false);
        var sample = SortEngine.ComposeName(stem, ext, o, DateTime.Now, o.StartNumber);
        ExampleText.Text = $"{Loc.T("Example")}\n{SampleName}\n→ {sample}";
        ExampleBorder.Visibility = o.CleanNames || o.Rename ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================================================================ Skeniranje

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("ChooseFolder") };
        if (Directory.Exists(FolderBox.Text.Trim())) dlg.InitialDirectory = FolderBox.Text.Trim();
        if (dlg.ShowDialog(this) == true)
        {
            FolderBox.Text = dlg.FolderName;
            _ = ScanAsync();
        }
    }

    private void DestBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("ChooseDest") };
        if (Directory.Exists(DestBox.Text.Trim())) dlg.InitialDirectory = DestBox.Text.Trim();
        if (dlg.ShowDialog(this) == true) DestBox.Text = dlg.FolderName;
    }

    private void FolderBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = ScanAsync();
        }
    }

    private void Scan_Click(object sender, RoutedEventArgs e) => _ = ScanAsync();

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_busy || !e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length == 0) return;

        var path = paths[0];
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (string.IsNullOrEmpty(folder)) return;

        FolderBox.Text = folder;
        _ = ScanAsync();
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        var folder = FolderBox.Text.Trim().Trim('"');
        if (!Directory.Exists(folder))
        {
            StatusText.Text = Loc.T("MsgFolderMissing");
            return;
        }

        SetBusy(true, Loc.T("MsgScanning"), indeterminate: true);
        try
        {
            var files = await Task.Run(() => SortEngine.Scan(folder));
            _files = files;
            _scannedFolder = folder;
            _settings.LastFolder = folder;
            BuildExtensionList(keepSelection: false);
            StatusText.Text = files.Count == 0
                ? Loc.T("MsgEmpty")
                : Loc.F("MsgFound", files.Count(f => f is FileInfo), files.Count(f => f is DirectoryInfo), _extensions.Count);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F("MsgScanError", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }
        RefreshPreview();
    }

    private void BuildExtensionList(bool keepSelection)
    {
        var previous = _extensions.ToDictionary(x => x.Extension, x => x.IsSelected, StringComparer.OrdinalIgnoreCase);
        foreach (var g in _extensions) g.PropertyChanged -= Ext_PropertyChanged;
        _extensions.Clear();

        // Folderi su prvi red; ostali tipovi po broju fajlova
        var groups = _files
            .GroupBy(SortEngine.KeyFor, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key == SortEngine.FolderKey ? 0 : 1)
            .ThenByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var g in groups)
        {
            var ext = g.Key;
            var group = new ExtensionGroup
            {
                Extension = ext,
                Count = g.Count(),
                TotalBytes = g.Sum(f => f is FileInfo fi ? fi.Length : 0),
                FolderName = _settings.ExtensionFolders.TryGetValue(ext, out var custom)
                    ? custom
                    : SortEngine.DefaultFolderFor(ext),
                // Folderi se obrađuju samo kad ih korisnik štiklira
                IsSelected = keepSelection && previous.TryGetValue(ext, out var sel)
                    ? sel
                    : ext != SortEngine.FolderKey
            };
            group.PropertyChanged += Ext_PropertyChanged;
            _extensions.Add(group);
        }

        ExtGrid.ItemsSource = null;
        ExtGrid.ItemsSource = _extensions;
    }

    private void Ext_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ExtensionGroup g) return;

        if (e.PropertyName == nameof(ExtensionGroup.FolderName))
        {
            var def = SortEngine.DefaultFolderFor(g.Extension);
            if (string.Equals(g.FolderName, def, StringComparison.Ordinal))
                _settings.ExtensionFolders.Remove(g.Extension);
            else
                _settings.ExtensionFolders[g.Extension] = g.FolderName;
        }
        ScheduleRefresh();
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in _extensions) g.IsSelected = true;
    }

    private void SelectNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var g in _extensions) g.IsSelected = false;
    }

    // ================================================================ Pregled

    private void RefreshPreview()
    {
        if (_scannedFolder == null || _busy) return;

        try
        {
            var o = ReadOptions();
            var map = _extensions.ToDictionary(x => x.Extension, x => x, StringComparer.OrdinalIgnoreCase);
            foreach (var old in _preview) old.PropertyChanged -= Preview_PropertyChanged;
            _preview = SortEngine.BuildPlan(_files, _scannedFolder, o, map);
            foreach (var item in _preview) item.PropertyChanged += Preview_PropertyChanged;
            PreviewGrid.ItemsSource = _preview;
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F("MsgPreviewError", ex.Message);
        }

        UpdatePreviewSummary();
    }

    private void Preview_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PreviewItem.IsIncluded)) UpdatePreviewSummary();
    }

    private void UpdatePreviewSummary()
    {
        int folders = _preview.Where(p => p.State == ItemState.Ready).Select(p => p.TargetFolder)
            .Where(f => f.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        int renamed = _preview.Count(p => p.State == ItemState.Ready
                                          && !string.Equals(p.NewName, p.OriginalName, StringComparison.Ordinal));
        int selected = _preview.Count(p => p.IsIncluded && p.State == ItemState.Ready);
        PreviewCount.Text = _preview.Count == 0 ? "" : Loc.F("PreviewCount", _preview.Count, folders, renamed, selected);
        ApplyBtn.IsEnabled = !_busy && selected > 0;
    }

    private void PreviewAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _preview) p.IsIncluded = true;
    }

    private void PreviewNone_Click(object sender, RoutedEventArgs e)
    {
        foreach (var p in _preview) p.IsIncluded = false;
    }

    // ================================================================ Primena

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (_scannedFolder == null || _busy) return;

        PreviewGrid.CommitEdit(DataGridEditingUnit.Row, true);
        var items = _preview.Where(p => p.State == ItemState.Ready && p.IsIncluded).ToList();
        if (items.Count == 0) return;

        var o = ReadOptions();
        string root;
        try
        {
            root = SortEngine.ResolveRoot(_scannedFolder, o);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F("MsgBadDest", ex.Message);
            return;
        }

        var action = Loc.T(o.Copy ? "VerbCopy" : "VerbMove");
        var answer = MessageBox.Show(this,
            Loc.F("ConfirmApply", items.Count, action, root),
            Loc.T("ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        SetBusy(true, Loc.T("Working"), indeterminate: false);
        ProgBar.Maximum = items.Count;
        ProgBar.Value = 0;
        var progress = new Progress<int>(done =>
        {
            ProgBar.Value = done;
            StatusText.Text = Loc.F("WorkingN", done, items.Count);
        });

        try
        {
            var copy = o.Copy;
            var log = await Task.Run(() => SortEngine.Execute(items, root, copy, progress));
            if (log.Entries.Count > 0) log.Save();

            int ok = items.Count(i => i.State == ItemState.Done);
            int skipped = items.Count(i => i.State == ItemState.Skipped);
            int errors = items.Count(i => i.State == ItemState.Error);
            StatusText.Text = Loc.F("DoneSummary", ok, skipped, errors)
                              + (log.Entries.Count > 0 ? Loc.T("CanUndo") : "");

            // Osveži listu preostalih fajlova (pregled ostaje da se vide statusi)
            _files = await Task.Run(() => SortEngine.Scan(_scannedFolder));
            BuildExtensionList(keepSelection: true);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F("MsgError", ex.Message);
        }
        finally
        {
            SetBusy(false);
            _settings.Save();
        }
    }

    // ================================================================ Poništavanje

    private async void Undo_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var log = UndoLog.Load();
        if (log == null || log.Entries.Count == 0)
        {
            StatusText.Text = Loc.T("NothingToUndo");
            UndoLog.Delete();
            UndoBtn.IsEnabled = false;
            return;
        }

        var what = Loc.T(log.IsCopy ? "UndoWhatCopy" : "UndoWhatMove");
        var answer = MessageBox.Show(this,
            Loc.F("ConfirmUndo", log.Time, log.Entries.Count, what),
            Loc.T("UndoTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes) return;

        SetBusy(true, Loc.T("Undoing"), indeterminate: true);
        try
        {
            var (ok, failed) = await RunSta(() => SortEngine.Undo(log));
            if (failed == 0) UndoLog.Delete();
            StatusText.Text = failed == 0
                ? Loc.F("UndoOk", ok)
                : Loc.F("UndoPartial", ok, failed);
        }
        catch (Exception ex)
        {
            StatusText.Text = Loc.F("UndoError", ex.Message);
        }
        finally
        {
            SetBusy(false);
        }

        if (_scannedFolder != null)
        {
            var status = StatusText.Text;
            await ScanAsync();
            StatusText.Text = status;
        }
    }

    private static Task<T> RunSta<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        var thread = new Thread(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { tcs.SetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    // ================================================================ Jezik i meni

    private void LangSerbian_Click(object sender, RoutedEventArgs e) => ApplyLanguage("sr");

    private void LangEnglish_Click(object sender, RoutedEventArgs e) => ApplyLanguage("en");

    private void ApplyLanguage(string? lang)
    {
        bool wasReady = _ready;
        _ready = false; // promene ispod ne smeju da pokrenu osvežavanje više puta

        // Podrazumevani naziv foldera prati jezik, ali korisnikov naziv ostaje
        bool singleIsDefault = string.IsNullOrWhiteSpace(SingleFolderBox.Text)
                               || Loc.IsAnyTranslation("FolderSorted", SingleFolderBox.Text);
        bool statusIsDefault = string.IsNullOrEmpty(StatusText.Text)
                               || Loc.IsAnyTranslation("StatusStart", StatusText.Text);

        Loc.Instance.SetLanguage(lang);
        _settings.Language = Loc.Instance.Language;
        MiSerbian.IsChecked = Loc.Instance.Language == "sr";
        MiEnglish.IsChecked = Loc.Instance.Language == "en";

        if (singleIsDefault) SingleFolderBox.Text = Loc.T("FolderSorted");
        if (statusIsDefault) StatusText.Text = Loc.T("StatusStart");

        // Zatvoren ComboBox ne osvežava sam prikazani tekst — ponovo se bira ista stavka
        foreach (var cb in new[] { CbFolderDate, CbGranularity, CbDateFormat, CbRnDate, CbDigits, CbSep, CbOrder })
        {
            int idx = cb.SelectedIndex;
            cb.SelectedIndex = -1;
            cb.SelectedIndex = idx;
        }

        foreach (var item in _preview) item.RefreshStatus();
        if (_files.Count > 0) BuildExtensionList(keepSelection: true); // podrazumevani nazivi foldera na novom jeziku
        UpdatePreviewSummary();

        _ready = wasReady;
        UpdateExample();
        if (_preview.All(p => p.State == ItemState.Ready)) ScheduleRefresh();
        _settings.Save();
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        var version = typeof(MainWindow).Assembly.GetName().Version?.ToString(3) ?? "";
        MessageBox.Show(this, Loc.F("AboutText", version), "SortFileZ",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ================================================================ Ostalo

    private void SetBusy(bool busy, string? message = null, bool indeterminate = true)
    {
        _busy = busy;
        OptionsPanel.IsEnabled = !busy;
        ExtGrid.IsEnabled = !busy;
        PreviewGrid.IsReadOnly = busy;
        FolderBox.IsEnabled = !busy;
        ScanBtn.IsEnabled = !busy;
        BrowseBtn.IsEnabled = !busy;
        ApplyBtn.IsEnabled = !busy && _preview.Any(p => p.State == ItemState.Ready && p.IsIncluded);
        UndoBtn.IsEnabled = !busy && UndoLog.Exists();
        ProgBar.IsIndeterminate = busy && indeterminate;
        ProgBar.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        if (message != null) StatusText.Text = message;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        try { ExtGrid.CommitEdit(DataGridEditingUnit.Row, true); } catch { }
        _settings.Save();
    }
}
