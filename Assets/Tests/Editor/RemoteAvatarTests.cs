using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Another machine's player on this one, in the real menu scene: their scooter without a view takes their seat,
    /// dressed as them; their readiness counts towards the countdown; and they leave. Enters Play Mode (about 10 s each)
    /// </summary>
    public class RemoteAvatarTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
        }

        static bool AtTitleScreen()
        {
            return GameManager.Instance != null && GameManager.Instance.MainState == GameState.Menu;
        }

        static bool CountingDown(PlayerInstantiate players)
        {
            return Reflect.GetField(players, "readyUpCountdown") != null;
        }

        static PlayerHatInformationSO AShowingHat(CustomizationSelector customization)
        {
            foreach (PlayerHatInformationSO hat in customization.Hats)
            {
                if (hat.displayHat)
                    return hat;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator RemotePlayer_TakesTheirSeat_DressedAsThem_WithoutErrors()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true; // the collector reports every problem at the end
            LogCollector log = new LogCollector();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(AtTitleScreen(), "the title screen");

            PlayerInstantiate players = PlayerInstantiate.Instance;
            OnlinePrefabs prefabs = OnlinePrefabs.Load();
            RemoteAvatar remote = RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null);
            PlayerSlot slot = players.AddRemotePlayer(remote.gameObject, 1, 5);

            Assert.IsNotNull(slot, "seated");
            Assert.AreEqual(1, slot.Index);
            Assert.IsFalse(slot.IsLocal);
            Assert.AreEqual(5UL, slot.OwnerClientId);
            Assert.AreEqual("P2", remote.name, "named after the seat, like a local player");
            Assert.AreEqual(2, remote.Driving.playerIndex);
            Assert.AreEqual(11, remote.Driving.Sphere.layer, "player 2's ball layer");
            Assert.IsFalse(remote.Driving.enabled, "no controls here");
            Assert.IsTrue(remote.Driving.Sphere.GetComponent<Rigidbody>().isKinematic, "no physics here");
            GameObject[] podiums = (GameObject[])Reflect.GetField(players, "menuSpawnPositions");
            Assert.Less(Vector3.Distance(podiums[1].transform.position, remote.Driving.transform.position), 0.01f, "on seat 2's podium");

            CompanyInformation company = players.CompanyForSlot(1);
            Assert.AreSame(company, slot.Company);
            Assert.AreSame(company.scooterColorMaterial, remote.transform.Find(ScooterLook.SCOOTER_BODY).GetComponent<MeshRenderer>().sharedMaterial, "company colours");
            Assert.AreSame(company.scooterDecalMaterial, remote.transform.Find(ScooterLook.LOGO).GetComponent<DecalProjector>().material, "company logo");

            CustomizationSelector customization = prefabs.LocalPlayerPrefab.GetComponentInChildren<CustomizationSelector>(true);
            PlayerColorInformationSO colour = customization.Colours[2];
            remote.ShowColour(colour);
            Assert.AreSame(colour.colorMaterial, remote.transform.Find(ScooterLook.GHOST).GetComponent<SkinnedMeshRenderer>().sharedMaterials[0], "ghost colour");
            Assert.AreSame(colour.colorMaterial, remote.transform.Find(ScooterLook.EYELID_LEFT).GetComponent<MeshRenderer>().sharedMaterial, "eyelids");
            PlayerHatInformationSO hat = AShowingHat(customization);
            remote.ShowHat(hat);
            Transform hatObject = remote.transform.Find(ScooterLook.HAT);
            Assert.IsTrue(hatObject.gameObject.activeSelf, "hat on");
            Assert.AreSame(hat.hatMesh, hatObject.GetComponent<MeshFilter>().sharedMesh, "the hat");

            // Player select and back, driving indicators and placings refreshed: nothing reaches for a view it hasn't got
            GameManager.Instance.ApplyGameState(GameState.PlayerSelect);
            players.PlayerUpdateDrivingIndicators();
            ScoreManager.Instance.UpdatePlacement();
            for (float until = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < until;)
                yield return null;
            GameManager.Instance.ApplyGameState(GameState.Menu);
            yield return null;

            players.RemoveRemotePlayer(1);
            yield return null;
            Assert.IsNull(players.Roster[1], "seat free");
            Assert.IsTrue(remote == null, "scooter gone");

            log.Dispose();
            Assert.IsEmpty(log.Problems, "Errors:\n\n" + string.Join("\n\n", log.Problems));
        }

        [UnityTest]
        public IEnumerator RemotePlayersReadiness_CountsTowardsTheCountdown()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!AtTitleScreen() && Time.realtimeSinceStartup < deadline)
                yield return null;
            PlayerInstantiate players = PlayerInstantiate.Instance;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (players.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            OnlinePrefabs prefabs = OnlinePrefabs.Load();
            players.AddRemotePlayer(RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null).gameObject, 1, 5);

            players.ReadyUp(0);
            Assert.IsFalse(CountingDown(players), "waiting for player 2");
            players.SetRemoteReady(1, true);
            Assert.IsTrue(players.IsReady(1));
            Assert.IsTrue(CountingDown(players), "everyone's ready");
            players.SetRemoteReady(1, false);
            Assert.IsFalse(CountingDown(players), "player 2 isn't ready any more");

            // A player from another machine joining mid-countdown stops it, like a local join
            players.SetRemoteReady(1, true);
            players.AddRemotePlayer(RemoteAvatar.Create(prefabs.RemoteAvatarPrefab, null).gameObject, 2, 6);
            Assert.IsFalse(CountingDown(players), "player 3 joined");

            // They leave again: everyone left is ready, so it counts down
            players.RemoveRemotePlayer(2);
            Assert.IsTrue(CountingDown(players), "players 1 and 2 are ready");

            players.SetRemoteReady(1, false); // stop before it loads the match
            Assert.IsFalse(CountingDown(players));
            yield return null;
        }
    }
}
