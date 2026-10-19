using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// Find (Matchmaker) and the lobby switch (OnlineLobby.Switch) over a fake Steam, a fake session and a fake clock.
    /// The player hosts lobby 42, alone, in player select
    /// </summary>
    public class MatchmakerTests
    {
        const string BUILD = "1.2";
        const ulong OWN = 42;

        FakeLobbyService lobbies;
        FakeSessionControl session;
        OnlineLobby lobby;
        Matchmaker match;
        double now;
        int others;
        List<string> notices;
        List<(ulong lobby, OnlineLobby.SwitchFailure why)> failures;

        double Clock()
        {
            return now;
        }

        int OtherMachines()
        {
            return others;
        }

        void Noticed(string notice)
        {
            notices.Add(notice);
        }

        void SwitchFailed(ulong failed, OnlineLobby.SwitchFailure why)
        {
            failures.Add((failed, why));
        }

        [SetUp]
        public void SetUp()
        {
            now = 100;
            others = 0;
            lobbies = new FakeLobbyService { Me = 500 };
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, BUILD, Clock, OtherMachines);
            lobby.ShowState(GameState.PlayerSelect);
            match = new Matchmaker(lobbies, lobby, BUILD, Clock, OtherMachines);
            notices = new List<string>();
            match.Notice += Noticed;
            failures = new List<(ulong lobby, OnlineLobby.SwitchFailure why)>();
            lobby.SwitchFailed += SwitchFailed;

            lobby.Host(invite: false);
            lobbies.RaiseCreated(OWN);
            Assert.IsTrue(lobby.IsHost);
        }

        // An open lobby of this game and build, found by a search, with its owner and free seats
        void Open(ulong id, ulong owner, int seats)
        {
            lobbies.AddLobby(id, LobbyRules.GAME, BUILD, owner);
            lobbies.SetData(id, MatchRules.MM_KEY, MatchRules.MM_ON);
            lobbies.SetData(id, MatchRules.OPEN_KEY, seats.ToString());
        }

        [Test]
        public void Find_PicksFullest_AndSwitches()
        {
            Open(70, 170, 3);
            Open(71, 171, 1);
            Open(72, 172, 2);

            Assert.IsTrue(match.Find());
            Assert.IsTrue(match.Searching);
            CollectionAssert.AreEqual(new[] { (BUILD, 1) }, lobbies.Searches, "one search, for one seat");
            lobbies.AnswerSearch(70, 71, 72);
            CollectionAssert.AreEqual(new[] { 71UL }, lobbies.Joins, "the fullest that fits");
            CollectionAssert.DoesNotContain(lobbies.Left, OWN, "its own lobby is kept until the new one is entered");

            lobbies.RaiseEntered(71, true);
            CollectionAssert.Contains(lobbies.Left, OWN, "then its own lobby is left");
            Assert.AreEqual(1, session.Leaves, "and its own session");
            CollectionAssert.AreEqual(new[] { 171UL }, session.JoinedHosts, "joins the new lobby's owner");
            Assert.AreEqual(71UL, lobby.Current);
            Assert.IsFalse(lobby.IsHost);
            CollectionAssert.DoesNotContain(lobbies.Left, 71UL, "the new lobby is kept as the old session goes offline");

            session.LetIn();
            Assert.IsFalse(match.Searching, "done once the new host lets it in");
            Assert.IsEmpty(failures);
            Assert.IsEmpty(notices);
        }

        [Test]
        public void Find_NoneFound_TurnsFillOn_AndTellsNone()
        {
            Assert.IsTrue(match.Find());
            lobbies.AnswerSearch();

            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
            Assert.IsTrue(lobby.Filling, "its own lobby opens to anyone");
            CollectionAssert.Contains(lobbies.PublicCalls, (OWN, true));
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
        }

        [Test]
        public void Find_ThreeFailures_StopsAndFills()
        {
            Open(70, 170, 1);
            Open(71, 171, 1);
            Open(72, 172, 2);
            Open(73, 173, 3);

            match.Find();
            lobbies.AnswerSearch(70, 71, 72, 73);
            for (int i = 0; i < 3; i++)
            {
                lobbies.RaiseEntered(lobbies.Joins[lobbies.Joins.Count - 1], false);
                match.Tick();
            }

            CollectionAssert.AreEqual(new[] { 70UL, 71UL, 72UL }, lobbies.Joins, "the fourth is not tried");
            Assert.AreEqual(3, failures.Count);
            Assert.AreEqual(OnlineLobby.SwitchFailure.CouldNotEnter, failures[0].why);
            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
            Assert.AreEqual(OWN, lobby.Current, "its own lobby was never left");
            Assert.IsTrue(lobby.Filling);
            Assert.AreEqual(0, session.Leaves);
        }

        [Test]
        public void Find_NoSeat_DoesNotCountATry()
        {
            Open(70, 170, 1);
            Open(71, 171, 1);
            Open(72, 172, 2);
            Open(73, 173, 3);

            match.Find();
            lobbies.AnswerSearch(70, 71, 72, 73);
            lobbies.RaiseEntered(70, true);
            session.TurnAway(JoinRules.FULL); // another took the last seat first
            CollectionAssert.AreEqual(new[] { (70UL, OnlineLobby.SwitchFailure.NoSeat) }, failures);
            Assert.AreEqual(0UL, lobby.Current, "the full lobby is left");
            match.Tick();

            for (int i = 0; i < 3; i++)
            {
                lobbies.RaiseEntered(lobbies.Joins[lobbies.Joins.Count - 1], false);
                match.Tick();
            }

            CollectionAssert.AreEqual(new[] { 70UL, 71UL, 72UL, 73UL }, lobbies.Joins, "three more tries after the full one");
            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
            Assert.AreEqual(2, lobbies.Creates.Count, "its own lobby was left, so it hosts a new one");
            lobbies.RaiseCreated(43);
            Assert.AreEqual(43UL, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.IsTrue(lobby.Filling, "open to anyone");
            Assert.IsEmpty(lobbies.Invites, "without the invite dialog");
        }

        [Test]
        public void Find_SomeoneJoinsDuringSearch_StaysHost()
        {
            Open(70, 170, 1);

            Assert.IsTrue(match.Find());
            others = 1; // a friend joined
            match.Tick();
            Assert.IsFalse(match.Searching, "the search is cancelled");

            lobbies.AnswerSearch(70);
            Assert.IsEmpty(lobbies.Joins, "and its answer ignored");
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.IsFalse(lobby.Filling);
            Assert.IsEmpty(notices);
        }

        [Test]
        public void Find_WithOthersInLobby_Refused_WithNotice()
        {
            others = 1;

            Assert.IsFalse(match.Find());
            Assert.IsFalse(match.Searching);
            Assert.IsEmpty(lobbies.Searches);
            CollectionAssert.AreEqual(new[] { MatchRules.FIND_REFUSED }, notices);
        }

        [Test]
        public void Find_TooSoon_Ignored()
        {
            Assert.IsTrue(match.Find());
            lobbies.AnswerSearch();
            now += 2;

            Assert.IsFalse(match.Find(), "a second Find 2 s later");
            Assert.AreEqual(1, lobbies.Searches.Count);

            now += MatchRules.SEARCH_GAP;
            Assert.IsTrue(match.Find(), "once the gap has passed");
        }

        [Test]
        public void Tick_FillingAlone_ResearchesEvery15s()
        {
            lobby.SetFill(true);

            now += MatchRules.RESEARCH - 1;
            match.Tick();
            Assert.IsEmpty(lobbies.Searches);

            now += 1;
            match.Tick();
            Assert.AreEqual(1, lobbies.Searches.Count, "a lone Fill host searches again");
            lobbies.AnswerSearch();
            Assert.IsFalse(match.Searching);
            Assert.IsEmpty(notices, "a quiet search: nobody is told nothing was found");

            now += MatchRules.RESEARCH - 1;
            match.Tick();
            Assert.AreEqual(1, lobbies.Searches.Count);
            now += 1;
            match.Tick();
            Assert.AreEqual(2, lobbies.Searches.Count, "every 15 s");

            lobbies.AnswerSearch();
            others = 1;
            now += MatchRules.RESEARCH;
            match.Tick();
            Assert.AreEqual(2, lobbies.Searches.Count, "not once someone joined");
        }

        [Test]
        public void Tick_YieldsOnlyToLowerId()
        {
            Open(70, 600, 1); // the fullest, but its owner's ID is higher: they'd join this lobby instead
            Open(71, 400, 2);
            lobby.SetFill(true);

            now += MatchRules.RESEARCH;
            match.Tick();
            lobbies.AnswerSearch(70, 71);

            CollectionAssert.AreEqual(new[] { 71UL }, lobbies.Joins, "only the lower ID's lobby");
        }

        [Test]
        public void Switch_RefusedTags_KeepsOwnLobby()
        {
            lobbies.AddLobby(77, LobbyRules.GAME, "9.9", 177); // another build

            Assert.IsTrue(lobby.Switch(77));
            lobbies.RaiseEntered(77, true);

            CollectionAssert.AreEqual(new[] { (77UL, OnlineLobby.SwitchFailure.Refused) }, failures);
            CollectionAssert.AreEqual(new[] { 77UL }, lobbies.Left, "only the refused lobby is left");
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.AreEqual(0, session.Leaves);
            Assert.IsEmpty(session.JoinedHosts);
        }

        [Test]
        public void Switch_WithOthersInSession_NotAllowed()
        {
            Open(77, 177, 1);
            others = 1;

            Assert.IsFalse(lobby.Switch(77));
            Assert.IsEmpty(lobbies.Joins);
        }

        [Test]
        public void Switch_JoinFailsAfterLeaving_RehostsFilled()
        {
            Open(70, 170, 1);
            session.JoinWorks = false;

            match.Find();
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true);
            CollectionAssert.AreEqual(new[] { (70UL, OnlineLobby.SwitchFailure.JoinFailed) }, failures);
            CollectionAssert.Contains(lobbies.Left, OWN);
            CollectionAssert.Contains(lobbies.Left, 70UL, "the lobby it couldn't play in is left");
            Assert.AreEqual(1, lobbies.Creates.Count, "nothing started while the old session stops");

            match.Tick();
            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
            Assert.AreEqual(2, lobbies.Creates.Count, "a new lobby");
            lobbies.RaiseCreated(43);
            Assert.AreEqual(43UL, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.IsTrue(lobby.Filling, "open to anyone");
            Assert.IsEmpty(lobbies.Invites);
        }

        [Test]
        public void LateFoundAfterCancel_Ignored()
        {
            Open(70, 170, 1);

            match.Find();
            match.Cancel();
            Assert.IsFalse(match.Searching);
            lobbies.AnswerSearch(70);

            Assert.IsEmpty(lobbies.Joins);
            Assert.IsFalse(lobby.Filling, "a cancelled search changes nothing");
            Assert.IsEmpty(notices);
        }

        [Test]
        public void LateEnteredAfterCancel_Left()
        {
            Open(70, 170, 1);

            match.Find();
            lobbies.AnswerSearch(70);
            match.Cancel();
            lobbies.RaiseEntered(70, true);

            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Left, "the lobby answering late is left");
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.IsEmpty(session.JoinedHosts);
        }
    }
}
