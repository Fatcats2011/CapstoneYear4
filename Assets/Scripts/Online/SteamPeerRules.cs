/// <summary>
/// Who may connect to a Steam host, and which messages the Steam transport delivers. The patched transport
/// (Packages/com.community.netcode.transport.steamnetworkingsockets, CHANGES-DOA.md) asks the session before it accepts
/// a connection, and keeps its own copy of Deliverable: a package can't see the game's code. See docs/online-safety.md
/// </summary>
public static class SteamPeerRules
{
    /// <summary>The largest message delivered: Netcode's own are far smaller</summary>
    public const int MAX_MESSAGE = 256 * 1024;

    /// <summary>
    /// A Steam user may connect only while they're in the host's lobby (friends join the lobby first, then connect)
    /// </summary>
    public static bool Accepts(ulong steamId, ulong lobby, ILobbyService lobbies)
    {
        if (steamId == 0 || lobby == 0 || lobbies == null)
            return false;

        return lobbies.IsMember(lobby, steamId);
    }

    /// <summary>
    /// Whether a received message (its size, the channel byte included) is passed on: empty ones, or only a channel byte,
    /// and huge ones are dropped
    /// </summary>
    public static bool Deliverable(int size)
    {
        return size > 1 && size <= MAX_MESSAGE;
    }
}
