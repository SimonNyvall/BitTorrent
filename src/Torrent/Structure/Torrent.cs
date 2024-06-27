namespace Torrent.Structure;

using System.Data;
using System.Data.Common;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Encoding;
using ImportExport;

public class Torrent // TODO: This need a major refactor
{
    private object[] fileWriteLocks;
    private static SHA1 sha1 = SHA1.Create();

    public Torrent(
        string name,
        string location,
        List<FileItem> files,
        List<string> trackers,
        int pieceSize,
        byte[]? pieceHashes = null,
        int blockSize = 16384,
        bool? isPrivate = false
    )
    {
        Name = name;
        DownloadDirectory = location;
        Files = files;
        fileWriteLocks = new object[Files.Count];

        for (int i = 0; i < Files.Count; i++)
        {
            fileWriteLocks[i] = new object();
        }

        if (trackers is not null)
        {
            foreach (string url in trackers)
            {
                Tracker tracker = new(url);
                Trackers.Add(tracker);
                tracker.PeersUpdated += HandlePeersUpdated;
            }
        }

        PieceSize = pieceSize;
        BlockSize = blockSize;
        IsPrivate = isPrivate;

        int count = Convert.ToInt32(Math.Ceiling(TotalSize / Convert.ToDouble(PieceSize)));

        PieceHashes = new byte[count][];
        IsPieceVerified = new bool[count];
        IsBlockAquired = new bool[count][];

        for (int i = 0; i < count; i++)
        {
            IsBlockAquired[i] = new bool[GetBlockCount(i)];
        }

        if (pieceHashes is null)
        {
            for (int i = 0; i < count; i++)
            {
                PieceHashes[i] = GetHash(i);
            }
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                PieceHashes[i] = new byte[20];
                Buffer.BlockCopy(pieceHashes, i * 20, PieceHashes[i], 0, 20);
            }
        }

        object info = Export.TorrentToBencode(this);
        byte[] bytes = BDecoding.Encode(info);
        Infohash = SHA1.Create().ComputeHash(bytes);

