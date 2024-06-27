namespace Torrent.Structure;

public class Helper
{
    const int TorrentChunkSize = 1024;

    public static string BytesToString(long bytes)
    {
        string[] units = [ "B", "KB", "MB", "GB", "TB", "PB", "EB" ];

        if (bytes == 0)
        {
            return "0" + units[0];
        }

        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, TorrentChunkSize)));

        double num = Math.Round(bytes / Math.Pow(TorrentChunkSize, place), 1);

        return num + units[place];
    }
}