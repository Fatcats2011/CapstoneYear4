using System;
using System.Collections.Generic;

/// <summary>
/// Find (a lone host in Dispatch looking for a match):
/// - searches Steam's public lobbies of this build with a free seat (one search at a time, SEARCH_GAP apart), and moves
///   into the fullest (OnlineLobby.Switch). A lobby that fails is passed for the next: a host with no free seat left
///   doesn't count against the MAX_TRIES, anything else does;
/// - with none left, the player's lobby opens to anyone (Fill), hosted again if it was left on the way;
/// - a friend joining while it searches cancels it, and so does the player (B) or leaving the menus, at any step;
/// - a search Steam doesn't answer within SEARCH_TIMEOUT counts as none found;
/// - a lone Fill host searches again every RESEARCH seconds, and moves only to a lobby whose owner has the lower Steam
///   ID, so two such hosts never both leave.
/// Plain C# over ILobbyService and OnlineLobby, so it's tested without Steam. OnlinePlay runs it. See docs/online.md
/// </summary>
public class Matchmaker
{
    readonly ILobbyService lobbies;
    readonly OnlineLobby lobby;
    readonly string build;
    readonly Func<double> clock;
    readonly Func<int> otherMachines;
    readonly Func<bool> inMenus;

    double lastSearchAt = double.NegativeInfinity;
    double fillSince = double.NegativeInfinity;   // when Fill last turned on
    bool awaitingFound;   // the search is out, its answer not in yet
    bool quiet;   // a Fill host's own search again: nobody is told when nothing is found
    List<MatchRules.Candidate> candidates = new List<MatchRules.Candidate>();
    int next;   // the next candidate to try
    int tries;   // the candidates that failed, a host with no free seat not counted
    ulong trying;   // the lobby being switched to (0 = none)
    bool tryNext;   // a switch failed: the next candidate is tried on the next Tick, once the failed session has stopped
    double blockedSince = double.NaN;   // when the next candidate couldn't be tried yet (NaN = it could)

    /// <summary>Whether a search or a move into a found lobby is under way</summary>
    public bool Searching { get; private set; }

    /// <summary>A message for the player: nothing found, or Find refused</summary>
    public event Action<string> Notice;

    /// <param name="build">This build's tag (LobbyRules.BuildTag): only lobbies with it are joined</param>
    /// <param name="clock">Seconds (OnlinePlay passes realtime since startup); tests pass a fake</param>
    /// <param name="otherMachines">How many machines besides the host's are in the hosted session</param>
    /// <param name="inMenus">Whether the game is in its menus (LobbyRules.InTheMenus): Find runs only there</param>
    public Matchmaker(ILobbyService lobbies, OnlineLobby lobby, string build, Func<double> clock, Func<int> otherMachines,
        Func<bool> inMenus)
    {
        this.lobbies = lobbies;
        this.lobby = lobby;
        this.build = build;
        this.clock = clock;
        this.otherMachines = otherMachines;
        this.inMenus = inMenus;

        lobbies.Found += OnFound;
        lobby.SwitchFailed += OnSwitchFailed;
        lobby.Switched += OnSwitched;
        lobby.FillChanged += OnFillChanged;
    }

    /// <summary>
    /// Stops listening to the lobby service and the lobby flow (OnlinePlay replaced them)
    /// </summary>
    public void Detach()
    {
        lobbies.Found -= OnFound;
        lobby.SwitchFailed -= OnSwitchFailed;
        lobby.Switched -= OnSwitched;
        lobby.FillChanged -= OnFillChanged;
    }

    /// <summary>
    /// Looks for a match to move into. False (and nothing happens) while searching, outside the menus, for a player not
    /// hosting, within SEARCH_GAP of the last search, or for a host with others in its lobby (who is told so)
    /// </summary>
    public bool Find()
    {
        if (Searching || !inMenus() || !lobby.IsHost || lobby.Current == 0)
            return false;

        if (otherMachines() > 0)
        {
            Tell(MatchRules.FIND_REFUSED);
            return false;
        }

        if (!MatchRules.SearchAllowed(lastSearchAt, clock()))
            return false;

        Search(false);
        return true;
    }

    /// <summary>
    /// Stops a search at any step, quietly (B, a friend joining, the player leaving): a lobby being entered is left when
    /// it answers, and once the player's own lobby was left, the new lobby and its host's session are left too
    /// </summary>
    public void Cancel()
    {
        if (!Searching)
            return;

        Stop();
        lobby.CancelSwitch();
    }

