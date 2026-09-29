using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Every machine's scooter on every machine (OnlineScooter), with hosts and clients on this computer (127.0.0.1) in one
    /// empty Play Mode scene. The host spawns one per machine in its seat, parked until its owner shares a pose. A pose
    /// and the flags reach the other machines, only from the owner. A long move jumps, and so does any move while the
    /// scooter is hidden (a respawn). A scooter goes with its machine.
    /// A few seconds each. Each test enters Play Mode itself (never from a helper: the domain reloads there). No lambda
    /// here captures a local: after EnterPlayMode even assigning a captured local throws
    /// </summary>
    public class OnlineScootersNetworkTests
    {
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7794;
        const float WAIT = 10f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            GameAuthority.Role = NetworkRole.Offline;
        }

        static OnlineScooter ScooterInSeat(OnlineSession session, int seat)
        {
            foreach (OnlineScooter scooter in session.Scooters)
            {
                if (scooter.Seat == seat)
                    return scooter;
            }
            return null;
        }

        static bool BothSeeBothScooters(OnlineSession host, OnlineSession client)
        {
            return ScooterInSeat(host, 0) != null && ScooterInSeat(host, 1) != null
                && ScooterInSeat(client, 0) != null && ScooterInSeat(client, 1) != null;
        }

        // A pose around a ball position: the model below it, leaning into a turn
        static ScooterPose PoseAt(Vector3 ball, float heading)
        {
            return new ScooterPose
            {
                Ball = ball,
                Heading = heading,
                ModelPosition = ball - new Vector3(0, 0.9f, 0),
                ModelRotation = Quaternion.Euler(-10, heading, 5),
            };
        }

        [UnityTest]
        public IEnumerator EveryMachine_HasAScooterInItsSeat_ParkedUntilItsOwnerSharesAPose()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(2, host.Scooters.Count, "the host has both scooters");
            Assert.AreEqual(2, client.Scooters.Count, "so does the client");
            Assert.IsTrue(ScooterInSeat(host, 0).IsOwner, "the host's own scooter sits in seat 0");
            Assert.IsFalse(ScooterInSeat(host, 1).IsOwner, "on the host, seat 1 is another machine's");
            Assert.IsTrue(ScooterInSeat(client, 1).IsOwner, "the client's own sits in seat 1");
            Assert.IsFalse(ScooterInSeat(host, 1).HasPose, "parked until its owner shares a pose");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AScootersPoseAndFlags_ReachTheOtherMachine_OnlyFromItsOwner()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineScooter mine = ScooterInSeat(client, 1);
            OnlineScooter onHost = ScooterInSeat(host, 1);
            ScooterPose pose = PoseAt(new Vector3(12, 3, -40), 135f);
            DriveFlags drifting = new DriveFlags(false, true, true, 3, true, false);

            // Its owner shares it every frame, as OnlineDriving does
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onHost.HasPose && Vector3.Distance(pose.Ball, onHost.Pose.Ball) < 0.05f && onHost.Flags.Value == drifting.Value)
                && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(pose, drifting);
                yield return null;
            }

            Assert.Less(Vector3.Distance(pose.Ball, onHost.Pose.Ball), 0.05f, "the ball");
            Assert.AreEqual(135f, onHost.Pose.Heading, 0.5f, "the heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, onHost.Pose.ModelPosition), 0.05f, "the model");
            Assert.Less(Quaternion.Angle(pose.ModelRotation, onHost.Pose.ModelRotation), 1f, "the model's lean");
            Assert.AreEqual(drifting.Value, onHost.Flags.Value, "what it's doing");

            // Another machine can't move it
            onHost.Share(PoseAt(Vector3.zero, 0f), new DriveFlags(0));
            float until = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.Less(Vector3.Distance(pose.Ball, onHost.Pose.Ball), 0.05f, "only its owner moves it");
            Assert.AreEqual(drifting.Value, onHost.Flags.Value, "only its owner sets what it's doing");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator ALongMove_Jumps_InsteadOfSlidingAcrossTheMap()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineScooter mine = ScooterInSeat(client, 1);
            OnlineScooter onHost = ScooterInSeat(host, 1);
            Vector3 here = new Vector3(0, 2, 0);
            Vector3 there = new Vector3(200, 2, 0);

            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onHost.HasPose && Vector3.Distance(here, onHost.Pose.Ball) < 0.05f) && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(here, 0f), new DriveFlags(0));
                yield return null;
            }
            Assert.Less(Vector3.Distance(here, onHost.Pose.Ball), 0.05f, "at the first spot");

            // A respawn or a spawn point: 200 m in one frame. On the host it never shows in between
            float worst = 0f;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Vector3.Distance(there, onHost.Pose.Ball) > 0.05f && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(there, 0f), new DriveFlags(0));
                yield return null;
                Vector3 shown = onHost.Pose.Ball;
                worst = Mathf.Max(worst, Mathf.Min(Vector3.Distance(shown, here), Vector3.Distance(shown, there)));
            }

            Assert.Less(Vector3.Distance(there, onHost.Pose.Ball), 0.05f, "it got there");
            Assert.Less(worst, 1f, "a jump: never shown in between");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AHiddenScootersMoves_Jump_EvenShortOnes()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;
            OnlineScooter mine = ScooterInSeat(client, 1);
            OnlineScooter onHost = ScooterInSeat(host, 1);
            DriveFlags hidden = new DriveFlags(false, false, false, 0, false, false, true);
            Vector3 here = new Vector3(0, 2, 0);
            Vector3 there = new Vector3(5, 2, 0);

            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onHost.HasPose && Vector3.Distance(here, onHost.Pose.Ball) < 0.05f && onHost.Flags.Hidden)
                && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(here, 0f), hidden);
                yield return null;
            }
            Assert.Less(Vector3.Distance(here, onHost.Pose.Ball), 0.05f, "at the first spot");
            Assert.IsTrue(onHost.Flags.Hidden, "hidden, as its rider is while its wisp flies");

            // 5 m, under the teleport distance: while it's hidden it still jumps, so it's where its owner has it when it
            // shows again
            float worst = 0f;
            deadline = Time.realtimeSinceStartup + WAIT;
            while (Vector3.Distance(there, onHost.Pose.Ball) > 0.05f && Time.realtimeSinceStartup < deadline)
            {
                mine.Share(PoseAt(there, 0f), hidden);
                yield return null;
                Vector3 shown = onHost.Pose.Ball;
                worst = Mathf.Max(worst, Mathf.Min(Vector3.Distance(shown, here), Vector3.Distance(shown, there)));
            }

            Assert.Less(Vector3.Distance(there, onHost.Pose.Ball), 0.05f, "it got there");
            Assert.Less(worst, 0.5f, "a jump: never shown in between");

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.PlayersIn > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator AScooter_GoesWithItsMachine()
        {
            EditorSceneManager.playModeStartScene = null;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            OnlineSession host = OnlineSession.Create("1.0.0");
            host.HostDirect(THIS_COMPUTER, PORT);
            OnlineSession client = OnlineSession.Create("1.0.0");
            client.JoinDirect(THIS_COMPUTER, PORT);
            float deadline = Time.realtimeSinceStartup + WAIT;
            while (!BothSeeBothScooters(host, client) && Time.realtimeSinceStartup < deadline)
                yield return null;

            LogCollector log = new LogCollector();
            log.MachinesLeave(); // Windows may report a leaving machine's closed port: see LogCollector.CLOSED_PORT
            client.Leave();
            deadline = Time.realtimeSinceStartup + WAIT;
            while (host.Scooters.Count > 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.AreEqual(1, host.Scooters.Count, "the client's scooter went with it");
            Assert.AreEqual(0, host.Scooters[0].Seat, "the host's stays");
            host.Leave();
            yield return null;
            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors while machines left:\n\n" + string.Join("\n\n", log.Problems));
        }
    }
}
