using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// The editor's Dispatch builder (Tools → Dead on Arrival → Dispatch): it sets up the room scene from the model's
    /// named empties, and builds and wires the board's UI in the menu scene
    /// </summary>
    public class DispatchLobbyBuilderTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        // The room as the scene has it: the model (its empties and rims, and its own camera) and the scene's Main Camera
        GameObject[] Room(bool modelCamera)
        {
            // Laid out as the model comes into Unity: seats 12 apart along x, the board behind them and above
            GameObject model = objects.NewGameObject("MultiplayerLobby");
            Mesh(Child(model, "Depot_Floor"));
            for (int i = 4; i >= 1; i--) // out of order: the builder sorts by name
            {
                float x = -18 + 12 * (i - 1);
                Child(model, "Seat" + i + "_Spawn").transform.position = new Vector3(x, 1.96f, 0);
                Child(model, "Seat" + i + "_Ticket").transform.position = new Vector3(x, 17.5f, 13.65f);
                GameObject podium = Child(model, "Podium" + i);
                Mesh(podium);
                Mesh(Child(podium, "Podium" + i + "_Base"));
                Child(podium, "Podium" + i + "_Rim").AddComponent<MeshRenderer>();
            }
            if (modelCamera)
                Child(model, "Camera").AddComponent<Camera>();

            GameObject main = objects.NewGameObject("Main Camera");
            main.AddComponent<Camera>();
            main.AddComponent<AudioListener>();
            return new[] { model, main };
        }

        static void Mesh(GameObject go)
        {
            go.AddComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            go.AddComponent<MeshRenderer>();
        }

        static GameObject Child(GameObject parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent.transform);
            return child;
        }

        [Test]
        public void SetUpRoom_WiresSeatsAnchorsAndRims_InSeatOrder()
        {
            GameObject[] roots = Room(true);

            DispatchRoom room = DispatchLobbyBuilder.SetUpRoom(roots);

            SerializedObject so = new SerializedObject(room);
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
            {
                Assert.AreEqual("Seat" + (i + 1) + "_Spawn", so.FindProperty("seats").GetArrayElementAtIndex(i).objectReferenceValue.name);
                Assert.AreEqual("Seat" + (i + 1) + "_Ticket", so.FindProperty("ticketAnchors").GetArrayElementAtIndex(i).objectReferenceValue.name);
                Assert.AreEqual("Podium" + (i + 1) + "_Rim", so.FindProperty("rims").GetArrayElementAtIndex(i).objectReferenceValue.name);
            }
            Assert.AreSame(roots[0], room.gameObject, "on the model's root");
        }

        [Test]
        public void SetUpRoom_MakesItsOwnCamera_FramingEverySeatAndTicket_Seat1OnTheLeft()
        {
            // The model's own camera (Blender's default one, aimed at the origin) and the scene's are both turned off
            GameObject[] roots = Room(true);

            DispatchRoom room = DispatchLobbyBuilder.SetUpRoom(roots);

            Camera cam = room.RoomCamera;
            Assert.AreEqual(DispatchLobbyBuilder.ROOM_CAMERA, cam.name);
            Assert.AreSame(roots[0].transform, cam.transform.parent, "moves with the room");
            cam.aspect = 16f / 9f;
            for (int i = 1; i <= 4; i++)
            {
                foreach (string point in new[] { "Seat" + i + "_Spawn", "Seat" + i + "_Ticket" })
                {
                    Vector3 v = cam.WorldToViewportPoint(roots[0].transform.Find(point).position);
                    Assert.That(v.z, Is.GreaterThan(0), point);
                    Assert.That(v.x, Is.InRange(0.05f, 0.95f), point);
                    // Between the bottom prompts and the top row (wordmark, ready counter)
                    Assert.That(v.y, Is.InRange(DispatchLobbyBuilder.FRAME_BOTTOM, DispatchLobbyBuilder.FRAME_TOP), point);
                }
            }
            float left = cam.WorldToViewportPoint(roots[0].transform.Find("Seat1_Spawn").position).x;
            float right = cam.WorldToViewportPoint(roots[0].transform.Find("Seat4_Spawn").position).x;
            Assert.That(left, Is.LessThan(right), "seat 1 on the left");

            Assert.IsTrue(CameraBudget.Covers(false, cam.rect, cam.clearFlags, cam.depth), "draws over the players' views");
            Assert.IsFalse(roots[0].transform.Find("Camera").GetComponent<Camera>().enabled, "the model's camera is off");
            Assert.IsFalse(roots[1].GetComponent<Camera>().enabled, "the scene's camera is off");
            Assert.IsNull(roots[1].GetComponent<AudioListener>(), "the menu has the listener");
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("MenuUI")));
            Assert.AreEqual(0, cam.cullingMask & (1 << LayerMask.NameToLayer("Player1Cam")));
        }

        [Test]
        public void SetUpRoom_ScalesTheRoomToTheScooters_Once()
        {
            GameObject[] roots = Room(false);

            DispatchLobbyBuilder.SetUpRoom(roots);
            DispatchLobbyBuilder.SetUpRoom(roots);

            Assert.AreEqual(Vector3.one * DispatchLobbyBuilder.ROOM_SCALE, roots[0].transform.localScale);
        }

        [Test]
        public void SetUpRoom_DimsEverythingButThePodiums()
        {
            GameObject[] roots = Room(false);

            DispatchRoom room = DispatchLobbyBuilder.SetUpRoom(roots);

            SerializedProperty dimmed = new SerializedObject(room).FindProperty("dimmed");
            List<string> names = new List<string>();
            for (int i = 0; i < dimmed.arraySize; i++)
                names.Add(dimmed.GetArrayElementAtIndex(i).objectReferenceValue.name);
            CollectionAssert.Contains(names, "Depot_Floor");
            Assert.IsFalse(names.Any(n => n.StartsWith("Podium")), "the podiums stay bright");
        }

        [Test]
        public void SetUpRoom_HidesBlendersDefaultCubeAndLight()
        {
            GameObject[] roots = Room(true);
            GameObject cube = Child(roots[0], "Cube");
            GameObject light = Child(roots[0], "Light");
            light.AddComponent<Light>();

            DispatchLobbyBuilder.SetUpRoom(roots);

            Assert.IsFalse(cube.activeSelf, "the default cube");
            Assert.IsFalse(light.activeSelf, "the default light");
        }

        [Test]
        public void SetUpRoom_SpotlightsEachPodium_FromAbove_Once()
        {
            GameObject[] roots = Room(false);

            DispatchLobbyBuilder.SetUpRoom(roots);
            DispatchLobbyBuilder.SetUpRoom(roots);

            Light[] spots = roots[0].GetComponentsInChildren<Light>(true).Where(l => l.type == LightType.Spot).ToArray();
            Assert.AreEqual(Constants.MAX_PLAYERS, spots.Length, "one per podium, not doubled by a second run");
            for (int i = 1; i <= 4; i++)
            {
                Transform seat = roots[0].transform.Find("Seat" + i + "_Spawn");
                Light spot = spots.Single(l => l.name == "Seat" + i + "_Spot");
                Assert.That(spot.transform.position.y, Is.GreaterThan(seat.position.y + 3), "above the podium");
                Vector3 toSeat = (seat.position - spot.transform.position).normalized;
                Assert.That(Vector3.Dot(spot.transform.forward, toSeat), Is.GreaterThan(0.99f), "aimed at the seat");
                Assert.That(spot.range, Is.GreaterThan(Vector3.Distance(seat.position, spot.transform.position)), "reaches it");
            }
        }

        [Test]
        public void SetUpRoom_DimsTheScenesDirectionalLight_SoTheSpotsStandOut()
        {
            GameObject[] roots = Room(false);
            GameObject sun = objects.NewGameObject("Directional Light");
            Light light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;

            DispatchLobbyBuilder.SetUpRoom(roots.Concat(new[] { sun }).ToArray());

            Assert.AreEqual(DispatchLobbyBuilder.ROOM_FILL, light.intensity, 1e-4f);
        }

        [Test]
        public void SetUpRoom_GivesTheFloorAndPodiumsColliders()
        {
            // Without them the scooters fall through the room
            GameObject[] roots = Room(false);

            DispatchLobbyBuilder.SetUpRoom(roots);
            DispatchLobbyBuilder.SetUpRoom(roots);

            Transform model = roots[0].transform;
            Assert.AreEqual(1, model.Find("Depot_Floor").GetComponents<Collider>().Length);
            for (int i = 1; i <= 4; i++)
            {
                Assert.AreEqual(1, model.Find("Podium" + i).GetComponents<Collider>().Length, "podium " + i);
                Assert.AreEqual(1, model.Find("Podium" + i + "/Podium" + i + "_Base").GetComponents<Collider>().Length, "base " + i);
            }
        }

        [Test]
        public void SetUpRoom_Twice_KeepsOneRoom()
        {
            GameObject[] roots = Room(true);

            DispatchLobbyBuilder.SetUpRoom(roots);
            DispatchLobbyBuilder.SetUpRoom(roots);

            Assert.AreEqual(1, roots[0].GetComponents<DispatchRoom>().Length);
            Assert.AreEqual(1, roots[0].GetComponentsInChildren<Camera>(true).Count(c => c.name == DispatchLobbyBuilder.ROOM_CAMERA));
        }

        [Test]
        public void SetUpRoom_MissingASeat_Throws()
        {
            GameObject[] roots = Room(true);
            Object.DestroyImmediate(roots[0].transform.Find("Seat3_Spawn").gameObject);

            Assert.Throws<System.InvalidOperationException>(() => DispatchLobbyBuilder.SetUpRoom(roots));
        }

        [Test]
        public void BuildBoard_WiresEveryField_RootAndBannerOff()
        {
            DispatchBoard board = DispatchLobbyBuilder.BuildBoard();
            objects.Keep(board.transform.root.gameObject);

            SerializedObject so = new SerializedObject(board);
            foreach (string field in new[] { "root", "readyLine", "banner", "matchmakingLine", "fillPrompt" })
                Assert.IsNotNull(so.FindProperty(field).objectReferenceValue, field);
            Assert.AreEqual(Constants.MAX_PLAYERS, so.FindProperty("tickets").arraySize);
            Assert.IsFalse(((GameObject)so.FindProperty("root").objectReferenceValue).activeSelf);
            Assert.IsFalse(((GameObject)so.FindProperty("banner").objectReferenceValue).activeSelf);
            Assert.AreNotSame(board.gameObject, so.FindProperty("root").objectReferenceValue, "hiding root mustn't stop the board's Update");

            Canvas canvas = board.GetComponent<Canvas>();
            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);

            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
            {
                TicketView ticket = (TicketView)so.FindProperty("tickets").GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.IsNotNull(ticket, "ticket " + i);
                SerializedObject t = new SerializedObject(ticket);
                foreach (string field in new[] { "header", "company", "playerName", "colourName", "hatName", "colourSwatch",
                    "hatIcon", "filled", "hiring", "stamp", "gettingReady", "mineMarker", "highlight" })
                    Assert.IsNotNull(t.FindProperty(field).objectReferenceValue, "ticket " + i + " " + field);
                Assert.IsNotNull(((GameObject)t.FindProperty("stamp").objectReferenceValue).GetComponentInChildren<TMP_Text>(true),
                    "the stamp's word");
            }
        }

        [Test]
        public void BuildBoard_UsesTheDispatchArt()
        {
            DispatchBoard board = DispatchLobbyBuilder.BuildBoard();
            objects.Keep(board.transform.root.gameObject);

            bool paper = false, wordmark = false;
            foreach (Image image in board.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite == null)
                    continue;
                paper |= image.sprite.name == "Ticket_Paper";
                wordmark |= image.sprite.name == "Dispatch_Wordmark";
            }
            Assert.IsTrue(paper, "ticket paper");
            Assert.IsTrue(wordmark, "wordmark");
        }

        [Test]
        public void FlashController_HasTheFlashTrigger()
        {
            AnimatorController controller = DispatchLobbyBuilder.FlashController();

            Assert.IsNotNull(controller);
            Assert.IsTrue(System.Array.Exists(controller.parameters,
                p => p.name == "Flash" && p.type == AnimatorControllerParameterType.Trigger));
        }

        [Test]
        public void WirePlayerViews_FindsThePlayerCams_InSeatOrder()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            for (int i = 4; i >= 1; i--)
                Child(canvas.gameObject, "Player " + i + " Cam");

            DispatchLobbyBuilder.WirePlayerViews(canvas);

            Assert.AreEqual(Constants.MAX_PLAYERS, canvas.playerViews.Length);
            for (int i = 0; i < Constants.MAX_PLAYERS; i++)
                Assert.AreEqual("Player " + (i + 1) + " Cam", canvas.playerViews[i].name);
        }
    
        [Test]
        public void WirePlayerViews_AlsoWiresTheTitleScreensCanvasToHide()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            GameObject title = objects.NewGameObject("Main Menu Canvas");
            title.AddComponent<Canvas>();

            DispatchLobbyBuilder.WirePlayerViews(canvas);

            CollectionAssert.AreEqual(new[] { title.GetComponent<Canvas>() }, canvas.dispatchHideCanvases);
        }

        [Test]
        public void WirePlayerViews_AlsoWiresTheOldLobbyToHide()
        {
            PlayerSelectCanvas canvas = objects.Add<PlayerSelectCanvas>();
            foreach (string name in new[] { "Player Backgrounds", "Player Icons", "Dividor X", "Dividor Y", "Countdown" })
                Child(canvas.gameObject, name);

            DispatchLobbyBuilder.WirePlayerViews(canvas);

            CollectionAssert.AreEquivalent(new[] { "Player Backgrounds", "Player Icons", "Dividor X", "Dividor Y" },
                canvas.dispatchHides.Select(g => g.name).ToArray(), "the countdown stays: Dispatch's 3-2-1 is on it");
        }
    }
}
