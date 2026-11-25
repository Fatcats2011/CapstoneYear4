using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using static DoA.Tests.TestHelpers;

namespace DoA.Tests
{
    /// <summary>
    /// Players' one-shots in an online match on this computer (127.0.0.1):
    /// - Hosting: this machine hosts with the game. Its player's one-shots go to every client, in order, but not its order
    ///   sounds (every machine replays those). Another machine's one-shots play on that player's scooter here, quieter the
    ///   farther it is from this machine's player, and the host passes them on to every client. A machine speaks only for
    ///   its own seat, and only of a kind it may send. D-pad emotes play on that player's ghost here and go to every client;
    ///   a hard bump the host judges goes out as Dizzy for its victim. The host's clock rings out to every client: a new
    ///   wave's bells, and time up. The other machine is a session without the game.
    /// - Joining: this machine joins with the game. Its player's fall in the water reaches the host, then where they rise.
    ///   The host's player's fall shows here: its sound, then its gravestone and the reborn sparkle at its point. A rise at a
    ///   point this scene doesn't have shows nothing. This machine's d-pad emote reaches the host; the host's plays on its
    ///   ghost here, and the host's Dizzy plays on whichever ghost it names, this machine's own too.
    /// Loads the game scene: a minute or two each. No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineCuesNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string GAME = "Design Scene(Main)";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7807;
        const string VERSION = "test";
        const float WAIT = 15f;
        const float LOADING = 180f; // loading the game scene in batch mode

        /// <summary>
        /// Remembers the one-shots a machine heard from the host (each one's seat and cue, in order)
        /// </summary>
        class CueRecorder
        {
            public readonly List<int> Seats = new List<int>();
            public readonly List<ScooterCue> Cues = new List<ScooterCue>();

            public CueRecorder(OnlineMatch match)
            {
                match.CueReceived += OnCue;
            }

            void OnCue(int seat, ScooterCue cue)
            {
                Seats.Add(seat);
                Cues.Add(cue);
            }

