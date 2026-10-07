using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Which cameras need to render (CameraBudget): a player's view isn't drawn while a full-screen camera covers it (the
    /// cutscene and results cameras), and the menu preview camera only renders in player select, the one place its
    /// picture is shown. See docs/performance.md
    /// </summary>
    public class CameraBudgetTests
    {
        static readonly Rect Full = new Rect(0f, 0f, 1f, 1f);

        [Test]
        public void Covers_AFullScreenCameraDrawnOverThePlayers()
        {
            Assert.IsTrue(CameraBudget.Covers(false, Full, CameraClearFlags.Skybox, 20f), "the cutscene camera");
            Assert.IsTrue(CameraBudget.Covers(false, Full, CameraClearFlags.SolidColor, 100f), "the results camera");
        }

        [Test]
        public void Covers_NotAPlayersOwnCameraOrADepthOnlyOne()
        {
            Assert.IsFalse(CameraBudget.Covers(false, Full, CameraClearFlags.Skybox, 1f), "a player's main camera");
            Assert.IsFalse(CameraBudget.Covers(false, Full, CameraClearFlags.Depth, 20f), "draws over without clearing");
            Assert.IsFalse(CameraBudget.Covers(false, Full, CameraClearFlags.Nothing, 20f));
            Assert.IsFalse(CameraBudget.Covers(true, Full, CameraClearFlags.Skybox, 20f), "renders into a texture");
            Assert.IsFalse(CameraBudget.Covers(false, new Rect(0f, 0f, 0.5f, 1f), CameraClearFlags.Skybox, 20f), "half the screen");
        }

        [Test]
        public void PreviewShown_OnlyInPlayerSelect()
        {
            Assert.IsTrue(CameraBudget.PreviewShown(GameState.PlayerSelect));
            foreach (GameState state in new[] { GameState.Menu, GameState.Options, GameState.Credits, GameState.Loading,
                GameState.StartingCutscene, GameState.Tutorial, GameState.Begin, GameState.MainLoop, GameState.FinalPackage,
                GameState.Results, GameState.Paused })
                Assert.IsFalse(CameraBudget.PreviewShown(state), state.ToString());
        }
    }
}
