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
        List<string> failures;

        [SetUp]
        public void SetUp()
        {
            lobbies = new FakeLobbyService();
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, "1.2");
            notices = new List<string>();
            lobby.Notice += Noticed;
            failures = new List<string>();
            lobby.HostFailed += Failed;
            lobby.ShowState(GameState.PlayerSelect);
        }

        void Noticed(string notice)
        {
            notices.Add(notice);
        }

        void Failed(string reason)
        {
            failures.Add(reason);
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
        public void Hosting_ASteamLobby_LetsOnlyItsMembersConnect()
        {
            lobbies.Members[42] = new List<ulong> { 7, 8 };
            Assert.IsFalse(lobby.LetsIn(7), "no lobby yet");

            Hosting();

            Assert.IsTrue(lobby.LetsIn(8), "a member");
            Assert.IsFalse(lobby.LetsIn(9), "a stranger");

            session.Stop(null);
            Assert.IsFalse(lobby.LetsIn(8), "the lobby is gone");
        }

        [Test]
        public void LetsIn_RepeatedReconnects_Refused()
        {
            double now = 0;
            lobby = new OnlineLobby(lobbies, session, "1.2", () => now);
            lobby.ShowState(GameState.PlayerSelect);
            lobbies.Members[42] = new List<ulong> { 8 };
            Hosting();

            for (int i = 0; i < JoinChurn.CHURN_LIMIT; i++)
            {
                Assert.IsTrue(lobby.LetsIn(8), "connection " + (i + 1));
                now += 5;
            }

            Assert.IsFalse(lobby.LetsIn(8), "the sixth within a minute");
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
        public void HostFailed_WithoutSteam_GivesTheReason()
        {
            lobbies.Available = false;

            lobby.Host();

            CollectionAssert.AreEqual(new[] { OnlineLobby.NO_STEAM }, failures);
        }

        [Test]
        public void HostFailed_LobbyCantBeMade_GivesTheReason()
        {
            lobby.Host();
            lobbies.RaiseCreated(0);

            CollectionAssert.AreEqual(new[] { OnlineLobby.LOBBY_FAILED }, failures);
        }

        [Test]
        public void HostFailed_SessionWontStart_GivesTheReason()
        {
            session.HostWorks = false;

            Hosting();

            CollectionAssert.AreEqual(new[] { OnlineLobby.HOST_FAILED }, failures);
        }

        [Test]
        public void HostFailed_NotRaised_OnSuccess_OrWhenThePlayerMovedOn()
        {
            Hosting();
            // A session ending isn't a host failure
            session.Stop(null);

            // B before the next lobby is made
            lobby.Host();
            lobby.ShowState(GameState.Menu);
            lobbies.RaiseCreated(43);

            CollectionAssert.IsEmpty(failures);
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
        public void Answer_TheSameLobbyTwice_JoinsOnce()
        {
            lobby.Answer(7, null);
            lobby.Answer(7, null); // a double "Join Game" before Steam answers

            CollectionAssert.AreEqual(new[] { 7UL }, lobbies.Joins, "entered once: a second answer would leave it");
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

        [Test]
        public void NewLobby_NeverPublic()
        {
            Hosting();

            Assert.IsFalse(lobbies.PublicCalls.Exists(c => c.open));
            Assert.IsFalse(lobby.Filling);
        }

        [Test]
        public void Fill_On_PublicAndTagged()
        {
            Hosting();
            bool raised = false;
            lobby.FillChanged += on => raised = on;

            lobby.SetFill(true);

            Assert.IsTrue(lobby.Filling);
            Assert.IsTrue(raised);
            CollectionAssert.Contains(lobbies.PublicCalls, (42UL, true));
            Assert.AreEqual(MatchRules.MM_ON, lobbies.GetData(42, MatchRules.MM_KEY));
            Assert.AreEqual("3", lobbies.GetData(42, MatchRules.OPEN_KEY), "4 seats minus the host");
        }

        [Test]
        public void Fill_Off_FriendsOnlyAndUntagged()
        {
            Hosting();
            lobby.SetFill(true);

            lobby.SetFill(false);

            Assert.IsFalse(lobby.Filling);
            Assert.AreEqual((42UL, false), lobbies.PublicCalls[lobbies.PublicCalls.Count - 1]);
            Assert.AreEqual("", lobbies.GetData(42, MatchRules.MM_KEY));
        }

        [Test]
        public void Fill_OnClient_DoesNothing()
        {
            Joined(7, 99);

            lobby.SetFill(true);

            Assert.IsFalse(lobby.Filling);
            Assert.IsEmpty(lobbies.PublicCalls);
            Assert.AreEqual("", lobbies.GetData(7, MatchRules.MM_KEY));
        }

        [Test]
        public void Fill_ResetOnLeave_NewLobbyFriendsOnly()
        {
            Hosting();
            lobby.SetFill(true);

            session.Stop(null);
            Assert.IsFalse(lobby.Filling);
            int publicCalls = lobbies.PublicCalls.Count;
            Hosting43();

            Assert.IsFalse(lobby.Filling);
            Assert.AreEqual(publicCalls, lobbies.PublicCalls.Count, "no public call for the new lobby");
        }

        // M-5 (spec Safety 3): Steam may hand a left lobby to a member who stays, so it goes back to friends only first
        [Test]
        public void Leave_WhileFilling_FriendsOnlyBeforeLeaving_AndFillChangedOff()
        {
            Hosting();
            lobby.SetFill(true);
            List<bool> changes = new List<bool>();
            lobby.FillChanged += changes.Add;

            session.Stop(null);

            Assert.AreEqual((42UL, false), lobbies.PublicCalls[lobbies.PublicCalls.Count - 1], "friends only");
            Assert.AreEqual("", lobbies.GetData(42, MatchRules.MM_KEY), "untagged");
            CollectionAssert.AreEqual(new[] { 42UL }, lobbies.Left);
            CollectionAssert.AreEqual(new[] { false }, changes, "Fill off is told");
            Assert.IsFalse(lobby.Filling);
        }

        [Test]
        public void Leave_NeverFilled_NoPublicCallAndNoFillChanged()
        {
            Hosting();
            List<bool> changes = new List<bool>();
            lobby.FillChanged += changes.Add;

            session.Stop(null);

            Assert.IsEmpty(lobbies.PublicCalls);
            Assert.IsEmpty(changes);
        }

        // M-4: an invite answered during a switch between sessions drops the switch first
        [Test]
        public void Answer_DuringASwitch_DropsIt_ThenJoins()
        {
            Hosting();
            lobbies.AddLobby(77, "doa", "1.2", 177);
            lobbies.AddLobby(7, "doa", "1.2", 99);
            Assert.IsTrue(lobby.Switch(77));
            lobbies.RaiseEntered(77, true);
            Assert.AreEqual(77UL, lobby.Current, "switching");
            bool answering = false;
            lobby.Answering += () => answering = true;

            lobby.Answer(7, null);

            Assert.IsTrue(answering, "Find hears it");
            CollectionAssert.Contains(lobbies.Left, 77UL, "the switch's lobby is left");
            Assert.AreEqual(2, session.Leaves, "the old session, then the one joining the switch's host");
            lobbies.RaiseEntered(7, true);
            Assert.AreEqual(7UL, lobby.Current);
            CollectionAssert.AreEqual(new[] { 177UL, 99UL }, session.JoinedHosts);
        }

        [Test]
        public void Host_SaysWhetherALobbyIsBeingMade()
        {
            Assert.IsTrue(lobby.Host(), "being made");
            Assert.IsFalse(lobby.Host(), "one is being made already");
            lobbies.RaiseCreated(42);
            Assert.IsFalse(lobby.Host(), "in a lobby");

            session.Stop(null);
            lobbies.Available = false;
            Assert.IsFalse(lobby.Host(), "no Steam");
        }

        void Hosting43()
        {
            lobby.Host();
            lobbies.RaiseCreated(43);
        }

        [Test]
        public void ShowOpenSeats_OnlyOnChange()
        {
            Hosting();

            lobby.ShowOpenSeats(3);
            lobby.ShowOpenSeats(3);
            lobby.ShowOpenSeats(2);

            Assert.AreEqual(2, lobbies.DataCalls.FindAll(c => c.key == MatchRules.OPEN_KEY).Count);
            Assert.AreEqual("2", lobbies.GetData(42, MatchRules.OPEN_KEY));
        }

        [Test]
        public void ShowOpenSeats_ClampedAndUsedWhenFillStarts()
        {
            Hosting();

            lobby.ShowOpenSeats(9);
            Assert.AreEqual("4", lobbies.GetData(42, MatchRules.OPEN_KEY));

            lobby.ShowOpenSeats(1);
            lobby.SetFill(true);
            Assert.AreEqual("1", lobbies.GetData(42, MatchRules.OPEN_KEY));
        }

        [Test]
        public void Host_FillTrue_NoInvite_PublicWhenMade()
        {
            lobby.Host(invite: false, fill: true);
            Assert.IsEmpty(lobbies.PublicCalls, "not before the lobby is made");

            lobbies.RaiseCreated(42);

            Assert.IsTrue(lobby.Filling);
            CollectionAssert.Contains(lobbies.PublicCalls, (42UL, true));
            Assert.IsEmpty(lobbies.Invites);
        }
    }
}
