using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The results screen in a real four-player local match. ResultsScreenCapture isn't a check: run by name, it films
    /// the podium to Logs/results (PNGs) for a look. The screen is an overlay canvas, so each frame is drawn through a
    /// camera of the test's own. They keep the test method bare around EnterPlayMode
    /// </summary>
    public class ResultsScreenTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LOADING = 90f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
            HouseRules.Current = HouseRules.Playing = HouseRules.Normal;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static IEnumerator Seconds(float seconds)
        {
            for (float until = Time.realtimeSinceStartup + seconds; Time.realtimeSinceStartup < until;)
                yield return null;
        }

        /// <summary>Four players into a short match with no golden round, the quick way (a rematch skips the tutorial)</summary>
        static IEnumerator IntoAMatch()
        {
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            // The first player joins on the title screen, the rest in player select (as ModesPlayTests)
            for (int player = 1; player <= 4; player++)
            {
                TestPlayers.Add();
                deadline = Time.realtimeSinceStartup + 10;
                while (PlayerInstantiate.Instance.PlayerCount < player && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (player == 1)
                {
                    HouseRules.Current = new HouseRules(MatchLength.Short, true, false);
                    GameManager.Instance.SetGameState(GameState.PlayerSelect);
                }
            }
            Assert.AreEqual(4, PlayerInstantiate.Instance.PlayerCount, "four players joined");
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "in the match");
            yield return null;
        }

        /// <summary>A match's worth of things happen, then the last wave ends: on to the results</summary>
        static IEnumerator PlayToTheResults()
        {
            // Seat 2 wins on deliveries, seat 0 steals, seat 3 swims, seat 1 is robbed
            int[] money = { 420, 135, 975, 60 };
            for (int seat = 0; seat < 4; seat++)
            {
                OrderHandler handler = OrderSync.HandlerIn(seat);
                if (handler != null)
                    handler.Score = money[seat];
            }
            for (int i = 0; i < 6; i++)
                FeatSync.Deliver(2, i < 2 ? Constants.OrderValue.Hard : Constants.OrderValue.Easy);
            FeatSync.Deliver(0, Constants.OrderValue.Medium);
            FeatSync.Deliver(1, Constants.OrderValue.Easy);
            for (int i = 0; i < 4; i++)
            {
                FeatSync.Steal(0, false);
                FeatSync.Rob(i < 3 ? 1 : 2);
            }
            for (int i = 0; i < 3; i++)
                FeatSync.Sink(3);
            FeatSync.Sink(1);
            FeatSync.Bump(3, 0, 0.8f);
            FeatSync.Bump(3, 1, 0.5f);
            FeatSync.Strike(0, 2, false, false);

            OrderManager orders = OrderManager.Instance;
            orders.wave = Mathf.Max(orders.maxEasy.Length, orders.maxMedium.Length, orders.maxHard.Length);
            orders.InitWave();
            float deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Results && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Results, State(), "the results");
        }

        /// <summary>Not a check: films the results screen to Logs/results for a look (run by name)</summary>
        [UnityTest, Explicit]
        public IEnumerator ResultsScreenCapture()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            yield return ResultsScreenCapture_InPlayMode();
        }

        static IEnumerator ResultsScreenCapture_InPlayMode()
        {
            yield return IntoAMatch();
            yield return PlayToTheResults();

            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "results");
            Directory.CreateDirectory(folder);
            float[] at = { 5.5f, 7f, 8.5f, 10f, 11.5f, 13f, 14.5f, 16f };
            float started = Time.realtimeSinceStartup;
            for (int shot = 0; shot < at.Length; shot++)
            {
                while (Time.realtimeSinceStartup - started < at[shot])
                    yield return null;
                yield return Shoot(Path.Combine(folder, "results-" + shot + ".png"));
            }
            yield return Seconds(0.2f);
        }

        // The podium through the results camera, then every overlay canvas over it through a camera far from the world
        // (an overlay canvas draws to no picture), put together here: 1920 x 1080
        static IEnumerator Shoot(string file)
        {
            const int WIDTH = 1920, HEIGHT = 1080;
            RenderTexture world = RenderTexture.GetTemporary(WIDTH, HEIGHT, 24);
            RenderTexture overlay = RenderTexture.GetTemporary(WIDTH, HEIGHT, 24);
            Camera camera = new GameObject("Results Capture").AddComponent<Camera>();
            camera.transform.position = new Vector3(0f, -20000f, 0f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.targetTexture = overlay;

            List<Canvas> overlays = new List<Canvas>();
            foreach (Canvas canvas in Object.FindObjectsOfType<Canvas>())
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay || !canvas.isActiveAndEnabled)
                    continue;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 50f;
                overlays.Add(canvas);
            }
            // The canvas takes the picture's size at the end of a frame, and the views that lay themselves out by it
            // do so in the next one's LateUpdate
            yield return null;
            yield return null;
            yield return null;
            if (!File.Exists(file + ".txt"))
                File.WriteAllText(file + ".txt", Layout());
            Canvas.ForceUpdateCanvases();
            camera.Render();

            Camera podium = ResultsMenu.Instance != null ? ResultsMenu.Instance.ResultsCamera : null;
            if (podium != null && podium.enabled)
            {
                podium.targetTexture = world;
                podium.Render();
                podium.targetTexture = null;
            }

            Color32[] back = Read(world, WIDTH, HEIGHT);
            Color32[] front = Read(overlay, WIDTH, HEIGHT);
            for (int i = 0; i < back.Length; i++)
            {
                // The overlay's colours are already multiplied by their alpha; its alpha came out squared
                float through = 1f - Mathf.Sqrt(front[i].a / 255f);
                back[i] = new Color32((byte)Mathf.Min(255f, front[i].r + back[i].r * through), (byte)Mathf.Min(255f, front[i].g + back[i].g * through),
                    (byte)Mathf.Min(255f, front[i].b + back[i].b * through), 255);
            }
            Texture2D frame = new Texture2D(WIDTH, HEIGHT, TextureFormat.RGBA32, false);
            frame.SetPixels32(back);
            File.WriteAllBytes(file, frame.EncodeToPNG());
            Object.Destroy(frame);

            foreach (Canvas canvas in overlays)
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
            }
            camera.targetTexture = null;
            Object.Destroy(camera.gameObject);
            RenderTexture.ReleaseTemporary(world);
            RenderTexture.ReleaseTemporary(overlay);
        }

        // The results canvas's boxes, for a look at where everything is
        static string Layout()
        {
            System.Text.StringBuilder text = new System.Text.StringBuilder();
            text.AppendLine("screen " + Screen.width + " x " + Screen.height);
            if (ResultsMenu.Instance != null)
                Describe(ResultsMenu.Instance.GetComponentInParent<Canvas>().rootCanvas.transform, 0, text);
            return text.ToString();
        }

        static void Describe(Transform box, int depth, System.Text.StringBuilder text)
        {
            RectTransform rect = box as RectTransform;
            UnityEngine.UI.Graphic graphic = box.GetComponent<UnityEngine.UI.Graphic>();
            text.AppendLine(new string(' ', depth * 2) + box.name + (box.gameObject.activeSelf ? "" : " (off)") + " at " + box.localPosition.ToString("F0")
                + " scale " + box.localScale.x.ToString("F2") + (rect != null ? " size " + rect.rect.size.ToString("F0") : "")
                + (graphic != null ? " " + graphic.GetType().Name + (graphic.enabled ? "" : " (not drawn)") : "")
                + (box.GetComponent<CanvasGroup>() != null ? " alpha " + box.GetComponent<CanvasGroup>().alpha.ToString("F2") : "") + " layer " + box.gameObject.layer);
            if (depth < 4)
            {
                foreach (Transform child in box)
                    Describe(child, depth + 1, text);
            }
        }

        static Color32[] Read(RenderTexture picture, int width, int height)
        {
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = picture;
            Texture2D frame = new Texture2D(width, height, TextureFormat.RGBA32, false);
            frame.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            frame.Apply();
            RenderTexture.active = before;
            Color32[] pixels = frame.GetPixels32();
            Object.Destroy(frame);
            return pixels;
        }
    }
}
