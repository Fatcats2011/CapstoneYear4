using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// The lobby flow (OnlineLobby) over a fake Steam and a fake session. The player starts in player select
    /// </summary>
    public class OnlineLobbyTests
    {
        FakeLobbyService lobbies;
        FakeSessionControl session;
        OnlineLobby lobby;
        List<string> notices;

        [SetUp]
        public void SetUp()
        {
            lobbies = new FakeLobbyService();
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, "1.2");
            notices = new List<string>();
            lobby.Notice += Noticed;
            lobby.ShowState(GameState.PlayerSelect);
        }

        void Noticed(string notice)
        {
            notices.Add(notice);
        }

        // Y in player select, and Steam makes lobby 42
        void Hosting()
        {
            lobby.Host();
            lobbies.RaiseCreated(42);
        }

        // A friend's invite to their lobby, which this machine enters
        void Joined(ulong friendsLobby, ulong owner)
        {
            lobbies.AddLobby(friendsLobby, "doa", "1.2", owner);
            lobby.Answer(friendsLobby, null);
            lobbies.RaiseEntered(friendsLobby, true);
        }

        [Test]
        public void Host_MakesAFriendsOnlyLobbyForFour_TagsItWithTheGameAndBuild_AndHostsInIt()
        {
            Hosting();

            CollectionAssert.AreEqual(new[] { 4 }, lobbies.Creates);
            Assert.AreEqual("doa", lobbies.Data[42]["game"]);
            Assert.AreEqual("1.2", lobbies.Data[42]["build"]);
            Assert.AreEqual(1, session.Hosts);
            Assert.AreEqual(42UL, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Invites, "the invite dialog opens");
        }

        [Test]
        public void Host_WithoutSteam_SaysSo_AndMakesNoLobby()
        {
            lobbies.Available = false;

            lobby.Host();

            CollectionAssert.AreEqual(new[] { OnlineLobby.NO_STEAM }, notices);
            Assert.IsEmpty(lobbies.Creates);
        }

        [Test]
        public void Host_LobbyCantBeMade_SaysSo_AndDoesntHost()
        {
            lobby.Host();
            lobbies.RaiseCreated(0);

            CollectionAssert.AreEqual(new[] { OnlineLobby.LOBBY_FAILED }, notices);
            Assert.AreEqual(0, session.Hosts);
            Assert.AreEqual(0UL, lobby.Current);
        }

        [Test]
        public void Host_SessionWontStart_LeavesTheLobby_AndSaysSo()
        {
            session.HostWorks = false;

            Hosting();

            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Left);
            CollectionAssert.AreEqual(new[] { OnlineLobby.HOST_FAILED }, notices);
            Assert.AreEqual(0UL, lobby.Current);
        }

        [Test]
        public void Host_PressedAgainWhileTheLobbyIsMade_MakesOnlyOne()
        {
            lobby.Host();
            lobby.Host();

            Assert.AreEqual(1, lobbies.Creates.Count);
        }

        [Test]
        public void Host_PlayerSelectClosesBeforeTheLobbyIsMade_LeavesIt_AndDoesntHost()
        {
            lobby.Host();
            lobby.ShowState(GameState.Menu);
            lobbies.RaiseCreated(42);

            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Left);
            Assert.AreEqual(0, session.Hosts);
            Assert.AreEqual(0UL, lobby.Current);
        }

        [Test]
        public void Invite_WithoutSteamsOverlay_PointsToTheFriendsList()
        {
            lobbies.InviteWorks = false;

            Hosting();

            CollectionAssert.AreEqual(new[] { OnlineLobby.INVITE_FROM_STEAM }, notices);
        }

        [Test]
        public void Answer_JoinsTheLobby_ThenItsOwnersSession()
        {
            lobbies.AddLobby(7, "doa", "1.2", 99);

            lobby.Answer(7, null);
            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Joins);

            lobbies.RaiseEntered(7, true);
            CollectionAssert.AreEqual(new[] { 99UL }, session.JoinedHosts);
            Assert.AreEqual(7UL, lobby.Current);
            Assert.IsFalse(lobby.IsHost);
        }

        [Test]
        public void Answer_WhileBusy_SaysWhy_AndJoinsNothing()
        {
            lobby.Answer(7, LobbyRules.BUSY);

            CollectionAssert.AreEqual(new[] { LobbyRules.BUSY }, notices);
            Assert.IsEmpty(lobbies.Joins);
        }

        [Test]
        public void Answer_ALobbyOfAnotherBuild_LeavesIt_AndSaysWhy()
        {
            lobbies.AddLobby(7, "doa", "1.1", 99);

            lobby.Answer(7, null);
            lobbies.RaiseEntered(7, true);

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            Assert.IsEmpty(session.JoinedHosts);
            CollectionAssert.AreEqual(new[] { LobbyRules.Refusal("doa", "1.1", "1.2") }, notices);
            Assert.AreEqual(0UL, lobby.Current);
        }

        [Test]
        public void Answer_AnotherGamesLobby_LeavesIt()
        {
            lobbies.AddLobby(7, "spacewar", "1.2", 99);

            lobby.Answer(7, null);
            lobbies.RaiseEntered(7, true);

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            CollectionAssert.AreEqual(new[] { LobbyRules.NOT_THIS_GAME }, notices);
        }

        [Test]
        public void Answer_LobbyCantBeEntered_SaysSo()
        {
            lobby.Answer(7, null);
            lobbies.RaiseEntered(7, false);

            CollectionAssert.AreEqual(new[] { OnlineLobby.JOIN_FAILED }, notices);
            Assert.AreEqual(0UL, lobby.Current);
            Assert.IsEmpty(lobbies.Left);
        }

        [Test]
        public void Answer_TwoRequests_FollowsTheLatest_AndLeavesTheFirst()
        {
            lobbies.AddLobby(7, "doa", "1.2", 99);
            lobbies.AddLobby(8, "doa", "1.2", 98);

            lobby.Answer(7, null);
            lobby.Answer(8, null);
            lobbies.RaiseEntered(7, true);
            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            Assert.IsEmpty(session.JoinedHosts);

            lobbies.RaiseEntered(8, true);
            CollectionAssert.AreEqual(new[] { 98UL }, session.JoinedHosts);
            Assert.AreEqual(8UL, lobby.Current);
        }

        [Test]
        public void Answer_ALocalMatchStartsBeforeTheLobbyAnswers_LeavesIt()
        {
            lobbies.AddLobby(7, "doa", "1.2", 99);

            lobby.Answer(7, null);
            lobby.ShowState(GameState.Loading);
            lobbies.RaiseEntered(7, true);

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            Assert.IsEmpty(session.JoinedHosts);
        }

        [Test]
        public void Answer_SessionWontStart_LeavesTheLobby()
        {
            session.JoinWorks = false;

            Joined(7, 99);

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            CollectionAssert.AreEqual(new[] { OnlineLobby.JOIN_FAILED }, notices);
        }

        [Test]
        public void Answer_TheLobbyThisMachineIsIn_IsIgnored()
        {
            Hosting();

            lobby.Answer(42, null);

            Assert.IsEmpty(lobbies.Joins);
            Assert.IsEmpty(notices);
        }

        [Test]
        public void SessionStops_LeavesTheLobby()
        {
            Hosting();

            session.Stop(null);

            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Left);
            Assert.AreEqual(0UL, lobby.Current);
        }

        [Test]
        public void SessionTurnsTheJoinerAway_LeavesTheLobby()
        {
            Joined(7, 99);

            session.TurnAway(JoinRules.FULL);

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Left);
            Assert.AreEqual(0UL, lobby.Current);
            Assert.IsEmpty(notices, "the online game shows why a session ended");
        }

        [Test]
        public void ShowState_TheMatchStarts_ClosesTheHostsLobby_TheMenusReopenIt()
        {
            Hosting();

            lobby.ShowState(GameState.Loading);
            Assert.IsFalse(lobbies.Joinable[42], "nobody joins a match that has started");

            lobby.ShowState(GameState.PlayerSelect);
            Assert.IsTrue(lobbies.Joinable[42], "friends can join between matches");
        }

        [Test]
        public void ShowState_OnAClient_LeavesJoinabilityAlone()
        {
            Joined(7, 99);

            lobby.ShowState(GameState.Loading);

            Assert.IsFalse(lobbies.Joinable.ContainsKey(7));
        }
    }
}
