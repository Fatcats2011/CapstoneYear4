using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class PlayerRosterTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        PlayerInput NewController(string name)
        {
            return objects.NewGameObject(name).AddComponent<PlayerInput>();
        }

        [Test]
        public void JoinLocal_FirstPlayer_TakesSlotZeroAsALocalPlayer()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");

            PlayerSlot slot = roster.JoinLocal(first);

            Assert.AreEqual(0, slot.Index);
            Assert.AreSame(first, slot.Input);
            Assert.AreSame(first.gameObject, slot.Player);
            Assert.IsTrue(slot.IsLocal);
            Assert.AreEqual(0UL, slot.OwnerClientId);
            Assert.AreSame(slot, roster[0]);
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void JoinLocal_AfterSomeoneLeft_FillsTheLowestFreeSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);
            roster.JoinLocal(NewController("P3"));
            roster.Leave(second);

            PlayerSlot newcomer = roster.JoinLocal(NewController("Newcomer"));

            Assert.AreEqual(1, newcomer.Index);
            Assert.AreEqual(3, roster.Count);
        }

        [Test]
        public void JoinLocal_WhenAllFourSlotsAreTaken_ReturnsNull()
        {
            PlayerRoster roster = new PlayerRoster();
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
                roster.JoinLocal(NewController("P" + (i + 1)));

            Assert.IsNull(roster.JoinLocal(NewController("Fifth")));
            Assert.AreEqual(Constants.MAX_PLAYERS, roster.Count);
        }

        [Test]
        public void JoinLocal_SameControllerTwice_KeepsOneSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");
            roster.JoinLocal(first);

            Assert.IsNull(roster.JoinLocal(first));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_FreesOnlyThatPlayersSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            PlayerInput first = NewController("P1");
            PlayerInput second = NewController("P2");
            roster.JoinLocal(first);
            roster.JoinLocal(second);

            Assert.AreEqual(1, roster.Leave(second));
            Assert.AreSame(first, roster[0].Input);
            Assert.IsNull(roster[1]);
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_PlayerNotInTheRoster_ReturnsMinusOne()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));

            Assert.AreEqual(-1, roster.Leave(NewController("Stranger")));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Leave_NoController_NeverRemovesAnOnlinePlayer()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinRemote(objects.NewGameObject("Online player"), 7);

            Assert.AreEqual(-1, roster.Leave(null));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void Players_ListsTakenSlotsInSlotOrder()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);
            roster.JoinLocal(NewController("P3"));
            roster.Leave(second);

            CollectionAssert.AreEqual(new[] { 0, 2 }, roster.Players.Select(p => p.Index).ToArray());
        }

        [Test]
        public void JoinRemote_OnlinePlayer_HasNoControllerAndIsLeftOutOfLocalPlayers()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            GameObject online = objects.NewGameObject("Online player");

            PlayerSlot slot = roster.JoinRemote(online, 7);

            Assert.AreEqual(1, slot.Index);
            Assert.IsFalse(slot.IsLocal);
            Assert.IsNull(slot.Input);
            Assert.AreSame(online, slot.Player);
            Assert.AreEqual(7UL, slot.OwnerClientId);
            Assert.AreEqual(2, roster.Count);
            Assert.AreEqual(1, roster.LocalCount);
            CollectionAssert.AreEqual(new[] { 0 }, roster.LocalPlayers.Select(p => p.Index).ToArray());
        }

        [Test]
        public void JoinRemote_SamePlayerTwice_KeepsOneSlot()
        {
            PlayerRoster roster = new PlayerRoster();
            GameObject online = objects.NewGameObject("Online player");
            roster.JoinRemote(online, 7);

            Assert.IsNull(roster.JoinRemote(online, 7));
            Assert.AreEqual(1, roster.Count);
        }

        [Test]
        public void IndexOf_FindsLocalPlayersBySlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            PlayerInput second = NewController("P2");
            roster.JoinLocal(second);

            Assert.AreEqual(1, roster.IndexOf(second));
            Assert.AreEqual(-1, roster.IndexOf(NewController("Stranger")));
        }

        [Test]
        public void Clear_FreesEverySlot()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(NewController("P1"));
            roster.JoinRemote(objects.NewGameObject("Online player"), 7);

            roster.Clear();

            Assert.AreEqual(0, roster.Count);
            Assert.IsEmpty(roster.Players);
            Assert.AreEqual(0, roster.JoinLocal(NewController("Next")).Index);
        }
    }
}
