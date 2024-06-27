namespace Torrent.Structure.ImportExport;

using System.Text;
using Encoding;

public class Export
{
    public static void SaveToFile(Torrent torrent)
    {
        object obj = TorrentToBencode(torrent);

        string path = $"{torrent.Name}.torrent";

        BDecoding.EncodeToFile(path, obj);
    }

    public static object TorrentToBencode(Torrent torrent)
    {
        Dictionary<string, object> info = [];

        if (torrent.Trackers.Count == 1)
        {
            info["announce"] = Encoding.UTF8.GetBytes(torrent.Trackers[0].Address);
        }
        else
        {
            info["announce"] = torrent.Trackers.Select(x => Encoding.UTF8.GetBytes(x.Address)).ToList();
        }

        info["comment"] = Encoding.UTF8.GetBytes(torrent.Comment);
        info["created by"] = Encoding.UTF8.GetBytes(torrent.CreatedBy);
        info["creation date"] = Encoding.UTF8.GetBytes(torrent.CreationDate.ToString());
        info["info"] = TorrentInfoToBEncoding(torrent);

        return info;
    }

    private static object TorrentInfoToBEncoding(Torrent torrent)
    {
        Dictionary<string, object> dict = [];

        dict["piece length"] = (long)torrent.PieceSize;

        byte[] pieces = new byte[20 * torrent.PieceCount];

        for (int i = 0; i < torrent.PieceCount; i++)
        {
            Buffer.BlockCopy(torrent.PieceHashes[i], 0, pieces, i * 20, 20);
        }

        dict["pieces"] = pieces;

        if (torrent.IsPrivate.HasValue)
        {
            dict["private"] = torrent.IsPrivate.Value ? 1L : 0L;
        }

        if (torrent.Files.Count == 1)
        {
            dict["name"] = Encoding.UTF8.GetBytes(torrent.Files[0].Path);
            dict["length"] = torrent.Files[0].Size;
        }
        else
        {
            List<object> files = [];

            foreach (var file in torrent.Files)
            {
                Dictionary<string, object> fileDict = [];

                fileDict["path"] = file.Path.Split(Path.DirectorySeparatorChar)
                    .Select(x => (object)Encoding.UTF8.GetBytes(x)).ToList();

                fileDict["length"] = file.Size;
                files.Add(fileDict);
            }

            dict["files"] = files;
            dict["name"] = Encoding.UTF8.GetBytes(torrent.FileDirectory.Substring(0, torrent.FileDirectory.Length - 1));
        }

        return dict;
    }

}