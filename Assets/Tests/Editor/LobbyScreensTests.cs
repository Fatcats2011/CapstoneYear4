using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class LobbyScreensTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        GameObject[] NewObjects(string name, bool active)
        {
            GameObject[] created = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < created.Length; i++)
            {
                created[i] = objects.NewGameObject(name + " " + i);
                created[i].transform.position = new Vector3(i, 0f, 0f);
                created[i].SetActive(active);
            }
            return created;
        }

        /// <summary>
        /// Players in slots 0 and 2 (the player in slot 1 left)
        /// </summary>
        PlayerRoster RosterWithAGap()
        {
            PlayerRoster roster = new PlayerRoster();
            roster.JoinLocal(objects.Add<PlayerInput>());
            PlayerInput leaver = objects.Add<PlayerInput>();
            roster.JoinLocal(leaver);
            roster.JoinLocal(objects.Add<PlayerInput>());
            roster.Leave(leaver);
            return roster;
        }

        [Test]
        public void PlayerSelect_HidesTheJoinPromptOfEveryTakenSlotOnly()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] joinPrompts = NewObjects("Join Prompt", true);
            Reflect.SetField(canvas, "pressButtonTexts", joinPrompts);

            canvas.TogglePressButtonOnAllTexts(RosterWithAGap());

            Assert.IsFalse(joinPrompts[0].activeSelf);
            Assert.IsTrue(joinPrompts[1].activeSelf);
            Assert.IsFalse(joinPrompts[2].activeSelf);
            Assert.IsTrue(joinPrompts[3].activeSelf);
        }

        [Test]
        public void LoadingScreen_ShowsOneConfirmButtonPerPlayer_PackedInSlotOrder()
        {
            LoadingScreenManager loading = objects.Add<LoadingScreenManager>();
            GameObject[] buttons = NewObjects("Button", false);
            GameObject[] positions = NewObjects("Button Position", true);
            GameObject confirmText = objects.NewGameObject("Confirm Text");
            confirmText.SetActive(false);
            Reflect.SetField(loading, "ButtonGameobjects", buttons);
            Reflect.SetField(loading, "buttonPositions", System.Array.ConvertAll(positions, p => p.transform));
            Reflect.SetField(loading, "textConfirm", confirmText);

            loading.InitalizeButtonGameobjects(RosterWithAGap());

            Assert.IsTrue(buttons[0].activeSelf);
            Assert.IsFalse(buttons[1].activeSelf);
            Assert.IsTrue(buttons[2].activeSelf);
            Assert.IsFalse(buttons[3].activeSelf);
            Assert.AreEqual(positions[0].transform.position, buttons[0].transform.position);
            Assert.AreEqual(positions[1].transform.position, buttons[2].transform.position);
            Assert.IsTrue(confirmText.activeSelf);
        }
    }
}
