using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// A diagnostic, run only by name (bash tools/run-tests.sh HitchProbe): plays one player through a match (menu, load,
    /// cutscene, tutorial, open map, first boost, the golden round) recording Unity's timing markers, and writes the
    /// biggest markers of every frame over 100 ms to Logs/hitches.txt, labelled with the step. Editor timings: shader
    /// compiling and on-demand asset loading there don't happen the same way in a build
    /// </summary>
    [Explicit("A diagnostic: run it by name")]
    [Category("Probe")]
    public class HitchProbe
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LONG_FRAME_MS = 100f;

        static readonly StringBuilder report = new StringBuilder();
        static int analysed = -1;
        static string step = "start";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            StopAll();
            if (Application.isPlaying)
                yield return new ExitPlayMode();
            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            File.WriteAllText("D:/.PersonalWork/DoA/CapstoneYear4/Logs/hitches.txt", report.ToString());
        }

        static void Step(string name)
        {
            Analyse();
            step = name;
            report.AppendLine("== " + name + " at frame " + Time.frameCount + ", rt " + Time.realtimeSinceStartup.ToString("0.00"));
        }

        static readonly List<(string name, ProfilerRecorder recorder)> recorders = new List<(string, ProfilerRecorder)>();
        static double lastRealtime;

        // Every timing marker Unity has now (markers that appear later are added on the next call)
        static readonly HashSet<string> recording = new HashSet<string>();
        static void RecordAll()
        {
            List<ProfilerRecorderHandle> handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription d = ProfilerRecorderHandle.GetDescription(handle);
                if (d.Flags.HasFlag(MarkerFlags.Counter) || d.UnitType != ProfilerMarkerDataUnit.TimeNanoseconds || recording.Contains(d.Name))
                    continue;
                recording.Add(d.Name);
                recorders.Add((d.Name, ProfilerRecorder.StartNew(d.Category, d.Name, 1)));
            }
        }

        static void StopAll()
        {
            foreach ((string name, ProfilerRecorder recorder) r in recorders)
                r.recorder.Dispose();
            recorders.Clear();
            recording.Clear();
        }

        // Called every frame: a long frame gets its biggest markers written down
        static double nextScan;
        static void Analyse()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now >= nextScan)
            {
                RecordAll();
                nextScan = now + 1.0;
            }
            double ms = (now - lastRealtime) * 1000.0;
            lastRealtime = now;
            if (ms < LONG_FRAME_MS || analysed < 0)
            {
                analysed = 0;
                return;
            }

            List<(double ms, string name)> top = new List<(double, string)>();
            foreach ((string name, ProfilerRecorder recorder) r in recorders)
            {
                if (!r.recorder.Valid)
                    continue;
                double m = r.recorder.LastValueAsDouble / 1e6;
                if (m >= 2.0)
                    top.Add((m, r.name));
            }
            top.Sort((a, b) => b.ms.CompareTo(a.ms));
            report.AppendLine("-- [" + step + "] frame " + Time.frameCount + ": " + ms.ToString("0") + " ms since the last");
            for (int i = 0; i < top.Count && i < 40; i++)
                report.AppendLine("   " + top[i].ms.ToString("0") + " ms  " + top[i].name);
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        static void Press(Gamepad pad, GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(button));
            InputSystem.QueueStateEvent(pad, new GamepadState());
        }

        [UnityTest]
        public IEnumerator OnePlayer_ThroughTheMatch_RecordsLongFrames()
        {
            report.Clear();
            analysed = -1;
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            RecordAll();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;

            Step("menu");
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Gamepad pad = TestPlayers.Pads[0];
            float next = 0;
            deadline = Time.realtimeSinceStartup + 15;
            while (State() != GameState.PlayerSelect && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= next)
                {
                    Press(pad, MainMenu.Instance.Selected == TitleEntry.Local ? GamepadButton.South : GamepadButton.DpadDown);
                    next = Time.realtimeSinceStartup + 0.5f;
                }
                yield return null;
            }

            Step("player select to loading");
            deadline = Time.realtimeSinceStartup + 240;
            next = 0;
            while ((State() == GameState.PlayerSelect || State() == GameState.Loading
                || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Design Scene(Main)") && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= next) { Press(pad, GamepadButton.South); next = Time.realtimeSinceStartup + 0.5f; }
                Analyse();
                yield return null;
            }

            RecordAll();
            Step("game scene up: cutscene");
            deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
            {
                Analyse();
                yield return null;
            }

            Step("tutorial");
            for (float until = Time.realtimeSinceStartup + 5f; Time.realtimeSinceStartup < until;)
            {
                Analyse();
                yield return null;
            }

            Step("tutorial skipped: to the open map");
            foreach (TutorialHandler handler in Object.FindObjectsOfType<TutorialHandler>())
                handler.TeachHandler(TutorialType.Final);
            OrderManager.Instance.DeleteActiveOrders();
            for (float until = Time.realtimeSinceStartup + 8f; Time.realtimeSinceStartup < until;)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
                Analyse();
                yield return null;
            }

            Step("first boost");
            BallDriving scooter = PlayerInstantiate.Instance.Roster[0].Player.GetComponentInChildren<BallDriving>();
            next = 0;
            for (float until = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < until;)
            {
                if (!scooter.Boosting && Time.realtimeSinceStartup >= next)
                {
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f }.WithButton(GamepadButton.South));
                    next = Time.realtimeSinceStartup + 0.5f;
                }
                else
                    InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
                Analyse();
                yield return null;
            }
            report.AppendLine("boosted: " + scooter.Boosting);

            Step("match ends: to the golden round");
            deadline = Time.realtimeSinceStartup + 120;
            next = 0;
            float nextWave = 0;
            while (State() != GameState.GoldenCutscene && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup >= next) { Press(pad, GamepadButton.South); next = Time.realtimeSinceStartup + 0.5f; }
                if (OrderManager.Instance != null && Time.realtimeSinceStartup >= nextWave && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "Design Scene(Main)")
                {
                    Reflect.Invoke(OrderManager.Instance, "IncrementWave");
                    nextWave = Time.realtimeSinceStartup + 0.5f;
                }
                Analyse();
                yield return null;
            }

            RecordAll();
            Step("golden round: " + State() + " in " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            for (float until = Time.realtimeSinceStartup + 15f; Time.realtimeSinceStartup < until;)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { rightTrigger = 1f });
                Analyse();
                yield return null;
            }
            Step("end");
            foreach (string name in recording)
            {
                if (name.Contains("LateUpdate") || name.Contains("WriteProperties"))
                    report.AppendLine("marker: " + name);
            }
            report.AppendLine("markers recorded: " + recording.Count);
        }
    }
}
