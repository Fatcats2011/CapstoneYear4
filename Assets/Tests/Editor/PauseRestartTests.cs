using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Restart in the pause menu (PauseMenu): a local match's host pause lists Resume · Restart · Main Menu; online there's
    /// no Restart. Restart asks first: A restarts (once), B goes back to the list. The entries are built by code beside
    /// the drawn ones
    /// </summary>
    public class PauseRestartTests
    {
        readonly TestObjects objects = new TestObjects();
        PauseMenu pause;
        GameObject resume, mainMenu;
        static int restarts;
        static int returns;

        static void CountRestart() { restarts++; }
        static void CountReturn() { returns++; }

        [SetUp]
        public void SetUp()
        {
            restarts = returns = 0;
            GameObject canvas = objects.NewGameObject("Pause Menu");
            canvas.AddComponent<RectTransform>();
            GameObject host = Child(canvas, "Host Pause");
            GameObject options = Child(host, "Options");
            resume = Child(options, "Resume");
            ((RectTransform)resume.transform).anchoredPosition = new Vector2(-213, 188);
            mainMenu = Child(options, "Menu");
            ((RectTransform)mainMenu.transform).anchoredPosition = new Vector2(-251, 66);

            pause = canvas.AddComponent<PauseMenu>();
            pause.tint = Child(canvas, "Tint");
            pause.hostPauseGO = host;
            pause.subPauseGO = Child(canvas, "Sub Pause");
            pause.selector = Child(host, "Selector");
            pause.selectorObjects = new[] { resume, mainMenu };
            pause.Restart = CountRestart;
            pause.Leave = CountReturn;
        }

        [TearDown]
        public void TearDown()
        {
            GameAuthority.Role = NetworkRole.Offline;
            PingSync.Reset();
            objects.DestroyAll();
        }

        static GameObject Child(GameObject parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        [Test]
        public void Local_TheHostsPause_ResumeRestartControlsMainMenu()
        {
            pause.OnPause(PauseMenu.PauseType.Host);

            Assert.AreEqual(4, pause.selectorObjects.Length);
            Assert.AreSame(resume, pause.selectorObjects[0]);
            Assert.AreEqual("RESTART", pause.RestartEntry.text);
            Assert.AreSame(pause.RestartEntry.gameObject, pause.selectorObjects[1]);
            Assert.AreEqual("CONTROLS", pause.ControlsEntry.text);
            Assert.AreSame(pause.ControlsEntry.gameObject, pause.selectorObjects[2]);
            Assert.AreSame(mainMenu, pause.selectorObjects[3]);
            Assert.Less(pause.ControlsEntry.transform.localPosition.y, pause.RestartEntry.transform.localPosition.y);
            Assert.Less(mainMenu.transform.localPosition.y, pause.ControlsEntry.transform.localPosition.y, "Main Menu moves down to the last");
        }

        [Test]
        public void CodeEntries_LineUpWithTheDrawnArtsLeftEdge()
        {
            RectTransform drawn = (RectTransform)mainMenu.transform;
            drawn.sizeDelta = new Vector2(400f, 150f);
            drawn.localScale = Vector3.one * 0.576f; // the drawn entries are scaled images

            pause.OnPause(PauseMenu.PauseType.Host);

            float drawnLeft = -251f - 400f * 0.5f * 0.576f;
            Assert.AreEqual(drawnLeft, pause.RestartEntry.rectTransform.anchoredPosition.x, 0.01f);
            Assert.AreEqual(drawnLeft, pause.ControlsEntry.rectTransform.anchoredPosition.x, 0.01f);
        }

        [Test]
        public void Online_NoRestart()
        {
            GameAuthority.Role = NetworkRole.Host;

            pause.OnPause(PauseMenu.PauseType.Host);

            Assert.AreEqual(3, pause.selectorObjects.Length);
            Assert.AreSame(pause.ControlsEntry.gameObject, pause.selectorObjects[1]);
            Assert.AreSame(mainMenu, pause.selectorObjects[2]);
            Assert.AreEqual(66f, ((RectTransform)pause.ControlsEntry.transform).anchoredPosition.y, 0.01f, "where Main Menu was drawn");
        }

        [Test]
        public void Online_ThePauseListsEveryonesConnection()
        {
            GameAuthority.Role = NetworkRole.Client;
            pause.OnPause(PauseMenu.PauseType.Sub);
            PingSync.Names[0] = "FATCATS";
            PingSync.Names[1] = "ALEX";
            PingBoard board = PingBoard.Unknown;
            board.Set(0, 0);
            board.Set(1, 96);

            PingSync.Arrive(board);

            Assert.IsTrue(pause.Connections.gameObject.activeInHierarchy);
            StringAssert.Contains("FATCATS", pause.Connections.text);
            StringAssert.Contains("ALEX", pause.Connections.text);
            StringAssert.Contains("96MS", pause.Connections.text);
        }

        [Test]
        public void Online_ASteamNameIsNeverMarkup()
        {
            GameAuthority.Role = NetworkRole.Client;
            pause.OnPause(PauseMenu.PauseType.Sub);
            PingSync.Names[1] = "<size=400%>X";
            PingBoard board = PingBoard.Unknown;
            board.Set(1, 50);

            PingSync.Arrive(board);

            StringAssert.DoesNotContain("<SIZE", pause.Connections.text.ToUpperInvariant());
        }

        [Test]
        public void Local_NoConnectionList()
        {
            pause.OnPause(PauseMenu.PauseType.Host);

            Assert.IsTrue(pause.Connections == null || !pause.Connections.gameObject.activeSelf);
        }

        [Test]
        public void TheOtherPlayersPause_HasControls()
        {
            pause.OnPause(PauseMenu.PauseType.Sub);

            Assert.IsNotNull(pause.SubControlsEntry);
            Assert.AreEqual("CONTROLS", pause.SubControlsEntry.text);
            Assert.IsTrue(pause.SubControlsEntry.gameObject.activeInHierarchy);
        }

        [Test]
        public void OnlineAfterLocal_RestartGoesAway()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.OnPlay();
            GameAuthority.Role = NetworkRole.Client;

            pause.OnPause(PauseMenu.PauseType.Host);

            Assert.AreEqual(3, pause.selectorObjects.Length);
            Assert.IsFalse(pause.RestartEntry.gameObject.activeSelf);
        }

        [Test]
        public void Restart_AsksFirst_ThenAYes_RestartsOnce()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.ScrollMenu(true);

            pause.ConfirmMenu();
            Assert.IsTrue(pause.Confirming, "asks first");
            Assert.AreEqual(0, restarts);
            Assert.IsTrue(pause.RestartQuestion.activeSelf);

            pause.ConfirmMenu();
            pause.ConfirmMenu();
            Assert.AreEqual(1, restarts);
        }

        [Test]
        public void Restart_ThenB_BackToTheList()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.ScrollMenu(true);
            pause.ConfirmMenu();

            pause.Back();

            Assert.IsFalse(pause.Confirming);
            Assert.IsFalse(pause.RestartQuestion.activeSelf);
            pause.ConfirmMenu();
            Assert.IsTrue(pause.Confirming, "Restart is still selected: it asks again");
            Assert.AreEqual(0, restarts);
        }

        [Test]
        public void WhileAsking_UpAndDown_DoNothing()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.ScrollMenu(true);
            pause.ConfirmMenu();

            pause.ScrollMenu(true);
            pause.ConfirmMenu();

            Assert.AreEqual(1, restarts, "still on Restart");
            Assert.AreEqual(0, returns);
        }

        [Test]
        public void MainMenu_IsTheLastEntry_Locally()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.ScrollMenu(false); // up from Resume: round to the last

            pause.ConfirmMenu();

            Assert.AreEqual(1, returns);
            Assert.AreEqual(0, restarts);
        }

        [Test]
        public void ANewPause_StartsOnResume_NotAsking()
        {
            pause.OnPause(PauseMenu.PauseType.Host);
            pause.ScrollMenu(true);
            pause.ConfirmMenu();
            pause.OnPlay();

            pause.OnPause(PauseMenu.PauseType.Host);

            Assert.IsFalse(pause.Confirming);
            Assert.IsFalse(pause.RestartQuestion.activeSelf);
        }
    }
}
