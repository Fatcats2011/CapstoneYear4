using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Matchmaking by distance (MatchRules.ByDistance): lobbies further than MAX_PING away are dropped, the rest keep
    /// their order (fullest first). One whose distance can't be told stays. In quiet hours, when every lobby found is
    /// far, the search takes them anyway, nearest first, rather than finding nothing
    /// </summary>
    public class MatchRegionTests
    {
        static readonly Dictionary<ulong, int> pings = new Dictionary<ulong, int>();
        static int PingOf(ulong lobby) { return pings.TryGetValue(lobby, out int ms) ? ms : -1; }

        static List<MatchRules.Candidate> Found(params ulong[] lobbies)
        {
            List<MatchRules.Candidate> list = new List<MatchRules.Candidate>();
            foreach (ulong lobby in lobbies)
                list.Add(new MatchRules.Candidate { Lobby = lobby, OpenSeats = 1 });
            return list;
        }

        static ulong[] Ids(List<MatchRules.Candidate> list)
        {
            ulong[] ids = new ulong[list.Count];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = list[i].Lobby;
            return ids;
        }

        [SetUp]
        public void SetUp()
        {
            pings.Clear();
        }

        [Test]
        public void FarLobbies_AreDropped_TheRestKeepTheirOrder()
        {
            pings[1] = 40;
            pings[2] = MatchRules.MAX_PING + 1;
            pings[3] = 120;

            CollectionAssert.AreEqual(new ulong[] { 1, 3 }, Ids(MatchRules.ByDistance(Found(1, 2, 3), PingOf)));
        }

        [Test]
        public void ALobbyWhoseDistanceCantBeTold_Stays()
        {
            pings[1] = MatchRules.MAX_PING + 50;

            CollectionAssert.AreEqual(new ulong[] { 2 }, Ids(MatchRules.ByDistance(Found(1, 2), PingOf)));
        }

        [Test]
        public void EveryLobbyFar_TakesThemAnyway_NearestFirst()
        {
            pings[1] = 400;
            pings[2] = 250;

            CollectionAssert.AreEqual(new ulong[] { 2, 1 }, Ids(MatchRules.ByDistance(Found(1, 2), PingOf)));
        }

        [Test]
        public void NoneFound_None()
        {
            Assert.IsEmpty(MatchRules.ByDistance(new List<MatchRules.Candidate>(), PingOf));
        }

        [Test]
        public void TheLocationTag_IsKeptShort()
        {
            Assert.AreEqual("", MatchRules.LocationTag(null));
            Assert.AreEqual("abc", MatchRules.LocationTag("abc"));
            Assert.AreEqual(MatchRules.MAX_LOCATION, MatchRules.LocationTag(new string('x', 5000)).Length, "Steam caps lobby data");
        }
    }
}