            public bool Heard(int seat, ScooterCue cue)
            {
                for (int i = 0; i < Cues.Count; i++)
                {
                    if (Seats[i] == seat && Cues[i].Equals(cue))
                        return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Remembers the one-shots the host heard from clients (who sent the last, each one's seat and cue, in order)
        /// </summary>
        class CueReportRecorder
        {
            public ulong Machine;
            public readonly List<int> Seats = new List<int>();
            public readonly List<ScooterCue> Cues = new List<ScooterCue>();

            public CueReportRecorder(OnlineMatch match)
            {
                match.CueReported += OnReported;
            }

            void OnReported(ulong machine, int seat, ScooterCue cue)
            {
                Machine = machine;
                Seats.Add(seat);
                Cues.Add(cue);
            }
        }

        /// <summary>
        /// Remembers the host's clock's one-shots a machine heard, in order
        /// </summary>
        class ClockRecorder
        {
            public readonly List<ClockCue> Cues = new List<ClockCue>();

            public ClockRecorder(OnlineMatch match)
            {
                match.ClockRang += OnRang;
            }

            void OnRang(ClockCue cue)
            {
                Cues.Add(cue);
            }
        }

        // Which machines told the host they have the game loaded
        class ReportRecorder
        {
            public readonly List<ulong> Machines = new List<ulong>();

            public ReportRecorder(OnlineMatch match)
            {
                match.MachineLoaded += OnLoaded;
            }

            void OnLoaded(ulong machine, MatchScene scene)
            {
                if (scene == MatchScene.Game)
                    Machines.Add(machine);
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
            OrderSync.Reset();
            TutorialSync.Reset();
            RespawnSync.Reset();
            CueSync.Reset();
        }

        // The sound pool of the scooter in a seat, here
        static SoundPool PoolOf(int seat)
        {
            return Slot(seat).Player.GetComponentInChildren<SoundPool>(true);
        }

        // Whether a sound pool played a sound: one of its sources has that sound's clip (a source keeps its clip after it ends)
        static bool Played(SoundPool pool, string key)
        {
            AudioClip clip = SoundManager.Instance.GetSFX(key).clip;
            foreach (AudioSource source in pool.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.clip == clip)
                    return true;
            }
            return false;
        }

        // The source of a sound pool playing a sound now, or null
        static AudioSource SourcePlaying(SoundPool pool, string key)
        {
            AudioClip clip = SoundManager.Instance.GetSFX(key).clip;
            foreach (AudioSource source in pool.GetComponentsInChildren<AudioSource>(true))
            {
                if (source.isPlaying && source.clip == clip)
                    return source;
            }
            return null;
        }

        static bool Playing(SoundPool pool, string key)
        {
            return SourcePlaying(pool, key) != null;
        }

        // How many horn flashes the scooter in a seat shows here
        static int Flashes(int seat)
        {
            PhaseIndicator horns = Slot(seat).Player.GetComponentInChildren<PhaseIndicator>(true);
            string flash = horns.flashParticles.name + "(Clone)";
            int count = 0;
            foreach (Transform child in horns.transform)
            {
                if (child.name == flash)
                    count++;
            }
            return count;
        }

        // Whether the ball of the scooter in a seat is there, here
        static bool IsAt(int seat, Vector3 spot)
        {
            return Vector3.Distance(ScooterIn(seat).Sphere.transform.position, spot) < 1f;
        }

        // The respawn (on the ball) of the scooter in a seat, here
        static Respawn RespawnOf(int seat)
        {
            return ScooterIn(seat).Sphere.GetComponent<Respawn>();
        }

        // The respawn point farthest from a place
        static int FarthestPointFrom(Vector3 spot)
        {
            int farthest = -1;
            float gap = -1f;
            for (int i = 0; RespawnManager.Instance.PointAt(i) != null; i++)
            {
                float distance = Vector3.Distance(RespawnManager.Instance.PointAt(i).PlayerSpawn, spot);
                if (distance > gap)
                {
                    gap = distance;
                    farthest = i;
                }
            }
            return farthest;
        }

        // The ghost emotes of the player in a seat, here (on their top object, as OnlineCues finds them)
        static PlayerEmotes GhostOf(int seat)
        {
            return OrderSync.HandlerIn(seat).GetComponentInParent<PlayerEmotes>();
        }

        // Whether a ghost asked for an action since it had asked this many times
        static bool AskedSince(PlayerEmotes ghost, int count, GhostAction action)
        {
            return ghost.AskCount > count && ghost.Asked == action;
        }

        // Whether a gravestone stands there
        static bool GraveStandsAt(Vector3 spot)
        {
            foreach (RespawnGravestone grave in Object.FindObjectsOfType<RespawnGravestone>())
            {
                if (Vector3.Distance(grave.transform.position, spot) < 0.1f)
                    return true;
            }
            return false;
        }

        // Whether the reborn sparkle plays there
        static bool SparkleAt(Vector3 spot)
        {
            foreach (ParticleSystem sparkle in Object.FindObjectsOfType<ParticleSystem>())
            {
                if (sparkle.name == "RebornParticles(Clone)" && Vector3.Distance(sparkle.transform.position, spot) < 1f)
                    return true;
            }
            return false;
        }

        [UnityTest]
        public IEnumerator Hosting_EveryPlayersOneShotsReachEveryMachine_AndPlayOnTheirScooterHere()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION);
            OnlineGame.Attach(host);
            Assert.IsTrue(host.HostDirect(THIS_COMPUTER, PORT), "hosting");
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlineSession other = OnlineSession.Create(VERSION); // another machine: a session without the game
            other.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && other.Match != null && ScooterInSeat(other, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            CueRecorder heard = new CueRecorder(other.Match);
            OnlineScooter theirs = ScooterInSeat(other, 1);

            // The match starts on both machines. Both players finish the tutorial (this machine's waits in the city), and
            // the first wave begins
            PlayerInstantiate.Instance.ReadyUp(0);
            SceneFlow.Current.LoadGameScene();
            other.Match.ReportLoaded(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING + 60;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave: online has no tutorial");
            FinishTutorialInTheCity(0);
            other.Match.ReportLearnt(1);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave");
            Vector3 spot = CitySpot();

            // This machine's player's one-shots go to every client, in order. Its order sounds don't: every machine
            // replays those
            SoundPool mine = PoolOf(0);
            mine.PlayBoostActivate();
            mine.PlayMiniBoost();
            mine.PlayBoostReady();
            mine.PlayPhaseSound();
            mine.PlayPhaseSound();
            mine.StopPhaseSound();
            mine.StopPhaseSound();
            mine.PlayOrderPickup();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (heard.Cues.Count < 5 && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            CollectionAssert.AreEqual(new[] { ScooterCue.Of(CueKind.Boost), ScooterCue.Of(CueKind.DriftBoost), ScooterCue.Of(CueKind.HornReady),
                ScooterCue.Of(CueKind.Phase), ScooterCue.Of(CueKind.PhaseEnd) }, heard.Cues, "one phase, one end; no pickup");
            CollectionAssert.AreEqual(new[] { 0, 0, 0, 0, 0 }, heard.Seats);

            // The other machine's player, 5 m from this machine's: its one-shots play on its scooter here, and every
            // client hears them
            Vector3 near = spot + Vector3.right * 5f;
            ShareAt(theirs, near);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!IsAt(1, near) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(IsAt(1, near), "their scooter is beside this machine's");
            SoundPool remote = PoolOf(1);
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.Boost));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Played(remote, "boost_used") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Played(remote, "boost_used"), "their boost plays on their scooter here");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(1, ScooterCue.Of(CueKind.Boost)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Heard(1, ScooterCue.Of(CueKind.Boost)), "the host sends it on to every client (its own machine skips it)");
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.HornReady));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Played(remote, "boost_charged") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Played(remote, "boost_charged"), "their full horn sounds here");
            Assert.AreEqual(2, Flashes(1), "both horns flash");
            remote.PlayOrderTheft(); // a steal's whoosh, as the host's order changes replay it here
            Assert.IsTrue(Played(remote, "whoosh"), "the thief's whoosh");

