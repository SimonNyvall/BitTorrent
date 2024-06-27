namespace Torrent.Structure.ImportExport;

using System.Text;
using Encoding;

public class Import
{
    public static Torrent LoadFromFile(string filePath, string downloadPath)
    {
        object obj = BEncoding.DecodeFile(filePath);
        string name = Path.GetFileNameWithoutExtension(filePath);

        return BEncodingObjectToTorrent(obj, name, downloadPath);
    }

    private static Torrent BEncodingObjectToTorrent(object bencoding, string name, string downloadPath)
    {
        Dictionary<string, object> obj = (Dictionary<string, object>)bencoding ?? throw new ArgumentException("Invalid torrent file");

        List<string> trackers = [];

        if (obj.ContainsKey("announce"))
        {
            trackers.Add(DecodeUTF8String(obj["announce"]));
        }

        Dictionary<string, object> info = (Dictionary<string, object>)obj["info"] ?? throw new ArgumentException("Invalid torrent file");

        List<FileItem> files = [];

        if (info.ContainsKey("name") && info.ContainsKey("length"))
        {
            files.Add(new FileItem()
            {
                Path = DecodeUTF8String(info["name"]),
                Size = (long)info["length"]
            });
        }
        else if (info.ContainsKey("files"))
        {
            long running = 0;

            foreach (var item in (List<object>)info["files"])
            {
                var dict = item as Dictionary<string, object>;

                if (dict is null || !dict.ContainsKey("path") || !dict.ContainsKey("length"))
                {
                    throw new ArgumentException("Invalid torrent file");
                }

                string path = string.Join(Path.DirectorySeparatorChar.ToString(), ((List<object>)dict["path"]).Select(x => DecodeUTF8String(x)));

                long size = (long)dict["length"];

                files.Add(new FileItem(){
                    Path = path,
                    Size = size,
                    Offset = running
                });

                running += size;
            }
        }
        else
        {
            throw new ArgumentException("Invalid torrent file");
        }

        if (!info.ContainsKey("piece length"))
        {
            throw new ArgumentException("Invalid torrent file");
        }

        int pieceSize = Convert.ToInt32(info["piece length"]);

        if (!info.ContainsKey("pieces"))
        {
            throw new ArgumentException("Invalid torrent file");
        }

        byte[] pieceHashes = (byte[])info["pieces"];

        bool? isPrivate = null;

        if (info.ContainsKey("private"))
        {
            isPrivate = (long)info["private"] == 1;
        }

        Torrent torrent = new Torrent(name, downloadPath, files, trackers, pieceSize, pieceHashes, 16384, isPrivate);

        if (obj.ContainsKey("comment"))
        {
            torrent.Comment = DecodeUTF8String(obj["comment"]);
        }

        if (obj.ContainsKey("created by"))
        {
            torrent.CreatedBy = DecodeUTF8String(obj["created by"]);
        }

        if (obj.ContainsKey("creation date"))
        {
            torrent.CreationDate = DateTime.Parse(DecodeUTF8String(obj["creation date"]));
        }

        if (obj.ContainsKey("encoding"))
        {
            torrent.Encoding = (Encoding)Enum.Parse(typeof(Encoding), DecodeUTF8String(obj["encoding"]));
        }

        return torrent;
    }

    private static string DecodeUTF8String(object obj)
    {
        var bytes = obj as byte[] ?? throw new ArgumentException("Invalid torrent file");

        return Encoding.UTF8.GetString(bytes);
    }
}