    /// <summary>
    /// Every frame: leaving the menus or a friend joining cancels the search, a search Steam doesn't answer times out, a
    /// failed move tries the next lobby, and a lone Fill host searches again every RESEARCH seconds
    /// </summary>
    public void Tick()
    {
        if (Searching && (!inMenus() || (lobby.IsHost && otherMachines() > 0)))
        {
            Cancel();
            return;
        }

        double now = clock();
        if (awaitingFound && now - lastSearchAt >= MatchRules.SEARCH_TIMEOUT)
        {
            awaitingFound = false;
            NothingFound();
            return;
        }

        if (tryNext)
        {
            tryNext = false;
            TryNext();
            return;
        }

        if (!Searching && inMenus() && lobby.IsHost && lobby.Current != 0 && lobby.Filling && otherMachines() == 0
            && now - Math.Max(lastSearchAt, fillSince) >= MatchRules.RESEARCH)
            Search(true);
    }

    void Search(bool again)
    {
        Searching = true;
        quiet = again;
        awaitingFound = true;
        lastSearchAt = clock();
        lobbies.Search(build, 1);
    }

    void OnFound(ulong[] found)
    {
        // A search cancelled, or one that isn't this one's
        if (!awaitingFound)
            return;
        awaitingFound = false;
        if (!inMenus())
        {
            Cancel();
            return;
        }

        List<(ulong lobby, ulong owner, string game, string build, string mm, string open)> tagged =
            new List<(ulong lobby, ulong owner, string game, string build, string mm, string open)>();
        if (found != null)
        {
            // Only the first MAX_RESULTS are read, whatever came (SteamLobbyService caps Steam's answer too)
            for (int i = 0; i < found.Length && i < MatchRules.MAX_RESULTS; i++)
            {
                ulong id = found[i];
                tagged.Add((id, lobbies.Owner(id), lobbies.GetData(id, LobbyRules.GAME_KEY),
                    lobbies.GetData(id, LobbyRules.BUILD_KEY), lobbies.GetData(id, MatchRules.MM_KEY),
                    lobbies.GetData(id, MatchRules.OPEN_KEY)));
            }
        }

        candidates = MatchRules.Candidates(tagged, build, lobby.Current, 1);
        // A Fill host's search again: only the higher Steam ID moves
        if (quiet)
            candidates.RemoveAll(c => !MatchRules.MayYieldTo(lobbies.Me, c.Owner));
        next = 0;
        tries = 0;
        blockedSince = double.NaN;
        TryNext();
    }

    void TryNext()
    {
        if (next >= candidates.Count || tries >= MatchRules.MAX_TRIES)
        {
            NothingFound();
            return;
        }

        trying = candidates[next].Lobby;
        if (lobby.Switch(trying))
        {
            next++;
            blockedSince = double.NaN;
            return;
        }
        trying = 0;

        // The last try's session is still stopping: the same lobby next Tick, for at most SEARCH_TIMEOUT
        if (!lobby.IsHost && lobby.Current == 0)
        {
            double now = clock();
            if (double.IsNaN(blockedSince))
                blockedSince = now;
            if (now - blockedSince < MatchRules.SEARCH_TIMEOUT)
            {
                tryNext = true;
                return;
            }
            NothingFound();
            return;
        }

        // The player moved on (into another lobby), or friends joined: nobody is dragged out
        Stop();
    }

    // No lobby worked out: the player's lobby opens to anyone, hosted again if the last try left it
    void NothingFound()
    {
        bool tell = !quiet;
        Stop();
        if (tell)
            Tell(MatchRules.NONE_FOUND);

        if (lobby.IsHost)
            lobby.SetFill(true);
        else if (lobby.Current == 0)
            lobby.Host(invite: false, fill: true);
    }

    void OnSwitchFailed(ulong failed, OnlineLobby.SwitchFailure why)
    {
        if (!Searching || failed != trying)
            return;

        trying = 0;
        if (why != OnlineLobby.SwitchFailure.NoSeat)
            tries++;

        // Not now: a failed session may still be stopping, and a new one can't start until it has
        tryNext = true;
    }

    void OnSwitched(ulong switched)
    {
        if (Searching && switched == trying)
            Stop();
    }

    void OnFillChanged(bool on)
    {
        if (on)
            fillSince = clock();
    }

    void Stop()
    {
        Searching = false;
        awaitingFound = false;
        tryNext = false;
        trying = 0;
        blockedSince = double.NaN;
        candidates.Clear();
    }

    void Tell(string message)
    {
        Notice?.Invoke(message);
    }
}