            // Phasing takes over their boost's sound, as on their own machine
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.Boost));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Playing(remote, "boost_used") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Playing(remote, "boost_used"), "their boost plays");
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.Phase));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Playing(remote, "phasing") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Playing(remote, "phasing"), "their phasing plays");
            Assert.IsFalse(Playing(remote, "boost_used"), "and their boost's sound stops");
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.PhaseEnd));

            // Their death plays on the players' mixer group, as their own machine plays it
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.Death));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (SourcePlaying(remote, "death") == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            AudioSource death = SourcePlaying(remote, "death");
            Assert.IsNotNull(death, "their death plays here");
            Assert.AreEqual("Player", death.outputAudioMixerGroup.name, "on the Player group");

            // 200 m away it's silent
            Vector3 far = spot + Vector3.right * 200f;
            ShareAt(theirs, far);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!IsAt(1, far) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(IsAt(1, far), "their scooter is far away");
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.DriftBoost));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(1, ScooterCue.Of(CueKind.DriftBoost)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            for (float until = Time.realtimeSinceStartup + 0.5f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsTrue(heard.Heard(1, ScooterCue.Of(CueKind.DriftBoost)), "the host took it");
            Assert.IsFalse(Played(remote, "mini"), "silent 200 m away");
            remote.PlayOrderPickup();
            Assert.IsFalse(Played(remote, "pickup"), "a pickup 200 m away is silent too");

            // A cue for a seat that isn't the sender's: nothing, and the host doesn't pass it on
            int before = heard.Cues.Count;
            other.Match.ReportCue(0, ScooterCue.Of(CueKind.Death));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(before, heard.Cues.Count, "not the sender's seat");

            // Their d-pad emote: their ghost here plays its action (up: BigWave), and the host passes it on
            ShareAt(theirs, near);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!IsAt(1, near) && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerEmotes theirGhost = GhostOf(1);
            Assert.IsNotNull(theirGhost, "their ghost has its emotes here");
            int asked = theirGhost.AskCount;
            other.Match.ReportCue(1, ScooterCue.Emote(0));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!AskedSince(theirGhost, asked, GhostAction.BigWave) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AskedSince(theirGhost, asked, GhostAction.BigWave), "their ghost waves here");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(1, ScooterCue.Emote(0)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Heard(1, ScooterCue.Emote(0)), "and every client hears it");

            // Their scooter hit a wall (only their machine sees it): their ghost sways here, and the host passes it on
            asked = theirGhost.AskCount;
            other.Match.ReportCue(1, ScooterCue.WallBump(22f));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!AskedSince(theirGhost, asked, GhostAction.WallBump) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AskedSince(theirGhost, asked, GhostAction.WallBump), "their ghost sways here");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(1, ScooterCue.WallBump(22f)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Heard(1, ScooterCue.WallBump(22f)), "and every client hears it");

            // This machine's player's d-pad emote: their ghost plays it here (down: Cackle) and every client hears it
            PlayerEmotes myGhost = GhostOf(0);
            myGhost.Pad(2);
            Assert.AreEqual(GhostAction.Cackle, myGhost.Asked);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(0, ScooterCue.Emote(2)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Heard(0, ScooterCue.Emote(2)), "this machine's emote reaches every client");

            // A client can't emote for a seat it doesn't own, send a kind the game doesn't know, or say it's dizzy (only
            // the host judges bumps): none is shown or passed on
            before = heard.Cues.Count;
            int myAsked = myGhost.AskCount;
            asked = theirGhost.AskCount;
            other.Match.ReportCue(0, ScooterCue.Emote(1));
            other.Match.ReportCue(1, ScooterCue.Of((CueKind)200));
            other.Match.ReportCue(1, ScooterCue.Of(CueKind.Dizzy));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.AreEqual(before, heard.Cues.Count, "nothing passed on");
            Assert.IsFalse(AskedSince(myGhost, myAsked, GhostAction.Taunt), "not their seat");
            Assert.IsFalse(AskedSince(theirGhost, asked, GhostAction.SeeingStars), "a client's Dizzy");

            // The host judges a hard bump on their player: their ghost here sees stars once (from the bump itself), and
            // every client hears Dizzy for their seat. A soft one sends nothing
            asked = theirGhost.AskCount;
            FeatSync.Bump(0, 1, 0.9f);
            Assert.AreEqual(asked + 1, theirGhost.AskCount, "once, from the bump");
            Assert.AreEqual(GhostAction.SeeingStars, theirGhost.Asked);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!heard.Heard(1, ScooterCue.Of(CueKind.Dizzy)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(heard.Heard(1, ScooterCue.Of(CueKind.Dizzy)), "Dizzy for their seat");
            FeatSync.Bump(1, 0, 0.3f);
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.IsFalse(heard.Heard(0, ScooterCue.Of(CueKind.Dizzy)), "a soft bump sends nothing");

            // The host's clock rings out: a new wave's bells, then time up
            ClockRecorder rang = new ClockRecorder(other.Match);
            OrderManager.Instance.wave = 1;
            OrderManager.Instance.InitWave(); // the second wave
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rang.Cues.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, rang.Cues.Count, "the bells ring out");
            Assert.AreEqual(ClockCue.WaveBells, rang.Cues[0]);
            OrderManager.Instance.wave = 99;
            OrderManager.Instance.InitWave(); // past the last wave: time up
            OrderManager.Instance.StopAllCoroutines(); // the golden round doesn't load: the test ends here
            deadline = Time.realtimeSinceStartup + WAIT;
            while (rang.Cues.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, rang.Cues.Count, "then time up");
            Assert.AreEqual(ClockCue.TimeUp, rang.Cues[1]);

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            other.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator Joining_ThisMachinesRespawnGoesOut_AndTheHostsShowsHere()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            OnlineSession host = OnlineSession.Create(VERSION); // another machine hosting: a session without the game
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession mine = OnlineSession.Create(VERSION);
            OnlineGame.Attach(mine);
            mine.JoinDirect(THIS_COMPUTER, PORT);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Slot(1) != null && Slot(1).IsLocal && Slot(0) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Slot(1) != null && Slot(1).IsLocal, "this machine's player moved to seat 2");
            ReportRecorder reports = new ReportRecorder(host.Match);
            CueReportRecorder reported = new CueReportRecorder(host.Match);

            // The host's match comes up here: the tutorial (this machine's player finishes it and waits in the city), then
            // the first wave
            host.Match.RequestLoad(MatchScene.Game);
            deadline = Time.realtimeSinceStartup + LOADING;
            while (reports.Machines.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Match.RequestShow(MatchScene.Game);
            host.Match.SendState(GameState.StartingCutscene);
            host.Match.SendState(GameState.MainLoop);
            deadline = Time.realtimeSinceStartup + 60;
            while (!(ActiveScene() == GAME && State() == GameState.MainLoop) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.MainLoop, State(), "the host's match is up here");
            host.Match.SendState(GameState.Tutorial);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Tutorial && Time.realtimeSinceStartup < deadline)
                yield return null;
            FinishTutorialInTheCity(1);
            host.Match.SendState(GameState.Begin);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (State() != GameState.Begin && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(GameState.Begin, State(), "the first wave, as the host says");
            Vector3 spot = CitySpot();
            PutBallAt(1, spot);
            Vector3 beside = spot + Vector3.right * 5f;
            ShareAt(ScooterInSeat(host, 0), beside);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!IsAt(0, beside) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(IsAt(0, beside), "the host's scooter is beside this machine's");

            // This machine's player falls in the water: the host hears it fall, then where it rises
            Respawn respawn = RespawnOf(1);
            respawn.LastGroundedPos = spot;
            respawn.StartRespawnCoroutine();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (reported.Cues.Count < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(1, reported.Cues.Count, "the host hears it fall");
            Assert.AreEqual(mine.Network.LocalClientId, reported.Machine);
            Assert.AreEqual(1, reported.Seats[0]);
            Assert.AreEqual(ScooterCue.Of(CueKind.Death), reported.Cues[0]);
            int far = FarthestPointFrom(spot);
            host.Match.SendRespawn(1, far);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (reported.Cues.Count < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, reported.Cues.Count, "then where it rises");
            Assert.AreEqual(ScooterCue.Rise(far), reported.Cues[1], "the host's point");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (respawn.IsRespawning && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsFalse(respawn.IsRespawning, "risen");
            PutBallAt(1, spot); // back beside the host's scooter

            // The host's player falls in 5 m away: its sound here, then its gravestone and sparkle at its point
            host.Match.SendCue(0, ScooterCue.Of(CueKind.Death));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!Played(PoolOf(0), "death") && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(Played(PoolOf(0), "death"), "the host's player's fall sounds here");
            int theirs = RespawnManager.Instance.IndexOf(RespawnManager.Instance.GetRespawnPoint(spot));
            Assert.AreNotEqual(far, theirs, "not this machine's player's point");
            RespawnPoint point = RespawnManager.Instance.PointAt(theirs);
            Pose grave = Respawn.GraveAt(point, RespawnOf(0).tombstoneOffset);
            host.Match.SendCue(0, ScooterCue.Rise(theirs));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!GraveStandsAt(grave.position) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(GraveStandsAt(grave.position), "their gravestone stands at their point");
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!SparkleAt(point.PlayerSpawn) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(SparkleAt(point.PlayerSpawn), "and they rise from it with the reborn sparkle");

            // A rise at a point this scene doesn't have shows nothing, and breaks nothing
            int graves = Object.FindObjectsOfType<RespawnGravestone>().Length;
            host.Match.SendCue(0, ScooterCue.Rise(9999));
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            Assert.LessOrEqual(Object.FindObjectsOfType<RespawnGravestone>().Length, graves, "no new gravestone");

            // This machine's player's d-pad emote: their ghost plays it (left: Raspberry), and the host hears it
            PlayerEmotes myGhost = GhostOf(1);
            Assert.IsNotNull(myGhost, "this machine's ghost has its emotes");
            myGhost.Pad(3);
            Assert.AreEqual(GhostAction.Raspberry, myGhost.Asked);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!reported.Cues.Contains(ScooterCue.Emote(3)) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(mine.Network.LocalClientId, reported.Machine);
            Assert.AreEqual(ScooterCue.Emote(3), reported.Cues[reported.Cues.Count - 1]);
            Assert.AreEqual(1, reported.Seats[reported.Seats.Count - 1], "for its own seat");

            // The host's player's d-pad emote plays on their ghost here (right: Taunt)
            PlayerEmotes hostGhost = GhostOf(0);
            Assert.IsNotNull(hostGhost, "the host's ghost has its emotes here");
            int asked = hostGhost.AskCount;
            host.Match.SendCue(0, ScooterCue.Emote(1));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!AskedSince(hostGhost, asked, GhostAction.Taunt) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AskedSince(hostGhost, asked, GhostAction.Taunt), "the host's ghost taunts here");

            // The host says a player was bumped hard: whoever it is sees stars here, this machine's own player too
            int myAsked = myGhost.AskCount;
            host.Match.SendCue(1, ScooterCue.Of(CueKind.Dizzy));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!AskedSince(myGhost, myAsked, GhostAction.SeeingStars) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AskedSince(myGhost, myAsked, GhostAction.SeeingStars), "this machine's ghost sees stars");
            asked = hostGhost.AskCount;
            host.Match.SendCue(0, ScooterCue.Of(CueKind.Dizzy));
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!AskedSince(hostGhost, asked, GhostAction.SeeingStars) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AskedSince(hostGhost, asked, GhostAction.SeeingStars), "the host's ghost sees stars here");

            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            mine.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}
