using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A rematch skips the next match's tutorial once (TutorialManager.SkipNextTutorial): the opening cutscene hands
    /// straight over to the first wave, and every player starts as if they'd learnt it. The menus clear the skip, so a
    /// match started from them teaches again
    /// </summary>
    public class TutorialSkipTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            TutorialManager.SkipNextTutorial = false;
            TutorialManager.instance = null;
            GameManager.instance = null;
            objects.DestroyAll();
        }

        [Test]
        public void Tutorial_WithASkip_HandsOverTheControlsThenGoesStraightOnToTheFirstWave()
        {
            // The tutorial state is where the scooters unfreeze and get their driving controls and engines, so a rematch
            // passes through it: its listeners run, then the first wave starts at once
            GameManager game = objects.Add<GameManager>();
            System.Collections.Generic.List<GameState> applied = new System.Collections.Generic.List<GameState>();
            game.StateApplied += applied.Add;
            int handOvers = 0;
            game.OnSwapTutorial += () => handOvers++;
            TutorialManager.SkipNextTutorial = true;

            game.SetGameState(GameState.Tutorial);

            Assert.AreEqual(1, handOvers, "the controls are handed over");
            CollectionAssert.AreEqual(new[] { GameState.Tutorial, GameState.Begin }, applied);
            Assert.AreEqual(GameState.Begin, game.MainState);
        }

        [Test]
        public void Tutorial_WithoutASkip_Stays()
        {
            GameManager game = objects.Add<GameManager>();

            game.SetGameState(GameState.Tutorial);

            Assert.AreEqual(GameState.Tutorial, game.MainState);
        }

        [Test]
        public void TheOpeningCutscene_AlwaysHandsOverToTheTutorialState()
        {
            TutorialManager.SkipNextTutorial = true;

            Assert.AreEqual(GameState.Tutorial, CutsceneManager.NextStateOf(0));
            Assert.AreEqual(GameState.FinalPackage, CutsceneManager.NextStateOf(1));
        }

        [Test]
        public void MenuOrPlayerSelect_ClearsTheSkip()
        {
            GameManager game = objects.Add<GameManager>();

            TutorialManager.SkipNextTutorial = true;
            game.ApplyGameState(GameState.Menu);
            Assert.IsFalse(TutorialManager.SkipNextTutorial, "the title screen");

            TutorialManager.SkipNextTutorial = true;
            game.ApplyGameState(GameState.PlayerSelect);
            Assert.IsFalse(TutorialManager.SkipNextTutorial, "the lobby");
        }

        [Test]
        public void TheFirstWave_UsesTheSkipUp()
        {
            GameManager game = objects.Add<GameManager>();
            GameManager.instance = game;
            TutorialManager tutorial = objects.Add<TutorialManager>();
            TutorialManager.instance = tutorial;
            tutorial.OnEnable();
            TutorialManager.SkipNextTutorial = true;

            game.ApplyGameState(GameState.Begin);

            Assert.IsFalse(TutorialManager.SkipNextTutorial);
            tutorial.OnDisable();
        }

        [Test]
        public void TutorialHandler_ResetWithASkip_HasLearnt()
        {
            TutorialManager.instance = objects.Add<TutorialManager>();
            GameObject player = objects.NewGameObject("Control");
            player.AddComponent<BallDriving>();
            TutorialHandler handler = player.AddComponent<TutorialHandler>();
            GameObject image = new GameObject("Tutorial Image");
            image.transform.SetParent(player.transform);
            SerializedObject so = new SerializedObject(handler);
            so.FindProperty("tutorialText").objectReferenceValue = new GameObject("Text", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            so.FindProperty("tutorialTextPurple").objectReferenceValue = new GameObject("Purple", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            so.FindProperty("tutorialImage").objectReferenceValue = image;
            so.ApplyModifiedPropertiesWithoutUndo();
            ((TextMeshProUGUI)so.FindProperty("tutorialText").objectReferenceValue).transform.SetParent(player.transform);
            ((TextMeshProUGUI)so.FindProperty("tutorialTextPurple").objectReferenceValue).transform.SetParent(player.transform);
            handler.Start();
            TutorialManager.SkipNextTutorial = true;

            handler.ResetHandler();

            Assert.IsTrue(handler.HasLearnt);
            Assert.IsFalse(image.activeSelf, "no tutorial picture");
        }
    }
}
