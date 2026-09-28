using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Driving together, in the real menu scene on this computer (127.0.0.1).
    /// - This machine hosts with the game.
    /// - Another machine is a session without the game: its scooter moves only when the test shares a pose for it, as
    ///   its own OnlineDriving would.
    /// - Another machine's scooter stays put until its owner shares a pose, then goes where its owner has it, showing
    ///   what it does. This machine's scooter reaches the other machine.
    /// Enters Play Mode (about 20 s each). No lambda captures a local: after EnterPlayMode even assigning one throws
    /// </summary>
    public class OnlineDrivingNetworkTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const string THIS_COMPUTER = "127.0.0.1";
        const ushort PORT = 7795;
        const string VERSION = "test";
        const float WAIT = 15f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            GameAuthority.Role = NetworkRole.Offline;
            SceneFlow.Current = null;
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static PlayerSlot Slot(int index)
        {
            return PlayerInstantiate.Instance.Roster[index];
        }

        static BallDriving ScooterIn(int slot)
        {
            return Slot(slot).Player.GetComponentInChildren<BallDriving>(true);
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

        [UnityTest]
        public IEnumerator AnotherMachinesScooter_StaysPut_UntilItsOwnerSharesAPose_ThenFollowsIt()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
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
            while (!(Slot(1) != null && ScooterInSeat(other, 1) != null) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsNotNull(Slot(1), "their scooter is in seat 2");
            BallDriving shown = ScooterIn(1);
            Vector3 podium = shown.Sphere.transform.position;

            // Their machine hasn't shared a pose yet: their scooter stays on its podium, never at the world's origin
            float until = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < until)
                yield return null;
            Assert.Less(Vector3.Distance(podium, shown.Sphere.transform.position), 0.01f, "on its podium");

            // Their machine shares a pose every frame, as its OnlineDriving would: a tier-1 drift, off the podium
            OnlineScooter theirs = ScooterInSeat(other, 1);
            ScooterPose pose = new ScooterPose
            {
                Ball = podium + new Vector3(4, 0, 2),
                Heading = 90f,
                ModelPosition = podium + new Vector3(4, -0.9f, 2),
                ModelRotation = Quaternion.Euler(-15, 95, 0),
            };
            DriveFlags drifting = new DriveFlags(false, true, false, 1, true, false);
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(Vector3.Distance(pose.Ball, shown.Sphere.transform.position) < 0.05f && shown.Drifting)
                && Time.realtimeSinceStartup < deadline)
            {
                theirs.Share(pose, drifting);
                yield return null;
            }

            Assert.Less(Vector3.Distance(pose.Ball, shown.Sphere.transform.position), 0.05f, "the ball");
            Assert.Less(Vector3.Distance(pose.Ball - new Vector3(0, BallDriving.SCOOTER_BELOW_BALL, 0), shown.transform.position), 0.05f, "the scooter");
            Assert.AreEqual(90f, shown.transform.eulerAngles.y, 1f, "its heading");
            Assert.Less(Vector3.Distance(pose.ModelPosition, shown.ScooterModel.position), 0.05f, "the model");
            Assert.IsTrue(shown.Drifting, "what it's doing");

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
        public IEnumerator ThisMachinesScooter_ReachesTheOtherMachine()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
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
            while (ScooterInSeat(other, 0) == null && Time.realtimeSinceStartup < deadline)
                yield return null;
            BallDriving mine = ScooterIn(0);
            OnlineScooter onOther = ScooterInSeat(other, 0);

            // This machine shares its scooter every frame, and the other machine has it where it is
            deadline = Time.realtimeSinceStartup + WAIT;
            while (!(onOther.HasPose && Vector3.Distance(mine.Sphere.transform.position, onOther.Pose.Ball) < 0.05f)
                && Time.realtimeSinceStartup < deadline)
                yield return null;

            Assert.IsTrue(onOther.HasPose, "this machine shares its scooter");
            Assert.Less(Vector3.Distance(mine.Sphere.transform.position, onOther.Pose.Ball), 0.05f, "where it is");
            Assert.AreEqual(mine.transform.eulerAngles.y, onOther.Pose.Heading, 1f, "its heading");
            Assert.Less(Vector3.Distance(mine.ScooterModel.position, onOther.Pose.ModelPosition), 0.05f, "its model");

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
    }
}