        for (int i = 0; i < PieceCount; i++)
        {
            Verify(i);
        }
    }

    public event EventHandler<int> PieceVerified;
    public event EventHandler<List<IPEndPoint>> PeersUpdated;

    public string Name { get; private set; } = string.Empty;
    public bool? IsPrivate { get; private set; }

    public List<FileItem> Files { get; private set; } = [];
    public string FileDirectory { get => GetFileDirectoryPath(); }
    public string DownloadDirectory { get; private set; } = string.Empty;
    public List<Tracker> Trackers { get; } = [];

    public string Comment { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreationDate { get; set; }
    public Encoding? Encoding { get; set; }

    public int BlockSize { get; private set; }
    public int PieceSize { get; private set; }
    public long TotalSize { get => GetTotalSize(); }

    public byte[][]? PieceHashes { get; private set; }
    public bool[] IsPieceVerified { get; private set; } = [];
    public bool[][] IsBlockAquired { get; private set; } = [];
    
    public byte[] Infohash { get; private set; } = [20];
    public string HexStringInfohash { get => GetInfohash(); }
    public string UrlSafeStringInfohash { get => GetUrlSafeInfohash(); }

    public string FormattedPieceSize { get => Helper.BytesToString(PieceSize); }
    public string FOrmattedTotalSize { get => Helper.BytesToString(TotalSize); }

    public int PieceCount { get => GetPieceCount(); }

    public string VerifiedPieceString { get => GetVerifiedPieceString(); }
    public int VerifiedPieceCount { get => GetVerifiedPieceCount(); }
    public double VerifiedRatio { get => (double)VerifiedPieceCount / PieceCount; } // TODO: Seperate this
    public bool IsCompleted { get => VerifiedPieceCount == PieceCount; }
    public bool IsStarted { get => VerifiedPieceCount > 0; }

    public long Uploaded { get; set; } = 0;
    public long Downloaded { get => PieceSize * VerifiedPieceCount; } // This is not accurate
    public long Left { get => TotalSize - Downloaded; }

    public int GetBlockSize(int piece, int block)
    {
        if (block == GetBlockCount(piece) - 1)
        {
            int remainder = Convert.ToInt32(GetPieceSize(piece) % BlockSize);

            if (remainder != 0)
            {
                return remainder;
            }
        }

        return BlockSize;
    }

    public int GetBlockCount(int piece)
    {
        return Convert.ToInt32(Math.Ceiling(GetPieceSize(piece) / (double)BlockSize));
    }

    public int GetPieceSize(int piece)
    {
        if (piece == PieceCount - 1)
        {
            int remainder = Convert.ToInt32(TotalSize % PieceSize);
            
            if (remainder != 0)
            {
                return remainder;
            }
        }

        return PieceSize;
    }

    private void HandlePeersUpdated(object sender, List<IPEndPoint> endPoints)
    {
        var handler = PeersUpdated;
        
        if (handler is not null)
        {
            handler(sender, endPoints);
        }
    }

    private void Verify(int piece)
    {
        byte[] hash = GetHash(piece);

        bool isVerified = (hash is not null && hash.SequenceEqual(PieceHashes[piece]));

        if (isVerified)
        {
            IsPieceVerified[piece] = true;

            for (int i = 0; i < IsBlockAquired[piece].Length; i++)
            {
                IsBlockAquired[piece][i] = true;
            }

            var handler = PieceVerified;

            if (handler is not null)
            {
                handler(this, piece);
            }

            return;
        }

        IsPieceVerified[piece] = false;

        if (IsBlockAquired[piece].All(x => x))
        {
            for (int i = 0; i < IsBlockAquired[piece].Length; i++)
            {
                IsBlockAquired[piece][i] = false;
            }
        }
    }

    public byte[]? GetHash(int piece) // TODO: Move this into the helper class
    {
        byte[] data = ReadPiece(piece)!;

        if (data is null)
        {
            return null;
        }

        return sha1.ComputeHash(data);
    }

    // TODO: Move this into the helper class
    public byte[]? ReadPiece(int piece)
    {
        return Read(piece * PieceSize, GetPieceSize(piece));
    }

    public byte[]? ReadBlock(int piece, int offset, int length)
    {
        return Read(piece * PieceSize + offset, length);
    }
    // ---

    // TODO: Seperate these read and write into a seperate class
    public byte[]? Read(long start, int length)
    {
        long end = start + length;
        byte[] buffer = new byte[length];

        for (int i = 0; i < Files.Count; i++)
        {
            if ((start < Files[i].Offset && end < Files[i].Offset) || (start > Files[i].Offset + Files[i].Size && end > Files[i].Offset + Files[i].Size))
            {
                continue;
            }

            string filePath = DownloadDirectory + Path.DirectorySeparatorChar + FileDirectory + Files[i].Path;

            if (!File.Exists(filePath))
            {
                return null;
            }

            long fileStart = Math.Max(0, start - Files[i].Offset);
            long fileEnd = Math.Min(end - Files[i].Offset, Files[i].Size);
            int fileLength = Convert.ToInt32(fileEnd - fileStart);
            int bufferStart = Math.Max(0, Convert.ToInt32(Files[i].Offset - start));

            using Stream stram = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            stram.Seek(fileStart, SeekOrigin.Begin);
            stram.Read(buffer, bufferStart, fileLength);
        }

        return buffer;
    }

    public void Write(long start, byte[] bytes)
    {
        long end = start + bytes.Length;

        for (int i = 0; i < Files.Count; i++)
        {
            if ((start < Files[i].Offset && end < Files[i].Offset) || (start > Files[i].Offset + Files[i].Size && end > Files[i].Offset + Files[i].Size))
            {
                continue;
            }

            string filePath = DownloadDirectory + Path.DirectorySeparatorChar + FileDirectory + Files[i].Path;

            string directory = Path.GetDirectoryName(filePath)!;

            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            lock (fileWriteLocks[i])
            {
                using Stream stram = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);

                long fileStart = Math.Max(0, start - Files[i].Offset);
                long fileEnd = Math.Min(end - Files[i].Offset, Files[i].Size);
                int fileLength = Convert.ToInt32(fileEnd - fileStart);
                int bufferStart = Math.Max(0, Convert.ToInt32(Files[i].Offset - start));

                stram.Seek(fileStart, SeekOrigin.Begin);
                stram.Write(bytes, bufferStart, fileLength);
            }
        }
    }
    // ---

    // TODO: Seperate these into a seperate class
    public static long DateTimeToUnixTimestamp(DateTime time)
    {
        return Convert.ToInt64((DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds);
    }
    // ---


    private string GetFileDirectoryPath()
    {
        if (Files.Count == 1)
        {
            return Name + Path.DirectorySeparatorChar;
        }

        return string.Empty;
    }

    private long GetTotalSize()
    {
        return Files.Sum(f => f.Size);
    }

    private string GetInfohash()
    {
        return string.Join(string.Empty, Infohash.Select(b => b.ToString("x2")));
    }

    private string GetUrlSafeInfohash()
    {
        return Encoding.UTF8.GetString(WebUtility.UrlDecodeToBytes(Infohash, 0, 20));
    }

    private int GetPieceCount()
    {
        if (PieceHashes is null)
        {
            return 0;
        }

        return PieceHashes.Length;
    }

    private string GetVerifiedPieceString()
    {
        return string.Join(string.Empty, IsPieceVerified.Select(b => b ? 1 : 0));
    }

    private int GetVerifiedPieceCount()
    {
        return IsPieceVerified.Count(b => b);
    }
}