namespace Torrent.Encoding;

using System.Text;
using Torrent.Extension;

public static class BDecoding
{
    public static byte[] Encode(object obj)
    {
        MemoryStream stream = new();

        EncodeNext(stream, obj);

        return stream.ToArray();
    }

    public static void EncodeToFile(string path, object obj)
    {
        File.WriteAllBytes(path, Encode(obj));
    }

    public static void EncodeNext(MemoryStream stream, object obj)
    {
        switch (obj)
        {
            case long value:
                EncodeInterger(stream, value);
                break;
            case string value:
                EncodeString(stream, value);
                break;
            case byte[] value:
                EncodeByteArray(stream, value);
                break;
            case List<object> value:
                EncodeList(stream, value);
                break;
            case Dictionary<string, object> value:
                EncodeDictionary(stream, value);
                break;
            default:
                throw new ArgumentException("Invalid type");
        }
    }

    private static void EncodeInterger(MemoryStream stream, long value)
    {
        stream.Append(SerializationByte.IntegerStart);
        stream.Append(Encoding.UTF8.GetBytes(value.ToString()));

        stream.Append(SerializationByte.IntegerEnd);
    }

    private static void EncodeByteArray(MemoryStream stream, byte[] body)
    {
        stream.Append(Encoding.UTF8.GetBytes(Convert.ToString(body.Length)));
        stream.Append(SerializationByte.StringSeparator);

        stream.Append(body);
    }

    private static void EncodeString(MemoryStream stream, string value)
    {
        EncodeByteArray(stream, Encoding.UTF8.GetBytes(value));
    }

    private static void EncodeList(MemoryStream stream, List<object> value)
    {
        stream.Append(SerializationByte.ListStart);

        foreach (var item in value)
        {
            EncodeNext(stream, item);
        }

        stream.Append(SerializationByte.ListEnd);
    }

    private static void EncodeDictionary(MemoryStream stream, Dictionary<string, object> input)
    {
        stream.Append(SerializationByte.DictionaryStart);

        var storedKeys = input.Keys.ToList().OrderBy(keys => BitConverter.ToString(Encoding.UTF8.GetBytes(keys)));

        foreach (var key in storedKeys)
        {
            EncodeString(stream, key);
            EncodeNext(stream, input[key]);
        }

        stream.Append(SerializationByte.DictionaryEnd);
    }
}