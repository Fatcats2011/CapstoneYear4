using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// The title screen's ghost show in the real menu scene (Play Mode): it runs only on the title screen and leaves
    /// nothing behind when the player goes elsewhere; every skit plays through and ends with its cast out of sight; the
    /// steals move the package; the Hat Thief's victim always has a hat. Takes a minute or two
    /// </summary>
    public class TitleShowTests
    {
        const float FAST = 8f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        /// <summary>Waits this long in the game's time (an EditMode test may only yield null)</summary>
        static IEnumerator Seconds(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
                yield return null;
        }

        /// <summary>The menu scene, playing, up to the title screen: returns its stage</summary>
        static IEnumerator Title(System.Action<TitleStage> found)
        {
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AtTitleScreen(), "the title screen");
            TitleStage stage = Object.FindObjectOfType<TitleStage>();
            Assert.IsNotNull(stage, "the stage");
            yield return null; // its first Update
            found(stage);
        }

        static void AssertClean(TitleStage stage, string when)
        {
            foreach (TitleActor actor in stage.actors)
            {
                Vector3 at = actor.transform.localPosition;
                Assert.GreaterOrEqual(Mathf.Abs(at.x), stage.OffX(at.z), actor.name + " is out of sight " + when);
                Assert.IsNull(actor.Package, actor.name + " carries nothing " + when);
                Assert.AreEqual(0, DG.Tweening.DOTween.TweensByTarget(actor.transform)?.Count ?? 0, actor.name + " isn't moving " + when);
                Assert.AreEqual(0, DG.Tweening.DOTween.TweensByTarget(actor.Model)?.Count ?? 0, actor.name + "'s model isn't moving " + when);
                Assert.AreEqual(Quaternion.identity, actor.Model.localRotation, actor.name + " is level " + when);
            }
            Assert.AreEqual(0, stage.PackagesOut.Count, "every package is back " + when);
            Assert.IsNull(stage.Current, "no skit " + when);
        }

        [UnityTest]
        public IEnumerator Show_RunsOnTheTitle_AndLeavingMidSkit_ResetsEveryActor()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScreenBuilder.MENU_SCENE);
            yield return new EnterPlayMode();
            yield return Show_RunsOnTheTitle_AndLeavingMidSkit_ResetsEveryActor_InPlayMode();
        }

        static IEnumerator Show_RunsOnTheTitle_AndLeavingMidSkit_ResetsEveryActor_InPlayMode()
        {
            TitleStage stage = null;
            yield return Title(s => stage = s);
            LogCollector log = new LogCollector();
            GameManager game = GameManager.Instance;

            Assert.AreEqual(TitleStage.POOL, stage.actors.Length, "four ghosts");
            foreach (TitleActor actor in stage.actors)
            {
                Assert.IsFalse(actor.GetComponentInChildren<BallDriving>(true).enabled, "no controls");
                Assert.IsFalse(actor.GetComponentsInChildren<Collider>(true).Any(c => c.enabled), "no colliders");
                Assert.IsFalse(actor.GetComponentsInChildren<AudioSource>(true).Any(a => a.enabled), "no sounds");
                Assert.IsTrue(actor.GetComponentsInChildren<Rigidbody>(true).All(b => b.isKinematic), "no physics");
            }

            Assert.IsTrue(stage.Running, "the show is on at the title screen");
            Assert.IsTrue(stage.stageCamera.enabled, "its camera films");
            Assert.IsTrue(stage.ghostImage.enabled, "its picture shows");
            Assert.IsNotNull(stage.ghostImage.texture, "the picture");

            // Into a skit, with someone on stage
            Time.timeScale = FAST;
            float deadline = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < deadline
                && !(stage.Current != null && stage.actors.Any(a => Mathf.Abs(a.transform.localPosition.x) < stage.EdgeX())))
                yield return null;
            Assert.IsNotNull(stage.Current, "a skit is playing");
            TitleActor[] cast = stage.CurrentCast;
            Assert.AreEqual(cast.Length, cast.Select(a => a.Hat).Distinct().Count(), "no two hats alike");
            Assert.AreEqual(cast.Length, cast.Select(a => a.Colour).Distinct().Count(), "no two colours alike");

            foreach (GameState elsewhere in new[] { GameState.Options, GameState.Credits, GameState.PlayerSelect })
            {
                game.SetGameState(elsewhere);
                yield return null;
                yield return null;
                Assert.IsFalse(stage.Running, "the show stops in " + elsewhere);
                Assert.IsFalse(stage.stageCamera.enabled, "the camera is off in " + elsewhere);
                Assert.IsFalse(stage.ghostImage.enabled, "the picture is hidden in " + elsewhere);
                AssertClean(stage, "in " + elsewhere);

                game.SetGameState(GameState.Menu);
                yield return null;
                yield return null;
                Assert.IsTrue(stage.Running, "the show is back on the title screen after " + elsewhere);
                deadline = Time.realtimeSinceStartup + 30;
                while (Time.realtimeSinceStartup < deadline && stage.Current == null)
                    yield return null;
                Assert.IsNotNull(stage.Current, "a fresh skit after " + elsewhere);
                yield return Seconds(2f); // somewhere into it
            }

            log.Dispose();
            Assert.IsEmpty(log.Problems, string.Join("\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator OffScreenX_IsPastTheCameraEdge_AtAnyAspect()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScreenBuilder.MENU_SCENE);
            yield return new EnterPlayMode();
            yield return OffScreenX_IsPastTheCameraEdge_AtAnyAspect_InPlayMode();
        }

        static IEnumerator OffScreenX_IsPastTheCameraEdge_AtAnyAspect_InPlayMode()
        {
            TitleStage stage = null;
            yield return Title(s => stage = s);
            Camera camera = stage.stageCamera;

            foreach (float aspect in new[] { 4f / 3f, 16f / 10f, 16f / 9f, 21f / 9f, 32f / 9f })
            {
                camera.aspect = aspect;
                foreach (float z in new[] { -1.5f, 0f, 3f })
                {
                    foreach (float y in new[] { 0f, 2.5f })
                    {
                        // The near end of a scooter standing at the off-screen spot is still out of sight
                        Vector3 nearEnd = stage.transform.TransformPoint(new Vector3(stage.OffX(z) - 1.5f, y, z));
                        float seen = camera.WorldToViewportPoint(nearEnd).x;
                        Assert.Greater(seen, 1f, "aspect " + aspect + ", z " + z + ", y " + y);
                        Vector3 edge = stage.transform.TransformPoint(new Vector3(stage.EdgeX(z) - 1f, y, z));
                        Assert.Less(camera.WorldToViewportPoint(edge).x, 1f, "the edge isn't far off screen: aspect " + aspect);
                    }
                }
            }
            camera.ResetAspect();
        }

        [UnityTest]
        public IEnumerator EverySkit_PlaysThrough_AndLeavesTheStageClean()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScreenBuilder.MENU_SCENE);
            yield return new EnterPlayMode();
            yield return EverySkit_PlaysThrough_AndLeavesTheStageClean_InPlayMode();
        }

        static IEnumerator EverySkit_PlaysThrough_AndLeavesTheStageClean_InPlayMode()
        {
            TitleStage stage = null;
            yield return Title(s => stage = s);
            LogCollector log = new LogCollector();
            stage.manual = true;
            stage.Halt();
            Time.timeScale = FAST;

            Assert.AreEqual(12, stage.skits.Length, "twelve skits");
            Assert.AreEqual(12, stage.skits.Select(s => s.Name).Distinct().Count(), "each with its own name");

            List<string> wrong = new List<string>();
            stage.SkitEnded = (skit, cast) =>
            {
                foreach (TitleActor actor in cast)
                {
                    Vector3 at = actor.transform.localPosition;
                    if (Mathf.Abs(at.x) < stage.OffX(at.z) - 0.05f)
                        wrong.Add(skit.Name + ": " + actor.name + " ends in sight at " + at);
                }
                foreach (Transform package in stage.PackagesOut)
                {
                    Vector3 at = stage.transform.InverseTransformPoint(package.position);
                    bool gone = Mathf.Abs(at.x) >= stage.EdgeX(at.z) + 0.5f || package.lossyScale.magnitude < 0.01f || at.y > 9f;
                    if (!gone)
                        wrong.Add(skit.Name + ": a package ends in sight at " + at);
                }

                // The steals: whoever rode on with the package doesn't leave with it
                if (Steals.Contains(skit.Name))
                {
                    if (cast[0].Package != null)
                        wrong.Add(skit.Name + ": the first of the cast still has the package");
                    if (!cast.Any(a => a.Package != null))
                        wrong.Add(skit.Name + ": nobody left with the package");
                }
            };

            for (int i = 0; i < stage.skits.Length; i++)
            {
                TitleSkit skit = stage.skits[i];
                Assert.That(skit.Cast, Is.InRange(2, TitleStage.POOL), skit.Name + "'s cast");

                bool done = false;
                float nearest = float.MaxValue;
                float started = Time.time;
                stage.StartCoroutine(Run(stage, i, () => done = true));
                float deadline = Time.realtimeSinceStartup + 30;
                while (!done && Time.realtimeSinceStartup < deadline)
                {
                    foreach (TitleActor actor in stage.actors)
                        nearest = Mathf.Min(nearest, actor.transform.localPosition.z);
                    yield return null;
                }
                float took = Time.time - started;

                Assert.IsTrue(done, skit.Name + " finishes");
                Assert.That(took, Is.InRange(4f, 10.5f), skit.Name + " takes 5 to 9 seconds, give or take");
                Assert.Greater(nearest, TitleStage.CAMERA_AT.z + 8f, skit.Name + " keeps its ghosts in the lane, away from the camera");
                AssertClean(stage, "after " + skit.Name);
            }

            log.Dispose();
            Assert.IsEmpty(wrong, string.Join("\n", wrong));
            Assert.IsEmpty(log.Problems, string.Join("\n", log.Problems));
        }

        static readonly string[] Steals = { "The Snatch", "Show-off", "Chain Chase", "Sneak", "Golden Parcel" };

        static IEnumerator Run(TitleStage stage, int skit, System.Action done)
        {
            yield return stage.PlayOne(skit);
            done();
        }

        [UnityTest]
        public IEnumerator HatThief_VictimAlwaysWearsAHat_AndHatsArePutRight()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScreenBuilder.MENU_SCENE);
            yield return new EnterPlayMode();
            yield return HatThief_VictimAlwaysWearsAHat_AndHatsArePutRight_InPlayMode();
        }

        static IEnumerator HatThief_VictimAlwaysWearsAHat_AndHatsArePutRight_InPlayMode()
        {
            TitleStage stage = null;
            yield return Title(s => stage = s);
            stage.manual = true;
            stage.Halt();
            int index = System.Array.FindIndex(stage.skits, s => s is HatThief);
            Assert.GreaterOrEqual(index, 0, "the Hat Thief");

            for (int i = 0; i < 100; i++)
            {
                TitleActor[] cast = stage.Dress(stage.skits[index]);
                Assert.IsTrue(cast[0].Hat.displayHat, "the victim has a hat to steal (deal " + i + ")");
                Assert.AreNotSame(cast[0].Hat, cast[1].Hat, "and the thief another, or none (deal " + i + ")");
            }

            Time.timeScale = FAST;
            PlayerHatInformationSO[] dealt = null, atTheEnd = null;
            PlayerHatInformationSO seenOnThief = null;
            stage.SkitEnded = (skit, cast) => atTheEnd = cast.Select(a => a.Hat).ToArray();
            bool done = false;
            stage.StartCoroutine(Run(stage, index, () => done = true));
            float deadline = Time.realtimeSinceStartup + 30;
            while (!done && Time.realtimeSinceStartup < deadline)
            {
                if (stage.CurrentCast != null)
                {
                    if (dealt == null)
                        dealt = stage.CurrentCast.Select(a => a.Hat).ToArray();
                    else if (stage.CurrentCast[1].Hat == dealt[0])
                        seenOnThief = dealt[0];
                }
                yield return null;
            }

            Assert.IsTrue(done, "the skit finishes");
            Assert.AreSame(dealt[0], seenOnThief, "the thief wore the victim's hat on the way");
            CollectionAssert.AreEqual(dealt, atTheEnd, "everyone's own hat back, off screen");
        }

        /// <summary>
        /// Not a check: films every skit to Logs/title for a look (run by name). Three frames a skit at 1920x1080
        /// </summary>
        [UnityTest, Explicit]
        public IEnumerator TitleScreenCapture()
        {
            // Nothing else here: Play Mode reloads the scripts, and only a bare test method survives that
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScreenBuilder.MENU_SCENE);
            yield return new EnterPlayMode();
            yield return TitleScreenCapture_InPlayMode();
        }

        static IEnumerator TitleScreenCapture_InPlayMode()
        {
            TitleStage stage = null;
            yield return Title(s => stage = s);
            stage.manual = true;
            stage.Halt();
            string folder = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Logs", "title");
            Directory.CreateDirectory(folder);
            yield return Seconds(0.2f);

            stage.pictureSize = new Vector2Int(1920, 1080);
            Texture2D frame = new Texture2D(1920, 1080, TextureFormat.RGBA32, false);
            for (int i = 0; i < stage.skits.Length; i++)
            {
                bool done = false;
                stage.Film(true);
                stage.StartCoroutine(Run(stage, i, () => done = true));
                float started = Time.time;
                float next = 0.75f;
                int shot = 0;
                while (!done && Time.time - started < 15f)
                {
                    if (Time.time - started >= next && stage.Picture != null)
                    {
                        RenderTexture before = RenderTexture.active;
                        RenderTexture.active = stage.Picture;
                        frame.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                        frame.Apply();
                        RenderTexture.active = before;
                        string file = Path.Combine(folder, (i + 1).ToString("00") + "-" + stage.skits[i].Name.Replace(' ', '_') + "-" + shot + ".png");
                        File.WriteAllBytes(file, frame.EncodeToPNG());
                        foreach (Transform package in stage.PackagesOut)
                            File.AppendAllText(Path.Combine(folder, "packages.txt"), Path.GetFileName(file) + " scale " + package.lossyScale.ToString("F2")
                                + " at " + stage.transform.InverseTransformPoint(package.position).ToString("F1") + " on " + package.parent.name + System.Environment.NewLine);
                        shot++;
                        next += 0.75f;
                    }
                    yield return null;
                }
                stage.Halt();
                yield return null;
            }
            Object.Destroy(frame);
            yield return Seconds(0.5f);
        }
    }
}
