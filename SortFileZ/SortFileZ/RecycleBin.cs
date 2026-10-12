using Microsoft.VisualBasic.FileIO;

namespace SortFileZ;

/// <summary>Brisanje u Windows korpu za otpatke (koristi se pri poništavanju kopiranja).</summary>
internal static class RecycleBin
{
    public static void DeleteFile(string path) =>
        FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);

    public static void DeleteDirectory(string path) =>
        FileSystem.DeleteDirectory(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
}
