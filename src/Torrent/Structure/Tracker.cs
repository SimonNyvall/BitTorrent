namespace Torrent.Structure;

using System.Net;

public class Tracker(string address)
{
    public event EventHandler<List<IPEndPoint>> PeersUpdated;
    public string Address { get; private set; } = address;
}