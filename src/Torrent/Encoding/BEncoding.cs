namespace Torrent.Encoding;

using System.Text;

public static class BEncoding
{
    public static object Decode(byte[] bytes)
    {
        IEnumerator<byte> enumerator = ((IEnumerable<byte>)bytes).GetEnumerator();
        enumerator.MoveNext();

        return DecodeNext(enumerator);
    }

    public static object DecodeNext(IEnumerator<byte> enumerator)
    {
        return enumerator.Current switch
        {
            SerializationByte.DictionaryStart => DecodeDictionary(enumerator),
            SerializationByte.ListStart => DecodeList(enumerator),
            SerializationByte.IntegerStart => DecodeInteger(enumerator),
            _ => DecodeByteArray(enumerator),
        };
    }

    public static object DecodeFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("File not found", path);
        }

        byte[] bytes = File.ReadAllBytes(path);

        return Decode(bytes);
    }

    private static long DecodeInteger(IEnumerator<byte> enumerator)
    {
        List<byte> bytes = [];

        while (enumerator.MoveNext())
        {
            if (enumerator.Current == SerializationByte.IntegerEnd)
            {
                break;
            }

            bytes.Add((byte)enumerator.Current);
        }

        string integerAsString = Encoding.UTF8.GetString(bytes.ToArray());

        return long.Parse(integerAsString);
    }

    private static byte[] DecodeByteArray(IEnumerator<byte> enumerator)
    {
        List<byte> byteLengths = [];

        do
        {
            if (enumerator.Current == SerializationByte.StringSeparator)
            {
                break;
            }

            byteLengths.Add(enumerator.Current);
        }
        while (enumerator.MoveNext());

        string lengthAsString = Encoding.UTF8.GetString(byteLengths.ToArray());

        if (!int.TryParse(lengthAsString, out int byteLength))
        {
            throw new FormatException("Invalid length");
        }

        var bytes = new byte[byteLength];

        for (int i = 0; i < byteLength; i++)
        {
            _ = enumerator.MoveNext();
            bytes[i] = enumerator.Current;
        }

        return bytes;
    }

    private static List<object> DecodeList(IEnumerator<byte> enumerator)
    {
        List<object> list = [];

        while (enumerator.MoveNext())
        {
            if (enumerator.Current == SerializationByte.ListEnd)
            {
                break;
            }

            list.Add(DecodeNext(enumerator));
        }

        return list;
    }

    private static Dictionary<string, object> DecodeDictionary(IEnumerator<byte> enumerator)
    {
        Dictionary<string, object> dict = [];
        List<string> keys = [];

        while (enumerator.MoveNext())
        {
            if (enumerator.Current == SerializationByte.DictionaryEnd)
            {
                break;
            }

            string key = Encoding.UTF8.GetString(DecodeByteArray(enumerator));

            _ = enumerator.MoveNext();

            object value = DecodeNext(enumerator);

            keys.Add(key);
            dict.Add(key, value);
        }

        var storedKeys = keys.OrderBy(key => BitConverter.ToString(Encoding.UTF8.GetBytes(key)));

        if (!keys.SequenceEqual(storedKeys))
        {
            throw new InvalidDataException("Keys are not sorted");
        }

        return dict;
    }
}