using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The Dispatch room, its own scene loaded alongside the menu while the board is up (DispatchRoomLoader), and what it
    /// tells the game: its seats, its ticket anchors, its camera, and its podium rims in each seat's company colour
    /// </summary>
    public class DispatchRoomTests
    {
        readonly TestObjects objects = new TestObjects();

        // Scene loads that finish when the test says so
        class FakeScenes : IRoomScenes
        {
            public bool Buildable = true;
            public bool Loaded;
            public int Loads, Unloads;
            public Action Pending;

            public bool CanLoad(string scene) { return Buildable; }
            public bool IsLoaded(string scene) { return Loaded; }

            public void Load(string scene, Action done)
            {
                Loads++;
                Pending = () => { Loaded = true; done(); };
            }

            public void Unload(string scene, Action done)
            {
                Unloads++;
                Pending = () => { Loaded = false; done(); };
            }

            public void Finish()
            {
                Action p = Pending;
                Pending = null;
                p();
            }
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        [Test]
        public void Loader_Wanted_LoadsOnce()
        {
            FakeScenes scenes = new FakeScenes();
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            loader.SetWanted(true);
            loader.SetWanted(true);
            scenes.Finish();
            loader.SetWanted(true);

            Assert.AreEqual(1, scenes.Loads);
            Assert.IsTrue(scenes.Loaded);
            Assert.AreEqual("Multiplayerlobby", DispatchRoomLoader.SCENE);
        }

        [Test]
        public void Loader_UnwantedWhileLoading_UnloadsOnceLoaded()
        {
            FakeScenes scenes = new FakeScenes();
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            loader.SetWanted(true);
            loader.SetWanted(false);
            Assert.AreEqual(0, scenes.Unloads, "nothing is unloaded mid-load");
            scenes.Finish();

            Assert.AreEqual(1, scenes.Unloads);
            scenes.Finish();
            Assert.IsFalse(scenes.Loaded);
            Assert.IsFalse(loader.Busy);
        }

        [Test]
        public void Loader_WantedAgainWhileUnloading_LoadsAgain()
        {
            FakeScenes scenes = new FakeScenes();
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);
            loader.SetWanted(true);
            scenes.Finish();

            loader.SetWanted(false);
            loader.SetWanted(true);
            scenes.Finish();

            Assert.AreEqual(2, scenes.Loads);
            scenes.Finish();
            Assert.IsTrue(scenes.Loaded);
        }

        [Test]
        public void Loader_NotLoaded_UnwantedDoesNothing()
        {
            // A match's scene load (Single) took the room with it: there's nothing to unload
            FakeScenes scenes = new FakeScenes();
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            loader.SetWanted(false);

            Assert.AreEqual(0, scenes.Unloads);
            Assert.IsFalse(loader.Busy);
        }

        [Test]
        public void Loader_SceneNotInTheBuild_LoadsNothing()
        {
            FakeScenes scenes = new FakeScenes { Buildable = false };
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            LogAssertNoThrow(() => loader.SetWanted(true));

            Assert.AreEqual(0, scenes.Loads);
            Assert.IsFalse(loader.Busy);
        }

        [Test]
        public void Loader_AlreadyOpen_IsntLoadedTwice_AndIsUnloaded()
        {
            FakeScenes scenes = new FakeScenes { Loaded = true };
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            loader.SetWanted(true);
            Assert.AreEqual(0, scenes.Loads);
            loader.SetWanted(false);
            Assert.AreEqual(1, scenes.Unloads);
        }

        [Test]
        public void Loader_LeavingPlayOrQuitting_UnloadsNothing()
        {
            // Unloading a scene while Play Mode is being torn down crashes the editor (going offline on Stop hides the
            // board for one last frame). Unity unloads every scene then anyway
            FakeScenes scenes = new FakeScenes();
            bool leaving = false;
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes, () => leaving);
            loader.SetWanted(true);
            scenes.Finish();

            leaving = true;
            loader.SetWanted(false);

            Assert.AreEqual(0, scenes.Unloads);
            Assert.IsFalse(loader.Busy);
        }

        [Test]
        public void Leaving_WhileEditing_IsFalse_EvenAfterAPlaySessionQuit()
        {
            // The editor raises Application.quitting each time Play stops: that mustn't make editing look like leaving
            FieldInfo quitting = typeof(UnityRoomScenes).GetField("quitting", BindingFlags.Static | BindingFlags.NonPublic);
            object was = quitting.GetValue(null);
            quitting.SetValue(null, true);
            try
            {
                Assert.IsFalse(UnityRoomScenes.Leaving());
            }
            finally
            {
                quitting.SetValue(null, was);
            }
        }

        [Test]
        public void Loader_ALoadThatEndsWithoutTheRoom_IsntRetried_UntilUnwanted()
        {
            FakeScenes scenes = new FakeScenes();
            DispatchRoomLoader loader = new DispatchRoomLoader(scenes);

            loader.SetWanted(true);
            scenes.Pending = null;
            Reflect.Invoke(loader, "Done"); // Unity refused it: done, nothing loaded
            loader.SetWanted(true);
            Assert.AreEqual(1, scenes.Loads);

            loader.SetWanted(false);
            loader.SetWanted(true);
            Assert.AreEqual(2, scenes.Loads);
        }

        static void LogAssertNoThrow(Action action)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { action(); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void RoomSeats_WiredComplete_WinsOverTheLoadedRoom()
        {
            GameObject[] wired = Four("Wired");
            GameObject[] loaded = Four("Loaded");

            Assert.AreSame(wired, DispatchRules.RoomSeats(wired, loaded));
        }

        [Test]
        public void RoomSeats_WiredMissingOne_UsesTheLoadedRoom()
        {
            GameObject[] wired = Four("Wired");
            wired[2] = null;
            GameObject[] loaded = Four("Loaded");

            Assert.AreSame(loaded, DispatchRules.RoomSeats(wired, loaded));
            Assert.IsNull(DispatchRules.RoomSeats(new GameObject[0], null));
        }

        [Test]
        public void ViewportToCanvas_CentreIsZero_CornersAreHalfTheSize()
        {
            Vector2 size = new Vector2(1920, 1080);

            Assert.AreEqual(Vector2.zero, DispatchRules.ViewportToCanvas(new Vector3(0.5f, 0.5f, 5), size));
            Assert.AreEqual(new Vector2(-960, 540), DispatchRules.ViewportToCanvas(new Vector3(0, 1, 5), size));
            Assert.AreEqual(new Vector2(480, -270), DispatchRules.ViewportToCanvas(new Vector3(0.75f, 0.25f, 5), size));
        }

        [Test]
        public void InView_BehindTheCamera_IsNot()
        {
            Assert.IsTrue(DispatchRules.InView(new Vector3(0.5f, 0.5f, 3)));
            Assert.IsFalse(DispatchRules.InView(new Vector3(0.5f, 0.5f, -3)));
        }

        [Test]
        public void Room_Register_IsCurrent_UntilUnregistered()
        {
            DispatchRoom room = objects.Add<DispatchRoom>();
            DispatchRoom other = objects.Add<DispatchRoom>();

            room.Register();
            Assert.AreSame(room, DispatchRoom.Current);
            other.Unregister();
            Assert.AreSame(room, DispatchRoom.Current, "another room going doesn't clear this one");
            room.Unregister();
            Assert.IsNull(DispatchRoom.Current);
        }

        [Test]
        public void Room_Camera_OnOnlyWhileTheBoardShows()
        {
            // The room's scene can be open from the start (the editor): its camera mustn't cover the title screen
            DispatchRoom room = objects.Add<DispatchRoom>();
            Camera cam = objects.Add<Camera>();
            Set(room, "roomCamera", cam);

            room.ShowCamera(false);
            Assert.IsFalse(cam.enabled);
            room.ShowCamera(true);
            Assert.IsTrue(cam.enabled);
        }

        [Test]
        public void Room_Dim_DarkensTheBackdrop_KeepingEachMaterialsColour()
        {
            // The board and walls sit back (the mockup's 75%): the spotlit podiums stand out
            DispatchRoom room = objects.Add<DispatchRoom>();
            MeshRenderer wall = objects.Add<MeshRenderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.5f, 0.4f, 0.3f, 1));
            wall.sharedMaterial = mat;
            Set(room, "dimmed", new Renderer[] { wall, null });

            room.Dim();

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            wall.GetPropertyBlock(block);
            AssertColour(new Color(0.5f * DispatchRoom.BACKDROP, 0.4f * DispatchRoom.BACKDROP, 0.3f * DispatchRoom.BACKDROP, 1),
                block.GetColor("_BaseColor"), "the wall");
            UnityEngine.Object.DestroyImmediate(mat);
        }

        [Test]
        public void Room_Tint_ColoursEachRimInItsSeatsCompany()
        {
            DispatchRoom room = objects.Add<DispatchRoom>();
            Renderer[] rims = new Renderer[Constants.MAX_PLAYERS];
            CompanyInformation[] companies = new CompanyInformation[Constants.MAX_PLAYERS];
            for (int i = 0; i < rims.Length; i++)
            {
                rims[i] = objects.Add<MeshRenderer>();
                companies[i] = ScriptableObject.CreateInstance<CompanyInformation>();
                companies[i].companyColor = new Color(0.1f * i, 0.5f, 0.2f, 1);
            }
            rims[3] = null; // a missing rim is skipped
            Set(room, "rims", rims);

            room.Tint(i => companies[i]);

            for (int i = 0; i < 3; i++)
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                rims[i].GetPropertyBlock(block);
                AssertColour(companies[i].companyColor * DispatchRoom.RIM_GLOW, block.GetColor("_EmissionColor"), "rim " + i);
                AssertColour(companies[i].companyColor, block.GetColor("_BaseColor"), "rim " + i);
            }
            foreach (CompanyInformation c in companies)
                UnityEngine.Object.DestroyImmediate(c);
        }

        [Test]
        public void Canvas_PlayerViews_HiddenOnDispatch_ExceptTheOneCustomizing_ThenRestored()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] views = Four("Player Cam");
            views[3].SetActive(false); // as the scene had it
            canvas.playerViews = views;

            canvas.SetPlayerViews(true, 1);
            CollectionAssert.AreEqual(new[] { false, true, false, false }, Active(views));

            canvas.SetPlayerViews(true, -1);
            CollectionAssert.AreEqual(new[] { false, false, false, false }, Active(views));

            canvas.SetPlayerViews(false, -1);
            CollectionAssert.AreEqual(new[] { true, true, true, false }, Active(views), "back as they were");
        }

        [Test]
        public void Canvas_DispatchHides_TheOldLobbysBackgrounds_ThenRestoresThem()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject[] old = Four("Old Lobby Part");
            old[2].SetActive(false); // as the scene had it
            canvas.dispatchHides = old;

            canvas.SetDispatchHidden(true);
            canvas.SetDispatchHidden(true);
            CollectionAssert.AreEqual(new[] { false, false, false, false }, Active(old));

            canvas.SetDispatchHidden(false);
            CollectionAssert.AreEqual(new[] { true, true, false, true }, Active(old), "back as they were");
            Assert.DoesNotThrow(() => canvas.SetDispatchHidden(false));
        }

        [Test]
        public void Canvas_DispatchHides_OtherCanvasesDrawing_ThenRestoresIt()
        {
            // The title screen's canvas sits under player select's: with the old lobby hidden it would cover the room
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            Canvas title = objects.Add<Canvas>();
            Canvas off = objects.Add<Canvas>();
            off.enabled = false; // as the scene had it
            canvas.dispatchHideCanvases = new[] { title, off };

            canvas.SetDispatchHidden(true);
            Assert.IsFalse(title.enabled);
            Assert.IsTrue(title.gameObject.activeSelf, "only its drawing: its scripts keep running");

            canvas.SetDispatchHidden(false);
            Assert.IsTrue(title.enabled);
            Assert.IsFalse(off.enabled, "back as it was");
        }

        [Test]
        public void Canvas_PlayerViews_NoneWired_DoesNothing()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();

            Assert.DoesNotThrow(() => canvas.SetPlayerViews(true, 0));
            Assert.DoesNotThrow(() => canvas.SetPlayerViews(false, -1));
        }

        [Test]
        public void SceneManager_AnAdditiveLoad_IsntASceneChange()
        {
            // The Dispatch room loads alongside the menu: the match flow mustn't take it for the scene it waits for
            SceneManager flow = objects.Add<SceneManager>();
            int raised = 0;
            flow.SceneUp += () => raised++;

            flow.RaiseSceneUp(default(Scene), LoadSceneMode.Additive);
            Assert.AreEqual(0, raised);
            flow.RaiseSceneUp(default(Scene), LoadSceneMode.Single);
            Assert.AreEqual(1, raised);
        }

        [Test]
        public void Board_Showing_LoadsTheRoom_Hiding_UnloadsIt()
        {
            DispatchBoard board = objects.Add<DispatchBoard>();
            FakeScenes scenes = new FakeScenes();
            board.RoomScenes = scenes;

            Reflect.Invoke(board, "SetShowing", true);
            Assert.AreEqual(1, scenes.Loads);
            scenes.Finish();
            Reflect.Invoke(board, "SetShowing", false);
            Assert.AreEqual(1, scenes.Unloads);
        }

        [Test]
        public void Board_PlaceTickets_PutsEachTicketOverItsAnchor()
        {
            DispatchBoard board = objects.Add<DispatchBoard>();
            RectTransform area = objects.NewGameObject("Tickets").AddComponent<RectTransform>();
            area.sizeDelta = new Vector2(1920, 1080);
            RectTransform ticket = new GameObject("Ticket", typeof(RectTransform)).GetComponent<RectTransform>();
            ticket.SetParent(area, false);

            Camera cam = objects.Add<Camera>();
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            Transform ahead = objects.NewGameObject("Ahead").transform;
            ahead.position = new Vector3(0, 0, 10);
            Transform behind = objects.NewGameObject("Behind").transform;
            behind.position = new Vector3(0, 0, -10);
            ticket.anchoredPosition = new Vector2(123, 45);

            board.PlaceTickets(cam, new[] { behind }, new[] { ticket });
            Assert.AreEqual(new Vector2(123, 45), ticket.anchoredPosition, "an anchor behind the camera moves nothing");

            board.PlaceTickets(cam, new[] { ahead }, new[] { ticket });
            Assert.That(ticket.anchoredPosition.magnitude, Is.LessThan(0.01f), "straight ahead is the centre");
        }

        [Test]
        public void Board_PlaceTickets_SizesEachTicketToTheGapBetweenAnchors()
        {
            DispatchBoard board = objects.Add<DispatchBoard>();
            RectTransform area = objects.NewGameObject("Tickets").AddComponent<RectTransform>();
            area.sizeDelta = new Vector2(1920, 1080);
            RectTransform[] tickets = new RectTransform[2];
            Transform[] anchors = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                tickets[i] = new GameObject("Ticket", typeof(RectTransform)).GetComponent<RectTransform>();
                tickets[i].SetParent(area, false);
                tickets[i].sizeDelta = new Vector2(300, 375);
                anchors[i] = objects.NewGameObject("Anchor").transform;
                anchors[i].position = new Vector3(i == 0 ? -1 : 1, 0, 10);
            }
            Camera cam = objects.Add<Camera>();
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;

            board.PlaceTickets(cam, anchors, tickets);

            float gap = tickets[1].anchoredPosition.x - tickets[0].anchoredPosition.x;
            Assert.That(gap, Is.GreaterThan(0));
            for (int i = 0; i < 2; i++)
                Assert.AreEqual(gap * DispatchBoard.TICKET_FILL, 300 * tickets[i].localScale.x, 0.5f, "ticket " + i);
        }

        [Test]
        public void Board_ShowAvatars_OnlyForASeatedPlayerWithAPicture()
        {
            DispatchBoard board = objects.Add<DispatchBoard>();
            RawImage[] pictures = new RawImage[Constants.MAX_PLAYERS];
            RectTransform[] frames = new RectTransform[Constants.MAX_PLAYERS];
            for (int i = 0; i < pictures.Length; i++)
            {
                frames[i] = objects.NewGameObject("Avatar").AddComponent<RectTransform>();
                pictures[i] = new GameObject("Picture", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                pictures[i].transform.SetParent(frames[i], false);
            }
            Set(board, "avatarFrames", frames);
            Set(board, "avatarPictures", pictures);
            Texture2D picture = new Texture2D(2, 2);
            TicketState[] seats = new TicketState[Constants.MAX_PLAYERS];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = new TicketState { Seat = i };
            seats[0] = new TicketState { Seat = 0, Taken = true, SteamId = 7 };   // a picture
            seats[1] = new TicketState { Seat = 1, Taken = true, SteamId = 8 };   // still loading
            seats[2] = new TicketState { Seat = 2, Taken = true };                // no Steam

            board.ShowAvatars(seats, id => id == 7 ? picture : null);

            Assert.IsTrue(frames[0].gameObject.activeSelf);
            Assert.AreSame(picture, pictures[0].texture);
            Assert.IsFalse(frames[1].gameObject.activeSelf, "loading: nothing yet");
            Assert.IsFalse(frames[2].gameObject.activeSelf, "no Steam ID");
            Assert.IsFalse(frames[3].gameObject.activeSelf, "a free seat");
            UnityEngine.Object.DestroyImmediate(picture);
        }

        [Test]
        public void Board_PlaceAvatars_CentredUnderTheirAnchor()
        {
            DispatchBoard board = objects.Add<DispatchBoard>();
            RectTransform area = objects.NewGameObject("Area").AddComponent<RectTransform>();
            area.sizeDelta = new Vector2(1920, 1080);
            RectTransform frame = new GameObject("Avatar", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(area, false);
            Camera cam = objects.Add<Camera>();
            cam.transform.position = Vector3.zero;
            cam.transform.rotation = Quaternion.identity;
            Transform ahead = objects.NewGameObject("Ahead").transform;
            ahead.position = new Vector3(0, 0, 10);

            board.PlaceAvatars(cam, new[] { ahead }, new[] { frame });

            Assert.AreEqual(0f, frame.anchoredPosition.x, 0.01f, "centred under it");
            Assert.AreEqual(-DispatchBoard.AVATAR_DROP, frame.anchoredPosition.y, 0.01f, "just below it");
            Assert.AreEqual(new Vector2(0.5f, 1), frame.pivot, "hangs from its top edge");
        }

        GameObject[] Four(string name)
        {
            GameObject[] four = new GameObject[Constants.MAX_PLAYERS];
            for (int i = 0; i < four.Length; i++)
                four[i] = objects.NewGameObject(name + " " + (i + 1));
            return four;
        }

        static void AssertColour(Color expected, Color actual, string message)
        {
            for (int c = 0; c < 4; c++)
                Assert.AreEqual(expected[c], actual[c], 1e-4f, message);
        }

        static bool[] Active(GameObject[] all)
        {
            List<bool> on = new List<bool>();
            foreach (GameObject g in all)
                on.Add(g.activeSelf);
            return on.ToArray();
        }

        static void Set(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            info.SetValue(target, value);
        }
    }
}
