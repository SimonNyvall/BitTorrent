using Torrent.Encoding;


public class Program
{
    public static void Main()
    {
        var path = "/home/sn/Documents/test-torrent.txt";

        var obj = BEncoding.DecodeFile(path);

        BDecoding.EncodeToFile("/home/sn/Documents/test-torrent2.txt", obj);
    }
}



