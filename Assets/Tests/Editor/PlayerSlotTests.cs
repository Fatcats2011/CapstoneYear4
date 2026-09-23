using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    public class PlayerSlotTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<PlayerSelectCanvas>(null);
            Reflect.SetSingleton<ScoreManager>(null);
            objects.DestroyAll();
        }

        /// <summary>
        /// The lobby's "press a button to join" prompts, one per slot, all hidden
        /// </summary>
        GameObject[] SetUpLobby()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] joinPrompts = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < joinPrompts.Length; i++)
            {
                joinPrompts[i] = objects.NewGameObject("Join Prompt " + i);
                joinPrompts[i].SetActive(false);
            }
            Reflect.SetField(canvas, "pressButtonTexts", joinPrompts);
            Reflect.SetSingleton(canvas);
            Reflect.SetSingleton(objects.Add<ScoreManager>());
            return joinPrompts;
        }

        PlayerInput NewPlayerWithCamera(string name)
        {
            PlayerInput player = objects.NewGameObject(name).AddComponent<PlayerInput>();
            player.camera = objects.Add<Camera>();
            return player;
        }

        [Test]
        public void PlayerCount_IsHowManyPlayersAreInTheRoster()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinRemote(objects.NewGameObject("Online player"), 2);

            Assert.AreEqual(2, instantiate.PlayerCount);
        }

        [Test]
        public void RemovePlayerRef_PlayerNotInLobby_LeavesPlayerOnesJoinPromptAlone()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] joinPrompts = SetUpLobby();
            PlayerInput stranger = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(stranger);

            Assert.IsFalse(joinPrompts[0].activeSelf);
        }

        [Test]
        public void RemovePlayerRef_JoinedPlayer_FreesTheirSlotAndGivesTheOthersTheScreen()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            GameObject[] joinPrompts = SetUpLobby();
            PlayerInput first = NewPlayerWithCamera("P1");
            PlayerInput second = NewPlayerWithCamera("P2");
            instantiate.Roster.JoinLocal(first);
            instantiate.Roster.JoinLocal(second);
            first.camera.rect = new Rect(0.25f, 0.5f, 0.5f, 0.5f);
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(second);

            Assert.AreEqual(1, instantiate.PlayerCount);
            Assert.IsNull(instantiate.Roster[1]);
            Assert.IsTrue(joinPrompts[1].activeSelf);
            Assert.AreEqual(new Rect(0, 0, 1, 1), first.camera.rect);
        }

        [Test]
        public void ClearPlayerArray_EmptiesTheRoster()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            instantiate.Roster.JoinLocal(objects.Add<PlayerInput>());
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.ClearPlayerArray();

            Assert.AreEqual(0, instantiate.PlayerCount);
            Assert.IsEmpty(instantiate.Roster.Players);
        }
    }
}
