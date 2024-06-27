namespace Torrent.Structure;

public class FileItem
{
    public string Path = string.Empty;
    public long Size;
    public long Offset;

    public string FormattedSize { get => GetFormattedSize(); }

    private string GetFormattedSize() => Helper.BytesToString(Size);

}