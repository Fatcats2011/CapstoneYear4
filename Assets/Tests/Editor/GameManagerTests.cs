using System.Collections.Generic;
using NUnit.Framework;

namespace DoA.Tests
{
    /// <summary>
    /// StateApplied tells every state the game switches to, in the order it happens, even a state switched to from
    /// inside another state's event. Online, the host sends each one to the clients
    /// </summary>
    public class GameManagerTests
    {
        readonly TestObjects objects = new TestObjects();
        readonly List<GameState> applied = new List<GameState>();
        GameManager game;

        [SetUp]
        public void SetUp()
        {
            game = objects.Add<GameManager>();
            game.StateApplied += applied.Add;
        }

        [TearDown]
        public void TearDown()
        {
            applied.Clear();
            objects.DestroyAll();
        }

        [Test]
        public void ApplyGameState_TellsStateAppliedBeforeTheStatesOwnEvent()
        {
            bool toldFirst = false;
            game.OnSwapPlayerSelect += () => toldFirst = applied.Contains(GameState.PlayerSelect);

            game.ApplyGameState(GameState.PlayerSelect);

            Assert.IsTrue(toldFirst);
            Assert.AreEqual(GameState.PlayerSelect, game.MainState);
        }

        [Test]
        public void AStateSwitchedToInsideAnother_IsToldAfterIt()
        {
            // What the opening cutscene does: the spawn handler switches to the main loop inside the cutscene's switch
            game.OnSwapStartingCutscene += () => game.SetGameState(GameState.MainLoop);

            game.SetGameState(GameState.StartingCutscene);

            CollectionAssert.AreEqual(new[] { GameState.StartingCutscene, GameState.MainLoop }, applied);
            Assert.AreEqual(GameState.MainLoop, game.MainState);
        }

        [Test]
        public void Tutorial_IsTold_ThoughOnSwapAnythingSkipsIt()
        {
            game.ApplyGameState(GameState.Tutorial);

            CollectionAssert.AreEqual(new[] { GameState.Tutorial }, applied);
        }

        [Test]
        public void ApplyGameState_MenuOrPlayerSelect_RaisesNewMatch()
        {
            // Online the menu scene opens straight on player select (the lobby), so both start the next match fresh
            int newMatches = 0;
            game.OnNewMatch += () => newMatches++;

            game.ApplyGameState(GameState.Menu);
            game.ApplyGameState(GameState.PlayerSelect);

            Assert.AreEqual(2, newMatches);
        }

        [Test]
        public void ApplyGameState_OtherStates_DoNotRaiseNewMatch()
        {
            int newMatches = 0;
            game.OnNewMatch += () => newMatches++;

            game.ApplyGameState(GameState.Results);
            game.ApplyGameState(GameState.Loading);
            game.ApplyGameState(GameState.MainLoop);

            Assert.AreEqual(0, newMatches);
        }

        [Test]
        public void StartNewMatch_RaisesNewMatchButNoMenuEvent()
        {
            // A rematch: the match starts over without the menu's music, title screen or podiums
            game.ApplyGameState(GameState.Results);
            int newMatches = 0, menus = 0;
            game.OnNewMatch += () => newMatches++;
            game.OnSwapMenu += () => menus++;

            game.StartNewMatch();

            Assert.AreEqual(1, newMatches);
            Assert.AreEqual(0, menus);
            Assert.AreEqual(GameState.Results, game.MainState);
        }
    }
}
