using System;
using System.Collections.Generic;

namespace DoA.Tests
{
    /// <summary>
    /// Steam's lobbies without Steam: records what the lobby flow asks for, and answers when a test says so (or at once,
    /// with AutoAnswer)
    /// </summary>
    public class FakeLobbyService : ILobbyService
    {
        /// <summary>This PC's ping location, and each location's round trip (none: can't tell)</summary>
        public string MyLocation { get; set; } = "";
        public readonly System.Collections.Generic.Dictionary<string, int> Pings = new System.Collections.Generic.Dictionary<string, int>();
        public int PingTo(string location) { return location != null && Pings.TryGetValue(location, out int ms) ? ms : -1; }

        public readonly List<int> Creates = new List<int>();
        public readonly List<ulong> Joins = new List<ulong>();
        public readonly List<ulong> Left = new List<ulong>();
        public readonly List<ulong> Invites = new List<ulong>();
        public readonly List<(ulong lobby, bool open)> PublicCalls = new List<(ulong lobby, bool open)>();
        public readonly List<(ulong lobby, string key, string value)> DataCalls = new List<(ulong lobby, string key, string value)>();
        public readonly List<(string build, int seats)> Searches = new List<(string build, int seats)>();

        public readonly Dictionary<ulong, Dictionary<string, string>> Data = new Dictionary<ulong, Dictionary<string, string>>();
        public readonly Dictionary<ulong, ulong> Owners = new Dictionary<ulong, ulong>();
        public readonly Dictionary<ulong, bool> Joinable = new Dictionary<ulong, bool>();
        /// <summary>Each lobby's members (Steam IDs)</summary>
        public readonly Dictionary<ulong, List<ulong>> Members = new Dictionary<ulong, List<ulong>>();

        public bool Available { get; set; } = true;
        public bool InviteWorks = true;
        public ulong NextLobby = 42;
        /// <summary>Create answers with NextLobby, and Join enters any lobby with data, at once</summary>
        public bool AutoAnswer;

        public event Action<ulong> Created;
        public event Action<ulong, bool> Entered;
        public event Action<ulong> JoinRequested;
        public event Action<int, ulong[]> Found;

        public void Create(int maxMembers)
        {
            Creates.Add(maxMembers);
            if (AutoAnswer)
                RaiseCreated(NextLobby);
        }

        public void Join(ulong lobby)
        {
            Joins.Add(lobby);
            if (AutoAnswer)
                RaiseEntered(lobby, Data.ContainsKey(lobby));
        }

        public void Leave(ulong lobby)
        {
            Left.Add(lobby);
        }

        public void SetData(ulong lobby, string key, string value)
        {
            DataCalls.Add((lobby, key, value));
            Dictionary<string, string> data;
            if (!Data.TryGetValue(lobby, out data))
            {
                data = new Dictionary<string, string>();
                Data[lobby] = data;
            }
            data[key] = value;
        }

        /// <summary>How many lobbies' data or owner were read</summary>
        public int Reads;

        public string GetData(ulong lobby, string key)
        {
            Reads++;
            Dictionary<string, string> data;
            string value;
            return Data.TryGetValue(lobby, out data) && data.TryGetValue(key, out value) ? value : "";
        }

        public ulong Owner(ulong lobby)
        {
            Reads++;
            ulong owner;
            return Owners.TryGetValue(lobby, out owner) ? owner : 0;
        }

        public void SetJoinable(ulong lobby, bool joinable)
        {
            Joinable[lobby] = joinable;
        }

        public bool IsMember(ulong lobby, ulong steamId)
        {
            List<ulong> members;
            return Members.TryGetValue(lobby, out members) && members.Contains(steamId);
        }

        public ulong Me { get; set; }

        public void SetPublic(ulong lobby, bool open)
        {
            PublicCalls.Add((lobby, open));
        }

        /// <summary>Search answers with no lobbies at once, before it returns (as SteamLobbyService does without Steam)</summary>
        public bool AnswerSearchAtOnce;

        /// <summary>Each search's id is its number in Searches, from 1</summary>
        public int Search(string build, int seatsNeeded)
        {
            Searches.Add((build, seatsNeeded));
            if (AnswerSearchAtOnce)
                AnswerSearch();
            return Searches.Count;
        }

        /// <summary>Answers the latest search with these lobbies</summary>
        public void AnswerSearch(params ulong[] lobbies)
        {
            AnswerSearchFor(Searches.Count, lobbies);
        }

        /// <summary>Answers one search (its id), maybe not the latest, with these lobbies</summary>
        public void AnswerSearchFor(int search, params ulong[] lobbies)
        {
            Found?.Invoke(search, lobbies);
        }

        public bool Invite(ulong lobby)
        {
            if (!InviteWorks)
                return false;

            Invites.Add(lobby);
            return true;
        }

        public void RaiseCreated(ulong lobby)
        {
            Created?.Invoke(lobby);
        }

        public void RaiseEntered(ulong lobby, bool entered)
        {
            Entered?.Invoke(lobby, entered);
        }

        public void RaiseJoinRequested(ulong lobby)
        {
            JoinRequested?.Invoke(lobby);
        }

        /// <summary>A friend's lobby, tagged by their game, with its owner</summary>
        public void AddLobby(ulong lobby, string game, string build, ulong owner)
        {
            SetData(lobby, LobbyRules.GAME_KEY, game);
            SetData(lobby, LobbyRules.BUILD_KEY, build);
            Owners[lobby] = owner;
            // A real host publishes its Steam ID in the lobby; a searcher can only read it from there
            SetData(lobby, MatchRules.OWNER_KEY, owner.ToString());
        }
    }

    /// <summary>
    /// The game's session without a network: counts hosts and joins, and stops when a test says so
    /// </summary>
    public class FakeSessionControl : ISessionControl
    {
        public bool HostWorks = true;
        public bool JoinWorks = true;
        public int Hosts;
        public readonly List<ulong> JoinedHosts = new List<ulong>();

        /// <summary>Settable: a test holds a session that is still stopping</summary>
        public bool IsRunning { get; set; }
        public event Action<NetworkRole> RoleChanged;
        public event Action<string> Ended;

        public bool Host()
        {
            Hosts++;
            if (!HostWorks)
                return false;

            IsRunning = true;
            RoleChanged?.Invoke(NetworkRole.Host);
            return true;
        }

        public bool Join(ulong hostId)
        {
            JoinedHosts.Add(hostId);
            IsRunning = JoinWorks;
            return JoinWorks;
        }

        public int Leaves;

        public void Leave()
        {
            Leaves++;
            if (!IsRunning)
                return;

            IsRunning = false;
            RoleChanged?.Invoke(NetworkRole.Offline);
        }

        /// <summary>The host lets this machine in: it's a client</summary>
        public void LetIn()
        {
            RoleChanged?.Invoke(NetworkRole.Client);
        }

        /// <summary>The session stops: this machine is offline again, and says why when there's a reason</summary>
        public void Stop(string reason)
        {
            IsRunning = false;
            RoleChanged?.Invoke(NetworkRole.Offline);
            if (reason != null)
                Ended?.Invoke(reason);
        }

        /// <summary>The host turns this machine away before it was ever let in</summary>
        public void TurnAway(string reason)
        {
            IsRunning = false;
            Ended?.Invoke(reason);
        }
    }
}
