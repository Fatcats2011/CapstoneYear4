using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// Matchmaking: which public lobbies a searching host may join, when an open lobby starts on its own, and the copy the
/// player sees while it happens
/// </summary>
public static class MatchRules
{
    public const string MM_KEY = "mm";
    public const string MM_ON = "1";
    public const string OPEN_KEY = "open";
    /// <summary>The lobby tag where the host publishes its Steam ID (Steam only tells members who a lobby's owner is)</summary>
    public const string OWNER_KEY = "owner";

    /// <summary>
    /// The Steam member cap of a lobby. Above the seats on purpose: Steam counts lobby members, not seats, so a cap of
    /// four would let three accounts that never connect fill the lobby. Seats are held to four by JoinRules and the open tag
    /// </summary>
    public const int LOBBY_MEMBERS = 16;

    public const int MAX_RESULTS = 50;
    public const int MAX_TAG = 64;
    // The most digits a Steam ID (a ulong) has
    const int MAX_OWNER_DIGITS = 20;
    public const double SEARCH_GAP = 5.0;
    /// <summary>How long a search waits for Steam's answer before it counts as none found</summary>
    public const double SEARCH_TIMEOUT = 10.0;
    public const int MAX_TRIES = 3;
    /// <summary>The most found lobbies one search moves into, whatever each failed for (no free seat too)</summary>
    public const int MAX_ATTEMPTS = 10;
    public const double RESEARCH = 15.0;
    public const double AUTO_START = 60.0;
    public const double FORCE_GRACE = 5.0;

    /// <summary>Tests: how long an open lobby waits before it starts by itself, instead of AUTO_START (0 = AUTO_START)</summary>
    internal static double AutoStartSecondsOverride;

    // How long an open lobby waits before it starts by itself
    static double AutoStartSeconds { get { return AutoStartSecondsOverride > 0 ? AutoStartSecondsOverride : AUTO_START; } }

    public const string SEARCHING = "LOOKING FOR A MATCH…";
    public const string NONE_FOUND = "No open matches right now. Your lobby is open to anyone.";
    /// <summary>None found, and the player's lobby couldn't be opened (it was left, and can't be hosted again yet)</summary>
    public const string NONE_FOUND_CLOSED = "No open matches right now.";
    public const string OPEN_TO_ANYONE = "OPEN TO ANYONE";
    public const string FRIENDS_ONLY = "FRIENDS ONLY";
    public const string FIND_MATCH = "FIND MATCH";
    public const string FIND_REFUSED = "Find a match before others join your lobby.";

    /// <summary>A public lobby this machine could join</summary>
    public struct Candidate
    {
        public ulong Lobby;
        public ulong Owner;
        public int OpenSeats;
    }

    /// <summary>
    /// A lobby's free seats from its tag, 0 to 4, or -1 when the tag is missing or isn't a plain number in range
    /// (another machine writes it, so it is never trusted)
    /// </summary>
    public static int ParseOpenSeats(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length > MAX_TAG)
            return -1;

        foreach (char c in tag)
        {
            if (c < '0' || c > '9')
                return -1;
        }

        int seats;
        if (!int.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out seats))
            return -1;

        return seats <= 4 ? seats : -1;
    }

    /// <summary>
    /// A lobby host's Steam ID from its owner tag, or 0 when the tag is missing or isn't plain digits that fit
    /// (another machine writes it, so it is never trusted)
    /// </summary>
    public static ulong ParseOwner(string tag)
    {
        if (string.IsNullOrEmpty(tag) || tag.Length > MAX_OWNER_DIGITS)
            return 0;

        foreach (char c in tag)
        {
            if (c < '0' || c > '9')
                return 0;
        }

        ulong owner;
        return ulong.TryParse(tag, NumberStyles.None, CultureInfo.InvariantCulture, out owner) ? owner : 0;
    }

    /// <summary>
    /// Whether a lobby's matchmaking tag marks it open to anyone
    /// </summary>
    public static bool IsOpen(string mmTag)
    {
        return mmTag == MM_ON;
    }

    /// <summary>
    /// The lobbies worth joining, fullest first (then lowest id): this game and build, open to anyone, with room for
    /// the seats needed, and not this machine's own. Only the first MAX_RESULTS found are looked at
    /// </summary>
    public static List<Candidate> Candidates(
        IEnumerable<(ulong lobby, ulong owner, string game, string build, string mm, string open)> found,
        string myBuild, ulong ownLobby, int seatsNeeded)
    {
        List<Candidate> result = new List<Candidate>();
        if (found == null)
            return result;

        int looked = 0;
        foreach (var f in found)
        {
            if (looked++ >= MAX_RESULTS)
                break;

            if (f.lobby == 0 || f.lobby == ownLobby)
                continue;
            if (f.game != LobbyRules.GAME || f.build != myBuild || !IsOpen(f.mm))
                continue;

            int seats = ParseOpenSeats(f.open);
            if (seats < 0 || seats < seatsNeeded)
                continue;

            result.Add(new Candidate { Lobby = f.lobby, Owner = f.owner, OpenSeats = seats });
        }

        result.Sort((a, b) =>
        {
            int bySeats = a.OpenSeats.CompareTo(b.OpenSeats);
            return bySeats != 0 ? bySeats : a.Lobby.CompareTo(b.Lobby);
        });
        return result;
    }

    /// <summary>
    /// Whether a lone Fill host may join another's lobby: only the higher Steam ID yields, so two hosts never join each other.
    /// An unknown owner (0: no readable owner tag) is never yielded to
    /// </summary>
    public static bool MayYieldTo(ulong me, ulong otherOwner)
    {
        return otherOwner != 0 && me > otherOwner;
    }

    /// <summary>
    /// When an open lobby starts by itself: AUTO_START after its count began (AutoStartClock: a machine's first seat in
    /// the session, or the count beginning)
    /// </summary>
    public static double AutoStartAt(double countFrom)
    {
        return countFrom + AutoStartSeconds;
    }

    /// <summary>
    /// Seconds until an open lobby starts, 0 to AUTO_START; 0 when no start is set or the time is nonsense
    /// </summary>
    public static float AutoStartRemaining(double at, double now)
    {
        if (at == 0 || double.IsNaN(at) || double.IsInfinity(at))
            return 0;

        double left = at - now;
        if (double.IsNaN(left) || left < 0)
            return 0;

        double most = AutoStartSeconds;
        return (float)(left > most ? most : left);
    }

    /// <summary>
    /// The lobby's status line: searching, open to anyone (with the seconds to an automatic start), or friends only
    /// </summary>
    public static string StatusLine(bool filling, bool searching, float autoStartRemaining)
    {
        if (searching)
            return SEARCHING;
        if (!filling)
            return FRIENDS_ONLY;
        if (autoStartRemaining > 0)
            return OPEN_TO_ANYONE + " · STARTS IN " + ((int)System.Math.Ceiling(autoStartRemaining)).ToString(CultureInfo.InvariantCulture);
        return OPEN_TO_ANYONE;
    }

    /// <summary>
    /// The host's Fill prompt (right shoulder): what pressing it does, so the lobby's state flipped
    /// </summary>
    public static string FillPrompt(bool filling)
    {
        return filling ? FRIENDS_ONLY : OPEN_TO_ANYONE;
    }

    /// <summary>
    /// Whether a search may run now: the first one always, then one every SEARCH_GAP seconds
    /// </summary>
    public static bool SearchAllowed(double lastSearchAt, double now)
    {
        return double.IsNegativeInfinity(lastSearchAt) || now - lastSearchAt >= SEARCH_GAP;
    }
}
