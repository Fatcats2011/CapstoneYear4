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
    }
}
