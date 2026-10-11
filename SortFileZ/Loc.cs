using System.ComponentModel;
using System.Globalization;

namespace SortFileZ;

/// <summary>
/// Prevodi. XAML se vezuje na indeksator ({Binding [Kljuc]}), pa promena jezika
/// odmah osvežava ceo interfejs bez restarta.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    private static readonly Dictionary<string, string> Sr = new()
    {
        // ---- Meni
        ["MenuLanguage"] = "_Jezik",
        ["MenuHelp"] = "_Pomoć",
        ["MenuAbout"] = "_O programu",
        ["AboutText"] = "SortFileZ {0}\n\nSortiranje, pakovanje u foldere i preimenovanje fajlova.\nPodešavanja: %LOCALAPPDATA%\\SortFileZ",

        // ---- Zaglavlje
        ["Subtitle"] = "Obrađuju se samo fajlovi u izabranom folderu — podfolderi se preskaču.",
        ["FolderTip"] = "Putanja foldera. Folder možeš i da prevučeš mišem na prozor. Enter = skeniraj.",
        ["Browse"] = "Izaberi folder…",
        ["Scan"] = "Skeniraj",

        // ---- 1. Način sortiranja
        ["Card1"] = "1. Način sortiranja",
        ["ModeType"] = "Po tipu fajla (PDF → PDF, EXE → Aplikacije…)",
        ["ModeTypeHint"] = "Naziv foldera za svaki tip menjaš u tabeli „Tipovi fajlova“ desno (dvoklik na naziv). Program pamti tvoje nazive.",
        ["ModeDate"] = "Po datumu",
        ["WhichDate"] = "Koji datum",
        ["DateCreated"] = "Datum kreiranja fajla",
        ["DateModified"] = "Datum izmene fajla",
        ["DateToday"] = "Današnji datum",
        ["FolderPer"] = "Folder po",
        ["Day"] = "Danu",
        ["Month"] = "Mesecu",
        ["Year"] = "Godini",
        ["DateFormat"] = "Format datuma",
        ["DateNote"] = "Napomena: kod kopiranih ili preuzetih fajlova „datum kreiranja“ je trenutak kopiranja. Tada je tačniji „datum izmene“.",
        ["ModeName"] = "Po imenu",
        ["NamePrefix"] = "Po zajedničkom početku (Ugovor_Ana → Ugovor)",
        ["NameLetter"] = "Po prvom slovu (A, B, C…)",
        ["ModeOwn"] = "Svaki fajl u svoj folder (Setup.exe → Setup\\)",
        ["ModeOwnHint"] = "Folder dobija ime fajla bez ekstenzije. Ako folder sa tim imenom već postoji (npr. raspakovan zip), fajl ide u njega. Štikliraj desno samo tipove koje želiš (npr. .exe i .zip).",
        ["ModeSingle"] = "Sve u jedan folder",
        ["FolderName"] = "Naziv foldera",
        ["ModeNone"] = "Bez premeštanja (samo preimenovanje)",

        // ---- 2. Preimenovanje
        ["Card2"] = "2. Preimenovanje",
        ["RenameCheck"] = "Preimenuj fajlove",
        ["RenameHint"] = "Štikliraj delove koje novo ime treba da sadrži:",
        ["RnName"] = "Ime",
        ["RnNameTip"] = "Npr. Ekskurzija. Prazno = zadržava se originalno ime fajla.",
        ["RnDate"] = "Datum",
        ["RnNumber"] = "Broj",
        ["From"] = "od",
        ["Digits"] = "cifara",
        ["Separator"] = "Razdvajač",
        ["SepUnderscore"] = "_   donja crta",
        ["SepDash"] = "-   crtica",
        ["SepSpace"] = "razmak",
        ["SepDot"] = ".   tačka",
        ["NumOrder"] = "Redosled brojeva",
        ["OrderName"] = "Po imenu",
        ["OrderCreated"] = "Po datumu kreiranja",
        ["NumPerFolder"] = "Brojanje kreće ispočetka u svakom folderu",
        ["Example"] = "Primer:",

        // ---- 3. Izvršavanje
        ["Card3"] = "3. Izvršavanje",
        ["OpMove"] = "Premesti fajlove",
        ["OpCopy"] = "Kopiraj (originali ostaju gde jesu)",
        ["Destination"] = "Odredište",
        ["DestTip"] = "Izaberi drugi folder za odredište",
        ["DestHint"] = "Prazno = novi folderi se prave unutar skeniranog foldera.",

        // ---- Tabele
        ["ExtTitle"] = "Tipovi fajlova u folderu",
        ["ExtHint"] = "Obrađuju se samo štiklirani tipovi — npr. ostavi samo .pdf da spakuješ PDF-ove u folder.",
        ["SelectAll"] = "Označi sve",
        ["SelectNone"] = "Poništi izbor",
        ["ColExt"] = "Ekstenzija",
        ["ColFiles"] = "Fajlova",
        ["ColSize"] = "Veličina",
        ["ColTarget"] = "Ide u folder (dvoklik za izmenu)",
        ["PreviewTitle"] = "Pregled pre primene",
        ["PreviewHint"] = "Ništa se ne menja dok ne klikneš „Primeni“. Novo ime i folder možeš ručno da izmeniš dvoklikom.",
        ["ColOriginal"] = "Originalno ime",
        ["ColNewName"] = "Novo ime",
        ["ColFolder"] = "Folder",
        ["ColCreated"] = "Kreiran",
        ["ColStatus"] = "Status",
        ["PreviewCount"] = "{0} fajlova  ·  {1} foldera  ·  {2} novih imena",

        // ---- Podnožje
        ["StatusStart"] = "Izaberi folder i klikni „Skeniraj“ (ili prevuci folder na prozor).",
        ["Undo"] = "Poništi poslednju operaciju",
        ["Apply"] = "Primeni",

        // ---- Nazivi foldera
        ["NoExtDisplay"] = "(bez ekstenzije)",
        ["FolderNoExt"] = "Bez ekstenzije",
        ["FolderOther"] = "Ostalo",
        ["FolderSorted"] = "Sortirano",
        ["Cat_Text"] = "Tekst",
        ["Cat_Images"] = "Slike",
        ["Cat_Video"] = "Video",
        ["Cat_Music"] = "Muzika",
        ["Cat_Archives"] = "Arhive",
        ["Cat_Apps"] = "Aplikacije",
        ["Cat_Shortcuts"] = "Prečice",
        ["Cat_Web"] = "Web",
        ["Cat_Ebooks"] = "eKnjige",
        ["Cat_Fonts"] = "Fontovi",

        // ---- Statusi
        ["StReady"] = "Spremno",
        ["StMoved"] = "Premešteno",
        ["StCopied"] = "Kopirano",
        ["StNoChange"] = "Bez promene",
        ["StError"] = "Greška: {0}",
        ["StGone"] = "Fajl više ne postoji",

        // ---- Poruke
        ["ChooseFolder"] = "Izaberi folder za sortiranje",
        ["ChooseDest"] = "Izaberi odredište",
        ["MsgFolderMissing"] = "Taj folder ne postoji.",
        ["MsgScanning"] = "Skeniram…",
        ["MsgEmpty"] = "U folderu nema fajlova (podfolderi se preskaču).",
        ["MsgFound"] = "Pronađeno {0} fajlova, {1} tipova. Podfolderi su preskočeni.",
        ["MsgScanError"] = "Greška pri skeniranju: {0}",
        ["MsgPreviewError"] = "Greška pri pravljenju pregleda: {0}",
        ["MsgBadDest"] = "Neispravno odredište: {0}",
        ["VerbCopy"] = "kopiranje",
        ["VerbMove"] = "premeštanje/preimenovanje",
        ["ConfirmApply"] = "Biće obrađeno {0} fajlova (operacija: {1}).\n\nOdredište:\n{2}\n\nNastaviti?",
        ["ConfirmTitle"] = "Potvrda",
        ["Working"] = "Radim…",
        ["WorkingN"] = "Radim… {0} / {1}",
        ["DoneSummary"] = "Gotovo: {0} obrađeno, {1} bez promene, {2} grešaka.",
        ["CanUndo"] = " Može se poništiti.",
        ["MsgError"] = "Greška: {0}",
        ["NothingToUndo"] = "Nema operacije za poništavanje.",
        ["UndoWhatCopy"] = "Kopije će biti premeštene u korpu za otpatke.",
        ["UndoWhatMove"] = "Fajlovi će biti vraćeni na staro mesto i staro ime.",
        ["ConfirmUndo"] = "Poništiti operaciju od {0:dd.MM.yyyy HH:mm} ({1} fajlova)?\n\n{2}",
        ["UndoTitle"] = "Poništavanje",
        ["Undoing"] = "Vraćam…",
        ["UndoOk"] = "Poništeno: vraćeno {0} fajlova.",
        ["UndoPartial"] = "Vraćeno {0} fajlova, {1} nije moguće vratiti (pomereni, obrisani ili zauzeti).",
        ["UndoError"] = "Greška pri poništavanju: {0}",
        ["Unexpected"] = "Neočekivana greška:\n\n{0}",
    };

    private static readonly Dictionary<string, string> En = new()
    {
        ["MenuLanguage"] = "_Language",
        ["MenuHelp"] = "_Help",
        ["MenuAbout"] = "_About",
        ["AboutText"] = "SortFileZ {0}\n\nSort, pack into folders and rename files.\nSettings: %LOCALAPPDATA%\\SortFileZ",

        ["Subtitle"] = "Only files in the selected folder are processed — subfolders are skipped.",
        ["FolderTip"] = "Folder path. You can also drag a folder onto the window. Enter = scan.",
        ["Browse"] = "Choose folder…",
        ["Scan"] = "Scan",

        ["Card1"] = "1. Sorting method",
        ["ModeType"] = "By file type (PDF → PDF, EXE → Apps…)",
        ["ModeTypeHint"] = "Change the folder name for each type in the “File types” table on the right (double-click the name). Your names are remembered.",
        ["ModeDate"] = "By date",
        ["WhichDate"] = "Which date",
        ["DateCreated"] = "File creation date",
        ["DateModified"] = "File modified date",
        ["DateToday"] = "Today's date",
        ["FolderPer"] = "Folder per",
        ["Day"] = "Day",
        ["Month"] = "Month",
        ["Year"] = "Year",
        ["DateFormat"] = "Date format",
        ["DateNote"] = "Note: for copied or downloaded files the “creation date” is the moment of copying. The “modified date” is more accurate then.",
        ["ModeName"] = "By name",
        ["NamePrefix"] = "By common prefix (Contract_Ann → Contract)",
        ["NameLetter"] = "By first letter (A, B, C…)",
        ["ModeOwn"] = "Each file into its own folder (Setup.exe → Setup\\)",
        ["ModeOwnHint"] = "The folder is named after the file without its extension. If a folder with that name already exists (e.g. an unzipped archive), the file goes into it. Tick only the types you want on the right (e.g. .exe and .zip).",
        ["ModeSingle"] = "Everything into one folder",
        ["FolderName"] = "Folder name",
        ["ModeNone"] = "Don't move (rename only)",

        ["Card2"] = "2. Renaming",
        ["RenameCheck"] = "Rename files",
        ["RenameHint"] = "Tick the parts the new name should contain:",
        ["RnName"] = "Name",
        ["RnNameTip"] = "E.g. Field trip. Empty = keep the original file name.",
        ["RnDate"] = "Date",
        ["RnNumber"] = "Number",
        ["From"] = "from",
        ["Digits"] = "digits",
        ["Separator"] = "Separator",
        ["SepUnderscore"] = "_   underscore",
        ["SepDash"] = "-   dash",
        ["SepSpace"] = "space",
        ["SepDot"] = ".   dot",
        ["NumOrder"] = "Numbering order",
        ["OrderName"] = "By name",
        ["OrderCreated"] = "By creation date",
        ["NumPerFolder"] = "Restart numbering in each folder",
        ["Example"] = "Example:",

        ["Card3"] = "3. Execution",
        ["OpMove"] = "Move files",
        ["OpCopy"] = "Copy (originals stay where they are)",
        ["Destination"] = "Destination",
        ["DestTip"] = "Choose a different destination folder",
        ["DestHint"] = "Empty = new folders are created inside the scanned folder.",

        ["ExtTitle"] = "File types in folder",
        ["ExtHint"] = "Only ticked types are processed — e.g. keep only .pdf to pack the PDFs into a folder.",
        ["SelectAll"] = "Select all",
        ["SelectNone"] = "Select none",
        ["ColExt"] = "Extension",
        ["ColFiles"] = "Files",
        ["ColSize"] = "Size",
        ["ColTarget"] = "Goes to folder (double-click to edit)",
        ["PreviewTitle"] = "Preview before applying",
        ["PreviewHint"] = "Nothing changes until you click “Apply”. You can edit the new name and folder by double-clicking.",
        ["ColOriginal"] = "Original name",
        ["ColNewName"] = "New name",
        ["ColFolder"] = "Folder",
        ["ColCreated"] = "Created",
        ["ColStatus"] = "Status",
        ["PreviewCount"] = "{0} files  ·  {1} folders  ·  {2} new names",

        ["StatusStart"] = "Choose a folder and click “Scan” (or drag a folder onto the window).",
        ["Undo"] = "Undo last operation",
        ["Apply"] = "Apply",

        ["NoExtDisplay"] = "(no extension)",
        ["FolderNoExt"] = "No extension",
        ["FolderOther"] = "Other",
        ["FolderSorted"] = "Sorted",
        ["Cat_Text"] = "Text",
        ["Cat_Images"] = "Images",
        ["Cat_Video"] = "Video",
        ["Cat_Music"] = "Music",
        ["Cat_Archives"] = "Archives",
        ["Cat_Apps"] = "Apps",
        ["Cat_Shortcuts"] = "Shortcuts",
        ["Cat_Web"] = "Web",
        ["Cat_Ebooks"] = "eBooks",
        ["Cat_Fonts"] = "Fonts",

        ["StReady"] = "Ready",
        ["StMoved"] = "Moved",
        ["StCopied"] = "Copied",
        ["StNoChange"] = "No change",
        ["StError"] = "Error: {0}",
        ["StGone"] = "File no longer exists",

        ["ChooseFolder"] = "Choose a folder to sort",
        ["ChooseDest"] = "Choose destination",
        ["MsgFolderMissing"] = "That folder does not exist.",
        ["MsgScanning"] = "Scanning…",
        ["MsgEmpty"] = "No files in the folder (subfolders are skipped).",
        ["MsgFound"] = "Found {0} files, {1} types. Subfolders were skipped.",
        ["MsgScanError"] = "Scan error: {0}",
        ["MsgPreviewError"] = "Error building preview: {0}",
        ["MsgBadDest"] = "Invalid destination: {0}",
        ["VerbCopy"] = "copy",
        ["VerbMove"] = "move/rename",
        ["ConfirmApply"] = "{0} files will be processed (operation: {1}).\n\nDestination:\n{2}\n\nContinue?",
        ["ConfirmTitle"] = "Confirm",
        ["Working"] = "Working…",
        ["WorkingN"] = "Working… {0} / {1}",
        ["DoneSummary"] = "Done: {0} processed, {1} unchanged, {2} errors.",
        ["CanUndo"] = " Can be undone.",
        ["MsgError"] = "Error: {0}",
        ["NothingToUndo"] = "Nothing to undo.",
        ["UndoWhatCopy"] = "The copies will be moved to the Recycle Bin.",
        ["UndoWhatMove"] = "Files will be returned to their original location and name.",
        ["ConfirmUndo"] = "Undo the operation from {0:dd.MM.yyyy HH:mm} ({1} files)?\n\n{2}",
        ["UndoTitle"] = "Undo",
        ["Undoing"] = "Undoing…",
        ["UndoOk"] = "Undone: {0} files restored.",
        ["UndoPartial"] = "{0} files restored, {1} could not be restored (moved, deleted or in use).",
        ["UndoError"] = "Undo error: {0}",
        ["Unexpected"] = "Unexpected error:\n\n{0}",
    };

    public static Loc Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language { get; private set; } = "sr";

    public string this[string key] => T(key);

    public static string T(string key)
    {
        var dict = Instance.Language == "en" ? En : Sr;
        if (dict.TryGetValue(key, out var value)) return value;
        return Sr.TryGetValue(key, out var fallback) ? fallback : key;
    }

    public static string F(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, T(key), args);

    /// <summary>Isti ključ na oba jezika — koristi se da se prepozna podrazumevani tekst.</summary>
    public static bool IsAnyTranslation(string key, string value) =>
        (Sr.TryGetValue(key, out var s) && s == value) || (En.TryGetValue(key, out var e) && e == value);

    public void SetLanguage(string? lang)
    {
        Language = lang == "en" ? "en" : "sr";
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
    }
}
