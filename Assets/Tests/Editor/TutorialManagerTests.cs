using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// The tutorial ends when every seat's player has finished it: this machine's players when their own triggers say
    /// so, and online another machine's player when that machine says so. Online only the host starts the first wave,
    /// and a player who leaves doesn't hold the others up. Each match starts the count over
    /// </summary>
    public class TutorialManagerTests
    {
        readonly TestObjects objects = new TestObjects();
        GameManager game;
        PlayerInstantiate players;
        TutorialManager tutorial;
        int ended;

        [SetUp]
        public void SetUp()
        {
            game = objects.Add<GameManager>();
            GameManager.instance = game;
            players = objects.Add<PlayerInstantiate>();
            PlayerInstantiate.instance = players;
            tutorial = objects.Add<TutorialManager>();
            TutorialManager.instance = tutorial;
            tutorial.OnEnable(); // it hears the game's states
            game.SetGameState(GameState.Tutorial);
            ended = 0;
            tutorial.OnTutorialComplete += CountEnd;
        }

        [TearDown]
        public void TearDown()
        {
            tutorial.OnDisable();
            TutorialManager.instance = null;
            PlayerInstantiate.instance = null;
            GameManager.instance = null;
            GameAuthority.Role = NetworkRole.Offline;
            TutorialSync.Reset();
            objects.DestroyAll();
        }

        void CountEnd()
        {
            ended++;
        }

        // A player on this machine: their TutorialHandler, on the "Control" child of their seat's object
        TutorialHandler Player(int seat)
        {
            GameObject root = objects.NewGameObject("P" + (seat + 1));
            GameObject control = new GameObject("Control");
            control.transform.SetParent(root.transform);
            TutorialHandler handler = control.AddComponent<TutorialHandler>();
            players.Roster.JoinRemoteAt(root, (ulong)seat, seat); // a seat is all the count needs
            return handler;
        }

        // Another machine's player: their seat, with no TutorialHandler here
        void Remote(int seat)
        {
            players.Roster.JoinRemoteAt(objects.NewGameObject("P" + (seat + 1)), (ulong)seat, seat);
        }

        [Test]
        public void EveryPlayerFinished_TheFirstWaveBegins()
        {
            // A local match
            TutorialHandler first = Player(0), second = Player(1);
            tutorial.IncrementAlumni(first);
            Assert.AreNotEqual(GameState.Begin, game.MainState, "one still in the tutorial");
            tutorial.IncrementAlumni(second);
            Assert.AreEqual(GameState.Begin, game.MainState);
            Assert.AreEqual(1, ended);
        }

        [Test]
        public void OnTheHost_AnotherMachinesPlayer_CountsWhenTheirMachineSaysSo()
        {
            GameAuthority.Role = NetworkRole.Host;
            TutorialHandler mine = Player(0);
            Remote(1);
            tutorial.IncrementAlumni(mine);
            Assert.AreNotEqual(GameState.Begin, game.MainState, "waits for the other machine's player");
            tutorial.SeatLearnt(1);
            Assert.AreEqual(GameState.Begin, game.MainState);
        }

        [Test]
        public void OnAClient_ItsPlayerFinishing_GoesToTheHost_AndEndsNothingHere()
        {
            GameAuthority.Role = NetworkRole.Client;
            Remote(0);
            TutorialHandler mine = Player(1);
            List<int> told = new List<int>();
            TutorialSync.Finished += told.Add;

            tutorial.IncrementAlumni(mine);
            tutorial.SeatLearnt(0);

            CollectionAssert.AreEqual(new[] { 1 }, told);
            Assert.AreNotEqual(GameState.Begin, game.MainState, "the host starts the first wave");
            Assert.AreEqual(0, ended);
        }

        [Test]
        public void APlayerWhoLeaves_DoesntHoldUpTheOthers()
        {
            GameAuthority.Role = NetworkRole.Host;
            TutorialHandler mine = Player(0);
            Remote(1);
            tutorial.IncrementAlumni(mine);

            players.Roster.LeaveSlot(1);
            tutorial.RecheckAlumni();

            Assert.AreEqual(GameState.Begin, game.MainState);
        }

        [Test]
        public void TheHostLeaves_AClientWhosePlayerFinished_GoesOnOffline()
        {
            GameAuthority.Role = NetworkRole.Client;
            Remote(0);
            TutorialHandler mine = Player(1);
            tutorial.IncrementAlumni(mine);

            GameAuthority.Role = NetworkRole.Offline;
            players.Roster.LeaveSlot(0);
            tutorial.RecheckAlumni();

            Assert.AreEqual(GameState.Begin, game.MainState);
        }

        [Test]
        public void ANewMatch_StartsTheTutorialOver()
        {
            // A match left mid-tutorial: who finished it doesn't count in the next one
            TutorialHandler first = Player(0), second = Player(1);
            tutorial.IncrementAlumni(second);
            game.SetGameState(GameState.StartingCutscene);
            game.SetGameState(GameState.Tutorial);
            tutorial.IncrementAlumni(first);
            Assert.AreNotEqual(GameState.Begin, game.MainState, "the second player hasn't finished this match's tutorial");
        }

        [Test]
        public void BackInTheLobby_APlayerLeaving_BeginsNoWave()
        {
            // A match left mid-tutorial, after this machine's player had finished it
            GameAuthority.Role = NetworkRole.Host;
            TutorialHandler mine = Player(0);
            Remote(1);
            tutorial.IncrementAlumni(mine);
            game.SetGameState(GameState.PlayerSelect);

            players.Roster.LeaveSlot(1);
            tutorial.RecheckAlumni();

            Assert.AreEqual(GameState.PlayerSelect, game.MainState, "still in the lobby");
        }

        [Test]
        public void InTheGoldenRound_FinishingBeginsNoWave()
        {
            tutorial.ShouldTutorialize = false; // the waves are over
            tutorial.IncrementAlumni(Player(0));
            Assert.AreNotEqual(GameState.Begin, game.MainState);
        }
    }
}
