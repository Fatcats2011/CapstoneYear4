using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Playing the highlight reel (HighlightReel): stand-ins act out the recording under one full-screen camera while the
    /// real players are hidden, the reel ends after its last clip or on everyone's skip, and everything comes back as it
    /// was. The clock's walk through the clips (StartClock) and the director's checks (RunReel, Playable) are Edit Mode;
    /// the playback tests enter Play Mode in the menu scene with one player (about 10 s each), as LateCameraTests
    /// </summary>
    public class HighlightReelTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const double START_AT = 1000.0;

        static double now; // the game's clock, as the reel reads it in Play Mode
        static bool pressing; // every seat holds A

        // ---- The clock (Edit Mode) ----

        static ReelShot Shot(double cut, float speed = 1f)
        {
            return new ReelShot { Kind = ShotKind.HighWide, Cut = cut, Position = new Vector3(0, 20, 0), FramedMask = 1, Speed = speed };
        }

        static ReelClip Clip(double start, double end, params ReelShot[] shots)
        {
            return new ReelClip { Start = start, End = end, KeyTime = (start + end) / 2, SeatsMask = 1, Shots = shots };
        }

        static ReelList List(double startAt, params ReelClip[] clips)
        {
            var list = new ReelList { Clips = clips, StartAt = startAt };
            foreach (ReelClip clip in clips)
                list.Length += clip.RealLength;
            return list;
        }

        // Clip A: 10-14, slowed to half from 12 for 1.5 s of clip time (5.5 s real). Clip B: 20-23 (3 s)
        static ReelList SlowThenPlain()
        {
            return List(100, Clip(10, 14, Shot(10), Shot(12, 0.5f)), Clip(20, 23, Shot(20)));
        }

        [Test]
        public void StartClock_BeforeTheStart_IsTheFirstClipsStart()
        {
            Assert.AreEqual(10.0, HighlightReel.StartClock(SlowThenPlain(), 99.0), 1e-6);
            Assert.AreEqual(10.0, HighlightReel.StartClock(SlowThenPlain(), 100.0), 1e-6);
        }

        [Test]
        public void StartClock_WalksTheClips_HonouringSlowShotsAndCuts()
        {
            ReelList list = SlowThenPlain();

            Assert.AreEqual(11.0, HighlightReel.StartClock(list, 101.0), 1e-6, "full speed at first");
            Assert.AreEqual(12.0, HighlightReel.StartClock(list, 102.0), 1e-6, "the peak cuts in");
            Assert.AreEqual(12.5, HighlightReel.StartClock(list, 103.0), 1e-6, "half speed through the peak");
            Assert.AreEqual(13.5, HighlightReel.StartClock(list, 105.0), 1e-6, "the peak's 1.5 s of clip time took 3 s");
            Assert.AreEqual(20.1, HighlightReel.StartClock(list, 105.6), 1e-4, "a hard cut to the next clip");
            Assert.AreEqual(22.5, HighlightReel.StartClock(list, 108.0), 1e-4);
        }

        [Test]
        public void StartClock_PastTheEnd_IsDone()
        {
            Assert.IsTrue(double.IsPositiveInfinity(HighlightReel.StartClock(SlowThenPlain(), 108.6)));
            Assert.IsTrue(double.IsPositiveInfinity(HighlightReel.StartClock(new ReelList { Clips = new ReelClip[0] }, 0)), "no clips: done");
        }

        [Test]
        public void StartClock_APeakCutShortByTheClipsEnd()
        {
            // Slowed from 12, but the clip ends at 12.5: 2 s at full speed, then 0.5 s of clip time at half (3 s real)
            ReelList list = List(0, Clip(10, 12.5, Shot(10), Shot(12, 0.5f)), Clip(20, 21, Shot(20)));

            Assert.AreEqual(12.25, HighlightReel.StartClock(list, 2.5), 1e-6);
            Assert.AreEqual(20.5, HighlightReel.StartClock(list, 3.5), 1e-4, "the second clip starts 3 s in");
        }

        [Test]
        public void Playable_TooLongWithPeaks_DropsThePeaks()
        {
            // Eight 3 s clips, each with a peak: 36 s, over the cap. Their first shots alone: 24 s
            var clips = new ReelClip[ReelList.MAX_CLIPS];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = Clip(i * 10, i * 10 + 3, Shot(i * 10), Shot(i * 10 + 1, 0.5f));
            ReelList list = List(5, clips);
            Assert.IsFalse(NetChecks.Sane(list), "the test's reel is over the cap");

            ReelList playable = HighlightDirector.Playable(list);

            Assert.IsTrue(NetChecks.Sane(playable));
            Assert.AreEqual(clips.Length, playable.Clips.Length);
            Assert.AreEqual(24f, playable.Length, 1e-4);
            Assert.AreEqual(5.0, playable.StartAt);
            foreach (ReelClip clip in playable.Clips)
                Assert.AreEqual(1, clip.Shots.Length, "each clip keeps its first shot");
        }

        [Test]
        public void Playable_TooLongEvenWithoutPeaks_PlaysNothing()
        {
            var clips = new ReelClip[ReelList.MAX_CLIPS];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = Clip(i * 10, i * 10 + 5, Shot(i * 10));
            ReelList playable = HighlightDirector.Playable(List(5, clips));

            Assert.AreEqual(0, playable.Clips.Length);
        }

        [Test]
        public void Playable_OverTheReelCapOnlyWithPeaks_DropsThePeaks()
        {
            // Four 4 s clips, each with a half-speed peak adding 1.5 s: 22 s to play, which every machine accepts but the spec's 20 s doesn't
            var clips = new ReelClip[4];
            for (int i = 0; i < clips.Length; i++)
                clips[i] = Clip(i * 10, i * 10 + 4, Shot(i * 10), Shot(i * 10 + 1, 0.5f));
            ReelList list = List(5, clips);
            Assert.IsTrue(NetChecks.Sane(list), "a list the machines accept");
            Assert.Greater(list.Length, HighlightRules.MAX_REEL, "but longer than the reel may play");

            ReelList playable = HighlightDirector.Playable(list);

            Assert.IsTrue(NetChecks.Sane(playable));
            Assert.LessOrEqual(playable.Length, HighlightRules.MAX_REEL);
            Assert.AreEqual(16f, playable.Length, 1e-4);
            Assert.AreEqual(clips.Length, playable.Clips.Length);
            foreach (ReelClip clip in playable.Clips)
                Assert.AreEqual(1, clip.Shots.Length, "no peak shots");
        }

        [Test]
        public void Playable_WithinTheReelCap_IsLeftAlone()
        {
            ReelList list = List(5, Clip(0, 4, Shot(0), Shot(1, 0.5f)), Clip(10, 14, Shot(10), Shot(11, 0.5f)));

            ReelList playable = HighlightDirector.Playable(list);

            Assert.AreEqual(list, playable);
            Assert.AreEqual(2, playable.Clips[0].Shots.Length, "the peaks stay");
        }

        [Test]
        public void RealLength_AgreesWithPlayback_OnADirectorMadeList()
        {
            // Two seats driving along x at 2 m/s from 0 to 10 s; every shot clear. Big clips get a slow peak shot: one
            // with all of its slow stretch, one cut short by the clip's end
            var recording = new ReelRecording(400);
            for (int i = 0; i <= 200; i++)
            {
                for (int seat = 0; seat < 2; seat++)
                {
                    Vector3 ball = new Vector3(i / 20f * 2f, 0f, seat * 3f);
                    recording.Add(seat, new ReelSample { Time = i / 20.0, Pose = new ScooterPose { Ball = ball, Heading = 90f, ModelPosition = ball, ModelRotation = Quaternion.identity } });
                }
            }
            var clips = new List<Clip>
            {
                new Clip { Start = 2, End = 6, KeyTime = 4, Score = 20, SeatsMask = 0b11, KeyPlace = new Vector3(8, 0, 1.5f) },
                new Clip { Start = 6.5, End = 8.5, KeyTime = 8, Score = 20, SeatsMask = 0b11, KeyPlace = new Vector3(16, 0, 1.5f) },
            };
            ReelList list = ReelList.From(clips, new ShotDirector(recording, (a, b) => true), 50);
            Assert.IsTrue(System.Array.TrueForAll(list.Clips, clip => clip.Shots.Length == 2 && clip.Shots[1].Speed < 1f), "each clip has a peak");

            Assert.IsFalse(double.IsPositiveInfinity(HighlightReel.StartClock(list, 50 + list.Length - 0.001)), "still playing just before Length");
            Assert.AreEqual(8.5, HighlightReel.StartClock(list, 50 + list.Length - 0.0001), 1e-3, "at the last clip's end");
            Assert.IsTrue(double.IsPositiveInfinity(HighlightReel.StartClock(list, 50 + list.Length + 0.001)), "done just after Length");
        }

        // The reel list the director played, through a seam (a static: no lambda over a test's locals)
        static ReelList edited;

        static HighlightReel NoteList(ReelList list, ReelRecording recording, bool decides)
        {
            edited = list;
            return null;
        }

        [Test]
        public void RunReel_AMomentJustBeforeTimeUp_GetsAClipThatEndsWithTheRecording()
        {
            // Both seats recorded from 100 to 110 (the round's end); a steal at 109.5 would get a clip to 111
            ReelRecording recording = Recording();
            GameObject holder = new GameObject("Director");
            try
            {
                HighlightDirector director = holder.AddComponent<HighlightDirector>();
                director.Recording = recording;
                director.RoundEnd = 110.0;
                director.Moments.Add(new Moment { Kind = MomentKind.Steal, Time = 109.5, Seat = 0, Other = 1, TimeLeft = 0.5f, Place = new Vector3(100, 1, 50) });
                edited = default;
                director.PlayReel = NoteList;

                director.RunReel().MoveNext();

                Assert.IsNotNull(edited.Clips, "a list was made");
                Assert.AreEqual(1, edited.Clips.Length, "and it is playable");
                ReelClip clip = edited.Clips[0];
                Assert.LessOrEqual(clip.End, 110.0, "the clip ends with the round");
                Assert.GreaterOrEqual(clip.End - clip.Start, HighlightRules.MIN_CLIP - 1e-6, "still long enough");
                Assert.GreaterOrEqual(clip.Start, recording.First(0), "and not before the recording");
                Assert.GreaterOrEqual(clip.KeyTime, clip.Start);
                Assert.LessOrEqual(clip.KeyTime, clip.End);
                Assert.IsTrue(NetChecks.Sane(edited));
                for (int seat = 0; seat < 2; seat++)
                    Assert.IsTrue(recording.TryGet(seat, clip.End, out ReelSample sample) && !sample.Flags.Hidden, "a stand-in shows at the clip's end (seat " + seat + ")");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        [Test]
        public void RunReel_NoMoments_YieldsAtOnce()
        {
            GameObject holder = new GameObject("Director");
            try
            {
                HighlightDirector director = holder.AddComponent<HighlightDirector>();
                director.Recording = new ReelRecording(10);

                Assert.IsFalse(director.RunReel().MoveNext(), "nothing to show: no reel, not even a frame");
            }
            finally
            {
                Object.DestroyImmediate(holder);
            }
        }

        // ---- Playback (Play Mode) ----

        // Two seats driving along x from 100 to 110 at 10 m/s, at 20 samples a second
        static ReelRecording Recording()
        {
            var recording = new ReelRecording(400);
            for (int seat = 0; seat < 2; seat++)
            {
                for (int i = 0; i <= 200; i++)
                {
                    double time = 100 + i / 20.0;
                    Vector3 ball = new Vector3(seat * 5 + (float)(time - 100) * 10, 1, 50);
                    recording.Add(seat, new ReelSample
                    {
                        Time = time,
                        Pose = new ScooterPose { Ball = ball, Heading = 90, ModelPosition = ball - new Vector3(0, 0.5f, 0), ModelRotation = Quaternion.Euler(0, 90, 0) },
                        Flags = new DriveFlags(i % 40 < 20, false, false, 0, true, false),
                        Carrying = (byte)(seat == 0 ? (int)Constants.OrderValue.Hard + 1 : 0),
                    });
                }
            }
            return recording;
        }

        // A static wide shot of both, then one riding behind seat 1
        static ReelList TwoClips()
        {
            ReelClip wide = new ReelClip
            {
                Start = 101, End = 103, KeyTime = 102, SeatsMask = 0b11, Caption = Caption.Theft, TimeLeftAtStart = 30,
                Shots = new[] { new ReelShot { Kind = ShotKind.HighWide, Cut = 101, Position = new Vector3(20, 20, 20), FramedMask = 0b11, Speed = 1 } },
            };
            ReelClip behind = new ReelClip
            {
                Start = 105, End = 107, KeyTime = 106, SeatsMask = 0b10, Caption = Caption.Water, TimeLeftAtStart = 25,
                Shots = new[] { new ReelShot { Kind = ShotKind.Behind, Cut = 105, Subject = 1, Offset = new Vector3(0, 3, -7), FramedMask = 0b10, Speed = 1 } },
            };
            return List(START_AT, wide, behind);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
        }

        // Play Mode starts in the menu (the test yields EnterPlayMode itself: an Edit Mode test can't from a nested one)
        static EnterPlayMode InTheMenu()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            return new EnterPlayMode();
        }

        // Then a player joins (the title screen lets only player 1 in): seat 1 is recorded, but has no one to copy
        static IEnumerator OnePlayer()
        {
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while ((GameManager.Instance == null || GameManager.Instance.MainState != GameState.Menu) && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.Roster[0] == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(PlayerInstantiate.Instance.Roster[0], "a player joined");
            yield return null;
        }

        static HighlightReel Play()
        {
            now = START_AT + 0.5;
            pressing = false;
            HighlightReel reel = HighlightReel.Play(TwoClips(), Recording(), true);
            reel.Now = () => now;
            reel.Skipper.Pressed = seat => pressing;
            return reel;
        }

        // Every renderer and canvas under the roster's players, and whether it's on
        static Dictionary<Behaviour, bool> CanvasesNow()
        {
            var state = new Dictionary<Behaviour, bool>();
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
            {
                foreach (Canvas canvas in slot.Player.GetComponentsInChildren<Canvas>(true))
                    state[canvas] = canvas.enabled;
            }
            return state;
        }

        static Dictionary<Renderer, bool> RenderersNow()
        {
            var state = new Dictionary<Renderer, bool>();
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
            {
                foreach (Renderer renderer in slot.Player.GetComponentsInChildren<Renderer>(true))
                    state[renderer] = renderer.enabled;
            }
            return state;
        }

        static void AssertRestored(Dictionary<Renderer, bool> renderers, Dictionary<Behaviour, bool> canvases)
        {
            foreach (KeyValuePair<Renderer, bool> before in renderers)
            {
                if (before.Key != null)
                    Assert.AreEqual(before.Value, before.Key.enabled, before.Key.name + " is back as it was");
            }
            foreach (KeyValuePair<Behaviour, bool> before in canvases)
            {
                if (before.Key != null)
                    Assert.AreEqual(before.Value, before.Key.enabled, before.Key.name + " is back as it was");
            }
        }

        [UnityTest]
        public IEnumerator Play_CoversThePlayersAndHidesTheirRenderers()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> before = RenderersNow();
            Assert.IsTrue(before.ContainsValue(true), "the players are drawn in the menu");

            Play();
            yield return null;
            yield return null;

            Assert.IsTrue(CameraBudget.CoveredNow(), "the reel's camera covers the players' views");
            foreach (KeyValuePair<Renderer, bool> renderer in before)
                Assert.IsFalse(renderer.Key.enabled, renderer.Key.name + " is hidden");
        }

        [UnityTest]
        public IEnumerator Play_StandInsFollowTheRecording()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightReel reel = Play();
            ReelRecording recording = Recording();
            yield return null;
            yield return null;

            Assert.AreEqual(101.5, reel.Clock, 1e-6, "half a second into the reel");
            Assert.IsTrue(recording.TryGet(0, 101.5, out ReelSample sample));
            Transform standIn = reel.StandIn(0);
            Assert.IsNotNull(standIn, "the player has a stand-in");
            Assert.IsTrue(standIn.gameObject.activeInHierarchy);
            Assert.Less(Vector3.Distance(sample.Pose.ModelPosition, standIn.position), 0.01f, "it's where it was recorded");
            Assert.IsNull(reel.StandIn(1), "no player in seat 1 to copy");

            now = START_AT + 2.5; // 0.5 s into the second clip: the camera rides behind seat 1
            yield return null;
            Assert.AreEqual(105.5, reel.Clock, 1e-6, "a hard cut to the second clip");
            Assert.IsTrue(recording.TryGet(1, 105.5, out ReelSample subject));
            Vector3 expected = subject.Pose.Ball + Quaternion.Euler(0, subject.Pose.Heading, 0) * new Vector3(0, 3, -7);
            Assert.Less(Vector3.Distance(expected, reel.Camera.transform.position), 0.01f, "the camera rides behind its subject");
        }

        [UnityTest]
        public IEnumerator Play_EndsAfterTheLastClip_AndRestoresEverything()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightReel reel = Play();
            yield return null;
            Assert.IsFalse(reel.Done);

            now = START_AT + 4.1;
            yield return null;
            yield return null;

            Assert.IsTrue(reel.Done, "done after the last clip");
            Assert.IsTrue(reel == null, "the reel is gone");
            Assert.IsFalse(CameraBudget.CoveredNow(), "the players' views are back");
            AssertRestored(renderers, canvases);
        }

        [UnityTest]
        public IEnumerator Skip_AllSeatsVote_EndsEarly()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightReel reel = Play();

            // A fresh press once the prompt shows: both players hold A
            float wait = Time.realtimeSinceStartup + SkipHold.SHOW_AFTER + 0.3f;
            while (Time.realtimeSinceStartup < wait)
                yield return null;
            pressing = true;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (reel != null && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(reel.Done, "everyone voted: the reel ended");
            Assert.Less(reel.Clock, 103.0, "early: still in the first clip");
        }

        [UnityTest]
        public IEnumerator End_Twice_IsHarmless()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightReel reel = Play();
            yield return null;

            reel.End();
            Assert.DoesNotThrow(reel.End);
            yield return null;

            Assert.IsTrue(reel == null);
            AssertRestored(renderers, canvases);
        }

        [UnityTest]
        public IEnumerator End_AfterARosterSeatLeft_DoesNotThrow()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightReel reel = Play();
            yield return null;

            Object.Destroy(PlayerInstantiate.Instance.Roster[0].Player);
            yield return null;
            yield return null;

            Assert.DoesNotThrow(reel.End);
            yield return null;
            Assert.IsTrue(reel == null);
            AssertRestored(renderers, canvases); // nothing left of the player's to restore, and nothing thrown
        }
        // The reel the director started, and the loads it asked for (statics: an iterator's lambdas can't share its locals safely)
        static HighlightReel played;
        static int loads;

        static HighlightReel PlayAndKeep(ReelList list, ReelRecording recording, bool decides)
        {
            played = HighlightReel.Play(list, recording, decides);
            return played;
        }

        static void CountLoad() { loads++; }

        // The director with a steal by seat 0 on seat 1 at 105, in the recording, and its reel started through a seam
        static HighlightDirector DirectorWithAMoment()
        {
            HighlightDirector director = HighlightDirector.Instance;
            Assert.IsNotNull(director, "the director made on launch");
            director.Recording = Recording();
            director.Moments.Clear();
            director.Moments.Add(new Moment { Kind = MomentKind.Steal, Time = 105, Seat = 0, Other = 1, TimeLeft = 20, Place = new Vector3(55, 1, 50) });
            return director;
        }

        [UnityTest]
        public IEnumerator RunReel_PlayThrows_FinishesAndRestoresThePlayers()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightDirector director = DirectorWithAMoment();
            director.PlayReel = (list, recording, decides) => HighlightReel.Play(list, null, decides); // throws once the players are hidden
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

            IEnumerator run = director.RunReel();
            Assert.IsFalse(run.MoveNext(), "no reel: RunReel ends at once, so the load follows");
            Assert.IsFalse(director.ReelCutShort);
            yield return null;

            Assert.IsNull(GameObject.Find("Highlight Reel"), "the half-built reel is gone");
            AssertRestored(renderers, canvases);
        }

        static double ThrowingClock() { throw new System.InvalidOperationException("clock broke"); }

        [UnityTest]
        public IEnumerator Playback_ThrowsInAFrame_LogsEndsAndRestoresThePlayers()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightReel reel = Play();
            yield return null;
            Assert.IsFalse(reel.Done);
            reel.Now = ThrowingClock;
            LogAssert.Expect(LogType.Exception, new Regex("InvalidOperationException"));

            yield return null;
            yield return null;

            Assert.IsTrue(reel == null, "the reel ended itself, so RunReel's wait is over");
            AssertRestored(renderers, canvases);
            Assert.IsFalse(CameraBudget.CoveredNow(), "the players' views are back");
        }

        [UnityTest]
        public IEnumerator ReelThen_ReelDestroyedByAnotherScene_DoesNotLoad()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            Dictionary<Renderer, bool> renderers = RenderersNow();
            Dictionary<Behaviour, bool> canvases = CanvasesNow();
            HighlightDirector director = DirectorWithAMoment();
            played = null;
            loads = 0;
            director.PlayReel = PlayAndKeep;
            director.StartCoroutine(director.ReelThen(CountLoad));
            Assert.IsNotNull(played, "a reel plays");
            yield return null;

            Object.Destroy(played.gameObject); // as a scene load would
            yield return null;
            yield return null;

            Assert.IsTrue(director.ReelCutShort);
            Assert.AreEqual(0, loads, "the game moved on elsewhere: no golden round load");
            AssertRestored(renderers, canvases);
        }

        [UnityTest]
        public IEnumerator ReelThen_ReelEnds_Loads()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = DirectorWithAMoment();
            played = null;
            loads = 0;
            director.PlayReel = PlayAndKeep;
            director.StartCoroutine(director.ReelThen(CountLoad));
            yield return null;

            played.End(); // as a skip would
            yield return null;
            yield return null;

            Assert.IsFalse(director.ReelCutShort);
            Assert.AreEqual(1, loads, "the golden round loads after the reel");
        }

        [UnityTest]
        public IEnumerator Play_HidesThePlayersLightsAndDecals_AndRestoresThem()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            var before = new Dictionary<Behaviour, bool>();
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
            {
                foreach (Light light in slot.Player.GetComponentsInChildren<Light>(true))
                    before[light] = light.enabled;
                foreach (UnityEngine.Rendering.Universal.DecalProjector decal in slot.Player.GetComponentsInChildren<UnityEngine.Rendering.Universal.DecalProjector>(true))
                    before[decal] = decal.enabled;
            }
            Assert.IsTrue(before.ContainsValue(true), "the player has a headlight or logo on");

            HighlightReel reel = Play();
            yield return null;
            foreach (Behaviour part in before.Keys)
                Assert.IsFalse(part.enabled, part.name + " is off during the reel");

            reel.End();
            yield return null;
            foreach (KeyValuePair<Behaviour, bool> part in before)
                Assert.AreEqual(part.Value, part.Key.enabled, part.Key.name + " is back as it was");
        }

        // ---- A reel shared and heard ----

        static readonly System.Collections.Generic.List<ReelList> shared = new System.Collections.Generic.List<ReelList>();
        static ReelList playedList;

        static void Share(ReelList list) { shared.Add(list); }

        static HighlightReel PlayAndNote(ReelList list, ReelRecording recording, bool decides)
        {
            playedList = list;
            playedDecides = decides;
            sharedBeforePlay = shared.Count;
            return PlayAndKeep(list, recording, decides);
        }

        static int sharedBeforePlay;
        static bool playedDecides;
        static bool inTheRound;

        static bool InTheRound() { return inTheRound; }

        [UnityTest]
        public IEnumerator RunReel_SharesTheListItPlays_BeforePlayingIt()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = DirectorWithAMoment();
            shared.Clear();
            HighlightSync.Reset();
            HighlightSync.Shared += Share;
            played = null;
            director.PlayReel = PlayAndNote;

            director.RunReel().MoveNext();
            HighlightSync.Reset();

            Assert.IsNotNull(played, "a reel plays");
            Assert.AreEqual(1, shared.Count, "the list was shared once");
            Assert.AreEqual(1, sharedBeforePlay, "and before the reel started");
            Assert.AreEqual(playedList, shared[0], "the list that plays");
            played.End();
        }

        static HighlightDirector ClientWithARecording()
        {
            HighlightDirector director = HighlightDirector.Instance;
            director.Recording = Recording();
            director.InMainRound = InTheRound;
            played = null;
            director.PlayReel = PlayAndNote;
            GameAuthority.Role = NetworkRole.Client;
            return director;
        }

        static ReelList StartingIn(double seconds)
        {
            ReelList list = TwoClips();
            list.StartAt = HighlightDirector.Now() + seconds;
            return list;
        }

        [UnityTest]
        public IEnumerator Client_ArrivedList_InTheMainRound_PlaysWithoutDeciding()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = true;

            director.OnReelArrived(StartingIn(5));

            Assert.IsNotNull(played, "the reel plays");
            Assert.AreEqual(StartingIn(5).Clips.Length, playedList.Clips.Length);
            Assert.IsFalse(playedDecides, "a client's skip vote only goes to the host");
            played.End();
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Client_ArrivedList_OutsideTheMainRound_IsIgnored()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = false;

            director.OnReelArrived(StartingIn(5));

            Assert.IsNull(played);
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Client_ArrivedList_AlreadyOver_PlaysNothing()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = true;
            ReelList list = TwoClips();

            director.OnReelArrived(StartingIn(-(list.Length + 5)));

            Assert.IsNull(played);
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Host_ArrivedList_IsIgnored()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            GameAuthority.Role = NetworkRole.Host;
            inTheRound = true;

            director.OnReelArrived(StartingIn(5));

            Assert.IsNull(played);
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Client_ArrivedList_StartingFarAhead_PlaysNothing()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = true;

            director.OnReelArrived(StartingIn(HighlightDirector.MAX_LEAD + 60));

            Assert.IsNull(played);
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Client_SecondListInTheRound_PlaysNothing_EvenAfterTheFirstIsDone()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = true;

            director.OnReelArrived(StartingIn(5));
            Assert.IsNotNull(played);
            played.End();
            yield return null;
            played = null;
            director.OnReelArrived(StartingIn(5));
            Assert.IsNull(played, "one reel a round");

            director.OnStateApplied(GameState.Begin); // the next round's waves
            director.OnReelArrived(StartingIn(5));
            Assert.IsNotNull(played, "a new round plays again");
            played.End();
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Arrive_ReachesTheClientsDirector()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            director.End(); // as a fresh launch: the tests above may have reset the seam's listeners
            director.Begin();
            inTheRound = true;

            HighlightSync.Arrive(StartingIn(5));

            Assert.IsNotNull(played, "HighlightSync.Arrive plays the reel");
            played.End();
            GameAuthority.Role = NetworkRole.Offline;
        }

        [UnityTest]
        public IEnumerator Client_TwoListsArrive_OnlyTheFirstPlays()
        {
            yield return InTheMenu();
            yield return OnePlayer();
            HighlightDirector director = ClientWithARecording();
            inTheRound = true;

            director.OnReelArrived(StartingIn(5));
            HighlightReel first = played;
            played = null;
            director.OnReelArrived(StartingIn(5));

            Assert.IsNotNull(first);
            Assert.IsNull(played, "a reel already plays");
            first.End();
            GameAuthority.Role = NetworkRole.Offline;
        }
    }
}
