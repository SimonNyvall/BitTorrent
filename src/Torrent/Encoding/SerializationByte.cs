namespace Torrent.Encoding;

internal class SerializationByte
{
    internal const byte DictionaryStart = (byte)'d';
    internal const byte DictionaryEnd = (byte)'e';
    internal const byte ListStart = (byte)'l';
    internal const byte ListEnd = (byte)'e';
    internal const byte IntegerStart = (byte)'i';
    internal const byte IntegerEnd = (byte)'e';
    internal const byte StringSeparator = (byte)':';

}