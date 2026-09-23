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

        [Test]
        public void RemoveFromPlayerArray_PlayerNotInLobby_ReturnsMinusOne()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput stranger = objects.Add<PlayerInput>();

            Assert.AreEqual(-1, instantiate.RemoveFromPlayerArray(stranger));
        }

        [Test]
        public void RemoveFromPlayerArray_JoinedPlayer_FreesOnlyTheirSlot()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
            PlayerInput first = objects.Add<PlayerInput>();
            PlayerInput second = objects.Add<PlayerInput>();
            instantiate.AddToPlayerArray(first);
            instantiate.AddToPlayerArray(second);

            Assert.AreEqual(1, instantiate.RemoveFromPlayerArray(second));
            Assert.AreSame(first, instantiate.PlayerInputs[0]);
            Assert.IsNull(instantiate.PlayerInputs[1]);
        }

        [Test]
        public void RemovePlayerRef_PlayerNotInLobby_LeavesPlayerOnesJoinPromptAlone()
        {
            PlayerInstantiate instantiate = objects.Add<PlayerInstantiate>();
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
            PlayerInput stranger = objects.Add<PlayerInput>();
            LogAssert.Expect(LogType.Error, new Regex("Destroy may not be called from edit mode"));

            instantiate.RemovePlayerRef(stranger);

            Assert.IsFalse(joinPrompts[0].activeSelf);
        }
    }
}
