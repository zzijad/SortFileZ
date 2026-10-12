using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SortFileZ;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    protected void OnPropertyChanged(string? name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public enum SortMode { ByType, ByDate, ByName, OwnFolder, SingleFolder, None }

// Redosled odgovara stavkama u padajućim listama
public enum DateSource { Created = 0, Modified = 1, Today = 2 }
public enum DateGranularity { Day = 0, Month = 1, Year = 2 }
public enum NameGrouping { Prefix, FirstLetter }
public enum ItemState { Ready, Done, Skipped, Error }

public sealed class SortOptions
{
    public SortMode Mode { get; set; } = SortMode.ByType;

    // Sortiranje po datumu
    public DateSource FolderDateSource { get; set; } = DateSource.Created;
    public DateGranularity Granularity { get; set; } = DateGranularity.Day;
    public bool IsoDateFormat { get; set; } = true;

    // Sortiranje po imenu
    public NameGrouping NameGrouping { get; set; } = NameGrouping.Prefix;

    // Jedan folder
    public string SingleFolderName { get; set; } = "";

    // Čitljiva imena
    public bool CleanNames { get; set; }
    public bool SplitWords { get; set; } = true;

    // Preimenovanje po šablonu
    public bool Rename { get; set; }
    public bool RenameUseName { get; set; } = true;
    public string RenameName { get; set; } = "";
    public bool RenameUseDate { get; set; }
    public DateSource RenameDateSource { get; set; } = DateSource.Created;
    public bool RenameUseNumber { get; set; } = true;
    public int StartNumber { get; set; } = 1;
    public int Digits { get; set; } = 3;
    public string Separator { get; set; } = "_";
    public bool NumberPerFolder { get; set; } = true;
    public bool NumberOrderByDate { get; set; }

    // Izvršavanje
    public bool Copy { get; set; }
    public string DestinationRoot { get; set; } = "";
}

public sealed class ExtensionGroup : ObservableObject
{
    public string Extension { get; init; } = "";
    public int Count { get; init; }
    public long TotalBytes { get; init; }

    public bool IsFolder => Extension == SortEngine.FolderKey;

    public string DisplayExtension => IsFolder ? Loc.T("FolderRow")
        : string.IsNullOrEmpty(Extension) ? Loc.T("NoExtDisplay") : Extension;

    public string SizeText => IsFolder ? "—" : SortEngine.FormatSize(TotalBytes);

    private bool _isSelected = true;
    public bool IsSelected { get => _isSelected; set => Set(ref _isSelected, value); }

    private string _folderName = "";
    public string FolderName { get => _folderName; set => Set(ref _folderName, value); }
}

public sealed class PreviewItem : ObservableObject
{
    public string SourcePath { get; init; } = "";
    public string OriginalName { get; init; } = "";
    public bool IsDirectory { get; init; }
    public DateTime Created { get; init; }

    public string DisplayName => IsDirectory ? "📁 " + OriginalName : OriginalName;

    private bool _isIncluded = true;
    public bool IsIncluded { get => _isIncluded; set => Set(ref _isIncluded, value); }

    private string _newName = "";
    public string NewName { get => _newName; set => Set(ref _newName, value); }

    private string _targetFolder = "";
    public string TargetFolder { get => _targetFolder; set => Set(ref _targetFolder, value); }

    public bool IsCopy { get; set; }
    public string ErrorMessage { get; private set; } = "";

    /// <summary>Ključ prevoda za razlog preskakanja (npr. odredišni folder).</summary>
    public string? SkipKey { get; private set; }

    private ItemState _state = ItemState.Ready;
    public ItemState State
    {
        get => _state;
        set
        {
            if (Set(ref _state, value)) RefreshStatus();
        }
    }

    /// <summary>Tekst statusa se računa iz stanja, pa prati izabrani jezik.</summary>
    public string Status => State switch
    {
        ItemState.Done => Loc.T(IsCopy ? "StCopied" : "StMoved"),
        ItemState.Skipped => Loc.T(SkipKey ?? "StNoChange"),
        ItemState.Error => Loc.F("StError", ErrorMessage),
        _ => Loc.T("StReady")
    };

    public void Skip(string reasonKey)
    {
        SkipKey = reasonKey;
        State = ItemState.Skipped;
        RefreshStatus();
    }

    public void SetError(string message)
    {
        ErrorMessage = message;
        State = ItemState.Error;
        RefreshStatus();
    }

    public void RefreshStatus() => OnPropertyChanged(nameof(Status));
}
