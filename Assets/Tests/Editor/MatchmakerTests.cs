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
        bool menus;
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

        bool InMenus()
        {
            return menus;
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
            menus = true;
            lobbies = new FakeLobbyService { Me = 500 };
            session = new FakeSessionControl();
            lobby = new OnlineLobby(lobbies, session, BUILD, Clock, OtherMachines);
            lobby.ShowState(GameState.PlayerSelect);
            match = new Matchmaker(lobbies, lobby, BUILD, Clock, OtherMachines, InMenus);
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
            Assert.IsTrue(match.ResearchingQuietly);
            Assert.IsFalse(match.Searching, "quietly: not shown as the player's Find");
            lobbies.AnswerSearch();
            Assert.IsFalse(match.ResearchingQuietly);
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

        // Finding 1: the player leaving once the old lobby was left
        [Test]
        public void Cancel_AfterLeavingOwnLobby_LeavesTheNewLobbyAndSession()
        {
            Open(70, 170, 1);

            match.Find();
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true);
            Assert.AreEqual(70UL, lobby.Current, "joining the new host");

            match.Cancel();
            Assert.IsFalse(match.Searching);
            Assert.AreEqual(0UL, lobby.Current, "no lobby held");
            CollectionAssert.Contains(lobbies.Left, 70UL);
            Assert.AreEqual(2, session.Leaves, "the joining session is left too");
            Assert.IsFalse(session.IsRunning);

            session.LetIn(); // too late
            match.Tick();
            Assert.IsEmpty(notices, "quietly");
            Assert.AreEqual(1, lobbies.Creates.Count, "no fallback");
            Assert.IsTrue(match.Find() == false, "not hosting any more");
        }

        // Finding 2: leaving the menus while a lobby is being entered
        [Test]
        public void LeavingTheMenusWhileEntering_StopsQuietly()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);

            match.Find();
            lobbies.AnswerSearch(70, 71);
            lobby.ShowState(GameState.Loading);
            menus = false;
            CollectionAssert.AreEqual(new[] { (70UL, OnlineLobby.SwitchFailure.CouldNotEnter) }, failures, "Find hears it");

            match.Tick();
            Assert.IsFalse(match.Searching);
            lobbies.RaiseEntered(70, true);
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins, "the next lobby isn't tried");
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Left, "the late answer is left");
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsFalse(lobby.Filling, "no fallback");
            Assert.IsEmpty(notices);
        }

        // Finding 2: Steam never answers the search
        [Test]
        public void SearchNeverAnswered_TimesOut_AsNoneFound()
        {
            Open(70, 170, 1);
            match.Find();

            now += MatchRules.SEARCH_TIMEOUT - 1;
            match.Tick();
            Assert.IsTrue(match.Searching);

            now += 1;
            match.Tick();
            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
            Assert.IsTrue(lobby.Filling);

            lobbies.AnswerSearch(70);
            Assert.IsEmpty(lobbies.Joins, "a late answer is ignored");
        }

        // Finding 3: a lone Fill host outside the menus (in a match) doesn't search or move
        [Test]
        public void NotInMenus_NoFindAndNoResearch()
        {
            lobby.SetFill(true);
            menus = false;

            Assert.IsFalse(match.Find());
            now += MatchRules.RESEARCH * 2;
            match.Tick();
            Assert.IsEmpty(lobbies.Searches);
        }

        // Finding 3: a search answering after the player left the menus
        [Test]
        public void FoundAfterLeavingTheMenus_Ignored()
        {
            Open(70, 170, 1);
            lobby.SetFill(true);
            now += MatchRules.RESEARCH;
            match.Tick();
            Assert.AreEqual(1, lobbies.Searches.Count);

            menus = false;
            lobbies.AnswerSearch(70);
            Assert.IsEmpty(lobbies.Joins);
            Assert.IsFalse(match.ResearchingQuietly);
        }

        // Finding 4: no retry or fallback once the player left the menus
        [Test]
        public void RetryAfterLeaving_NotInMenus_StopsQuietly()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);
            session.JoinWorks = false;

            match.Find();
            lobbies.AnswerSearch(70, 71);
            lobbies.RaiseEntered(70, true); // JoinFailed after leaving
            menus = false;
            match.Tick();

            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins, "the next lobby isn't tried");
            Assert.AreEqual(1, lobbies.Creates.Count, "no lobby hosted again");
            Assert.IsEmpty(notices);
        }

        // Finding 4: a candidate that can't be tried yet isn't skipped
        [Test]
        public void Retry_WhileTheLastSessionStops_WaitsInsteadOfSkipping()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);

            match.Find();
            lobbies.AnswerSearch(70, 71);
            lobbies.RaiseEntered(70, true);
            session.TurnAway(JoinRules.FULL);
            session.IsRunning = true; // still stopping

            match.Tick();
            match.Tick();
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins, "not yet");
            Assert.IsTrue(match.Searching);
            Assert.IsEmpty(notices, "no false \"no open matches\"");

            session.IsRunning = false;
            match.Tick();
            CollectionAssert.AreEqual(new[] { 70UL, 71UL }, lobbies.Joins, "then the next lobby");
        }

        // Finding 4: a session that never stops doesn't keep Find waiting forever
        [Test]
        public void Found_AHugeAnswer_OnlyTheFirstMaxResultsAreRead()
        {
            // Steam's answer is capped in SteamLobbyService; Find doesn't count on it
            ulong[] huge = new ulong[100000];
            for (int i = 0; i < huge.Length; i++)
                huge[i] = 1000 + (ulong)i;

            match.Find();
            lobbies.Reads = 0;
            lobbies.AnswerSearch(huge);

            Assert.LessOrEqual(lobbies.Reads, MatchRules.MAX_RESULTS * 5, "an owner and four tags per lobby looked at");
            Assert.IsFalse(match.Searching, "none of them is this game's: none found");
        }

        [Test]
        public void Found_AgainAfterTheAnswer_IsIgnored()
        {
            // Steam (or a fake answer) repeating itself doesn't start a second round of tries
            Open(70, 170, 1);
            match.Find();
            lobbies.AnswerSearch(70);
            lobbies.AnswerSearch(70);
            lobbies.AnswerSearch(70);

            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins);
        }

        // M-8: the notice says the lobby is open only when it is
        [Test]
        public void Retry_SessionNeverStops_GivesUpAsNoneFound_WithoutClaimingAnOpenLobby()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);

            match.Find();
            lobbies.AnswerSearch(70, 71);
            lobbies.RaiseEntered(70, true);
            session.TurnAway(JoinRules.FULL);
            session.IsRunning = true;
            match.Tick();
            now += MatchRules.SEARCH_TIMEOUT;
            match.Tick();

            Assert.IsFalse(match.Searching);
            Assert.AreEqual(1, lobbies.Creates.Count, "no lobby can be hosted while the old session still runs");
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND_CLOSED }, notices, "so it isn't said to be open");
        }

        // I-1: Fill turned off stops a quiet search: its answer neither opens the lobby again nor moves the host
        [Test]
        public void QuietSearch_FillTurnedOff_StopsAndNeverReopens()
        {
            lobby.SetFill(true);
            now += MatchRules.RESEARCH;
            match.Tick();
            Assert.IsTrue(match.ResearchingQuietly);

            lobby.SetFill(false);
            int publicCalls = lobbies.PublicCalls.Count;
            Assert.IsFalse(match.ResearchingQuietly, "stopped");
            lobbies.AnswerSearch();

            Assert.IsFalse(lobby.Filling);
            Assert.AreEqual(publicCalls, lobbies.PublicCalls.Count, "no new (OWN, true): the lobby isn't made public again");
            Assert.IsEmpty(notices);
        }

        // I-1: Fill turned off while a quiet search enters a stranger's lobby: the host stays
        [Test]
        public void QuietSearch_FillTurnedOffWhileEntering_HostStays()
        {
            Open(70, 400, 1);
            lobby.SetFill(true);
            now += MatchRules.RESEARCH;
            match.Tick();
            lobbies.AnswerSearch(70);
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins);

            lobby.SetFill(false);
            lobbies.RaiseEntered(70, true);

            CollectionAssert.Contains(lobbies.Left, 70UL, "the stranger's lobby answering late is left");
            Assert.AreEqual(OWN, lobby.Current);
            Assert.IsTrue(lobby.IsHost);
            Assert.AreEqual(0, session.Leaves, "its own session is kept");
            Assert.IsEmpty(session.JoinedHosts);
            Assert.IsFalse(lobby.Filling);
        }

        // I-1: the same for the player's Find, Fill turned off mid-search
        [Test]
        public void Find_FillTurnedOffMidSearch_StopsAndStaysFriendsOnly()
        {
            lobby.SetFill(true);
            Assert.IsTrue(match.Find());

            lobby.SetFill(false);
            int publicCalls = lobbies.PublicCalls.Count;
            Assert.IsFalse(match.Searching, "stopped");
            lobbies.AnswerSearch();
            now += MatchRules.SEARCH_TIMEOUT;
            match.Tick();

            Assert.IsFalse(lobby.Filling);
            Assert.AreEqual(publicCalls, lobbies.PublicCalls.Count, "no fallback opens it");
            Assert.IsEmpty(notices);
        }

        // M-5 with I-1: the switch leaving the host's open lobby (back to friends only) isn't the host turning Fill off
        [Test]
        public void QuietSwitch_LeavingTheOpenLobby_GoesOn()
        {
            Open(70, 400, 1);
            lobby.SetFill(true);
            now += MatchRules.RESEARCH;
            match.Tick();
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true);

            Assert.AreEqual((OWN, false), lobbies.PublicCalls[lobbies.PublicCalls.Count - 1], "its own lobby friends only as it's left");
            Assert.IsTrue(match.ResearchingQuietly, "the switch goes on");
            Assert.IsTrue(match.Switching, "between sessions");
            CollectionAssert.AreEqual(new[] { 400UL }, session.JoinedHosts);

            session.LetIn();
            Assert.IsFalse(match.ResearchingQuietly);
            Assert.IsFalse(match.Switching);
            Assert.AreEqual(70UL, lobby.Current);
        }

        // A quiet search that left its lobby and found nothing hosts again, open as the host had it, and says nothing
        [Test]
        public void QuietSwitch_FailsAfterLeaving_HostsAgainOpen_Quietly()
        {
            Open(70, 400, 1);
            session.JoinWorks = false;
            lobby.SetFill(true);
            now += MatchRules.RESEARCH;
            match.Tick();
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true); // can't join its host, after leaving
            match.Tick();

            Assert.IsFalse(match.ResearchingQuietly);
            Assert.AreEqual(2, lobbies.Creates.Count, "hosted again");
            lobbies.RaiseCreated(43);
            Assert.IsTrue(lobby.Filling, "open to anyone, as the host had it");
            Assert.IsEmpty(notices);
        }

        // M-6: Switching covers only the moment between sessions, for the player's Find and a quiet search alike
        [Test]
        public void Switching_OnlyBetweenSessions()
        {
            Open(70, 170, 1);

            match.Find();
            Assert.IsFalse(match.Switching, "searching: still hosting");
            lobbies.AnswerSearch(70);
            Assert.IsFalse(match.Switching, "entering the found lobby: still hosting");
            lobbies.RaiseEntered(70, true);
            Assert.IsTrue(match.Switching, "its own session left, the new host not reached yet");
            Assert.IsTrue(match.Searching);

            session.LetIn();
            Assert.IsFalse(match.Switching);
            Assert.IsFalse(match.Searching);
        }

        // M-7: hosts with no free seat don't count as tries, but no more than MAX_ATTEMPTS lobbies are moved into
        [Test]
        public void Find_NoSeatRefusals_CappedAtMaxAttempts()
        {
            int lobbiesFound = MatchRules.MAX_ATTEMPTS + 2;
            ulong[] found = new ulong[lobbiesFound];
            for (int i = 0; i < lobbiesFound; i++)
            {
                found[i] = 70 + (ulong)i;
                Open(found[i], 170 + (ulong)i, 1);
            }

            match.Find();
            lobbies.AnswerSearch(found);
            for (int i = 0; i < lobbiesFound && match.Searching; i++)
            {
                lobbies.RaiseEntered(lobbies.Joins[lobbies.Joins.Count - 1], true);
                session.TurnAway(JoinRules.FULL); // another took the last seat first
                match.Tick();
            }

            Assert.AreEqual(MatchRules.MAX_ATTEMPTS, lobbies.Joins.Count, "the rest aren't tried");
            Assert.IsFalse(match.Searching);
            Assert.AreEqual(2, lobbies.Creates.Count, "it ends as none found: its own lobby was left, so it hosts again");
            CollectionAssert.AreEqual(new[] { MatchRules.NONE_FOUND }, notices);
        }

        // Deferred T5: a found lobby Steam never answers is a failed try after SEARCH_TIMEOUT
        [Test]
        public void FoundLobbyNeverAnswers_TimesOut_AndTheNextIsTried()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);

            match.Find();
            lobbies.AnswerSearch(70, 71);
            now += MatchRules.SEARCH_TIMEOUT - 1;
            match.Tick();
            Assert.IsEmpty(failures, "not yet");

            now += 1;
            match.Tick();
            CollectionAssert.AreEqual(new[] { (70UL, OnlineLobby.SwitchFailure.CouldNotEnter) }, failures);
            match.Tick();
            CollectionAssert.AreEqual(new[] { 70UL, 71UL }, lobbies.Joins, "the next lobby");
            Assert.IsTrue(match.Searching);

            lobbies.RaiseEntered(70, true); // too late
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Left, "the late answer is left");
            Assert.AreEqual(OWN, lobby.Current, "its own lobby kept while 71 is entered");
            Assert.IsTrue(lobby.IsHost);
            Assert.AreEqual(0, session.Leaves);
        }

        // M-4: a friend's invite answered between sessions drops the switch first, then joins the friend
        [Test]
        public void Answer_WhileSwitching_CancelsTheSwitch_ThenJoinsTheFriend()
        {
            Open(70, 170, 1);
            lobbies.AddLobby(9, LobbyRules.GAME, BUILD, 99);

            match.Find();
            lobbies.AnswerSearch(70);
            lobbies.RaiseEntered(70, true);
            Assert.IsTrue(match.Switching);

            lobby.Answer(9, null); // offline for the moment, so not busy
            Assert.IsFalse(match.Searching, "Find stops");
            Assert.IsFalse(match.Switching);
            CollectionAssert.Contains(lobbies.Left, 70UL, "the switch's lobby is left");
            Assert.AreEqual(2, session.Leaves, "and the session joining its host");
            Assert.AreEqual(9UL, lobbies.Joins[lobbies.Joins.Count - 1], "then the friend's lobby is entered");

            lobbies.RaiseEntered(9, true);
            CollectionAssert.AreEqual(new[] { 170UL, 99UL }, session.JoinedHosts);
            Assert.AreEqual(9UL, lobby.Current);

            session.LetIn();
            now += MatchRules.SEARCH_TIMEOUT;
            match.Tick();
            Assert.AreEqual(9UL, lobby.Current, "no switch or fallback takes it out again");
            Assert.AreEqual(1, lobbies.Creates.Count);
            Assert.IsEmpty(notices);
        }

        // M-4: between two tries (no lobby, no session) an invite stops Find before the next lobby is tried
        [Test]
        public void Answer_BetweenTries_NextLobbyNotTried()
        {
            Open(70, 170, 1);
            Open(71, 171, 2);
            lobbies.AddLobby(9, LobbyRules.GAME, BUILD, 99);

            match.Find();
            lobbies.AnswerSearch(70, 71);
            lobbies.RaiseEntered(70, true);
            session.TurnAway(JoinRules.FULL);
            Assert.AreEqual(0UL, lobby.Current);

            lobby.Answer(9, null);
            match.Tick();

            Assert.IsFalse(match.Searching);
            CollectionAssert.AreEqual(new[] { 70UL, 9UL }, lobbies.Joins, "71 isn't tried");
        }

        // M-4: while this machine still hosts its own lobby it's busy: the invite is refused and the switch goes on
        [Test]
        public void Answer_Busy_LeavesTheSwitchAlone()
        {
            Open(70, 170, 1);
            lobbies.AddLobby(9, LobbyRules.GAME, BUILD, 99);

            match.Find();
            lobbies.AnswerSearch(70);
            lobby.Answer(9, LobbyRules.ALREADY_ONLINE);

            Assert.IsTrue(match.Searching);
            CollectionAssert.AreEqual(new[] { 70UL }, lobbies.Joins);
            lobbies.RaiseEntered(70, true);
            CollectionAssert.AreEqual(new[] { 170UL }, session.JoinedHosts, "the switch carries on");
        }
    }
}
