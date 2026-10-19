using System;
using System.Collections.Generic;

/// <summary>
/// Find (a lone host in Dispatch looking for a match):
/// - searches Steam's public lobbies of this build with a free seat (one search at a time, SEARCH_GAP apart), and moves
///   into the fullest (OnlineLobby.Switch). A lobby that fails is passed for the next: a host with no free seat left
///   doesn't count against the MAX_TRIES, anything else does, and no more than MAX_ATTEMPTS are moved into in all;
/// - with none left, the player's lobby opens to anyone (Fill), hosted again if it was left on the way, and the player
///   hears whether it did;
/// - a friend joining while it searches cancels it, and so does the player (B, Fill turned off, a friend's invite
///   answered) or leaving the menus, at any step;
/// - a search Steam doesn't answer within SEARCH_TIMEOUT counts as none found, and a found lobby Steam doesn't answer
///   within it as one that couldn't be entered;
/// - a lone Fill host searches again every RESEARCH seconds, quietly (nothing shown, its own lobby left as it is), and
///   moves only to a lobby whose owner has the lower Steam ID, so two such hosts never both leave.
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
    bool running;   // a search (the player's or a quiet one) or a move into a found lobby is under way
    bool awaitingFound;   // the search is out, its answer not in yet
    bool quiet;   // a Fill host's own search again: nothing is shown for it, and its own lobby is left as it is
    List<MatchRules.Candidate> candidates = new List<MatchRules.Candidate>();
    int next;   // the next candidate to try
    int tries;   // the candidates that failed, a host with no free seat not counted
    int attempts;   // the candidates moved into, whatever came of each
    ulong trying;   // the lobby being switched to (0 = none)
    double triedAt;   // when the switch to it began
    bool tryNext;   // a switch failed: the next candidate is tried on the next Tick, once the failed session has stopped
    double blockedSince = double.NaN;   // when the next candidate couldn't be tried yet (NaN = it could)

    /// <summary>
    /// Whether the player's Find is under way: searching, or moving into a found lobby. The status line shows it and B
    /// cancels it. A lone Fill host's quiet search again isn't this
    /// </summary>
    public bool Searching { get { return running && !quiet; } }

    /// <summary>Whether a lone Fill host's quiet search again (or its move into a found lobby) is under way</summary>
    internal bool ResearchingQuietly { get { return running && quiet; } }

    /// <summary>
    /// Whether a search, the player's or a quiet one, has this machine between sessions: its own lobby and session left
    /// for a found lobby's host, who hasn't let it in yet (or between two tries). The machine is offline meanwhile, so
    /// Dispatch stays up and its buttons wait (B cancels)
    /// </summary>
    public bool Switching { get { return running && !lobby.IsHost; } }

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
        lobby.Answering += Cancel;
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
        lobby.Answering -= Cancel;
    }

    /// <summary>
    /// Looks for a match to move into. False (and nothing happens) while a search runs (a quiet one too), outside the
    /// menus, for a player not hosting, within SEARCH_GAP of the last search, or for a host with others in its lobby (who
    /// is told so)
    /// </summary>
    public bool Find()
    {
        if (running || !inMenus() || !lobby.IsHost || lobby.Current == 0)
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
    /// Stops a search at any step, the player's or a quiet one, quietly (B, a friend joining or inviting, Fill turned
    /// off, the player leaving): a lobby being entered is left when it answers, and once the player's own lobby was left,
    /// the new lobby and its host's session are left too
    /// </summary>
    public void Cancel()
    {
        if (!running)
            return;

        Stop();
        lobby.CancelSwitch();
    }

    /// <summary>
    /// Every frame: leaving the menus or a friend joining cancels the search, a search or a found lobby Steam doesn't
    /// answer times out, a failed move tries the next lobby, and a lone Fill host searches again every RESEARCH seconds
    /// </summary>
    public void Tick()
    {
        if (running && (!inMenus() || (lobby.IsHost && otherMachines() > 0)))
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

        // A found lobby Steam never let this machine into: a failed try (OnSwitchFailed), and its late answer is left
        if (trying != 0 && now - triedAt >= MatchRules.SEARCH_TIMEOUT && lobby.GiveUpEntering(trying))
            return;

        if (tryNext)
        {
            tryNext = false;
            TryNext();
            return;
        }

        if (!running && inMenus() && lobby.IsHost && lobby.Current != 0 && lobby.Filling && otherMachines() == 0
            && now - Math.Max(lastSearchAt, fillSince) >= MatchRules.RESEARCH)
            Search(true);
    }

    void Search(bool again)
    {
        running = true;
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
        attempts = 0;
        blockedSince = double.NaN;
        TryNext();
    }

    void TryNext()
    {
        // Hosts with no free seat don't count as tries, but they can't keep a search going for ever either
        if (next >= candidates.Count || tries >= MatchRules.MAX_TRIES || attempts >= MatchRules.MAX_ATTEMPTS)
        {
            NothingFound();
            return;
        }

        trying = candidates[next].Lobby;
        if (lobby.Switch(trying))
        {
            next++;
            attempts++;
            triedAt = clock();
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

    // No lobby worked out. The player's Find opens their lobby to anyone, and they hear whether it did. A quiet search
    // leaves the lobby as it is (still open: turning Fill off stops the search) and says nothing. Either one hosts again,
    // open to anyone, when the last try left the lobby: the player chose that, by Find or by Fill
    void NothingFound()
    {
        bool find = !quiet;
        Stop();

        bool open;
        if (lobby.IsHost)
        {
            if (find)
                lobby.SetFill(true);
            open = lobby.Filling;
        }
        else if (lobby.Current == 0)
            open = lobby.Host(invite: false, fill: true);
        else
            open = false;

        if (find)
            Tell(open ? MatchRules.NONE_FOUND : MatchRules.NONE_FOUND_CLOSED);
    }

    void OnSwitchFailed(ulong failed, OnlineLobby.SwitchFailure why)
    {
        if (!running || failed != trying)
            return;

        trying = 0;
        if (why != OnlineLobby.SwitchFailure.NoSeat)
            tries++;

        // Not now: a failed session may still be stopping, and a new one can't start until it has
        tryNext = true;
    }

    void OnSwitched(ulong switched)
    {
        if (running && switched == trying)
            Stop();
    }

    // Fill on: a lone host's quiet search waits RESEARCH from now. The host turning it off stops any search, so its
    // fallback can't open the lobby again and it can't move them into a stranger's. A lobby left by a switch also reads
    // as Fill off, once it's gone: that isn't the host's choice, and the switch goes on
    void OnFillChanged(bool on)
    {
        if (on)
            fillSince = clock();
        else if (lobby.IsHost)
            Cancel();
    }

    void Stop()
    {
        running = false;
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
