using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Bots phase G1, for real: a CPU driver given Player 1's scooter in a local match heads for an order and picks it
    /// up. Enters Play Mode and loads the game scene (a minute or two). No lambda captures a local: after EnterPlayMode
    /// even assigning one throws
    /// </summary>
    public class BotDriverPlayTests
    {
        const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
        const float LOADING = 90f;
        const float TO_PICK_UP = 90f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        // Not in the suite yet: driving straight at an order, the bot gets stuck behind buildings (and boosting into one
        // lifts the scooter straight up). It needs the scooter NavMesh (EDITOR-TODO.md, Bots) to path round them. Run it
        // by hand once that's baked
        [UnityTest, Explicit("Needs the scooter NavMesh bake: EDITOR-TODO.md, Bots")]
        public IEnumerator ABot_DrivesToAnOrder_AndPicksItUp()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;

            // Into a match the quick way: a rematch skips the loading screen's A and the tutorial
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && State() != GameState.MainLoop && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(State() == GameState.Begin || State() == GameState.MainLoop, "in the match");

            BallDriving scooter = PlayerInstantiate.Instance.Roster[0].Player.GetComponentInChildren<BallDriving>();
            BotDriver bot = scooter.gameObject.AddComponent<BotDriver>();
            bot.Drive(scooter);
            OrderHandler handler = OrderSync.HandlerIn(0);
            Vector3 start = scooter.transform.position;

            deadline = Time.realtimeSinceStartup + TO_PICK_UP;
            float travelled = 0f;
            float closest = float.MaxValue;
            Vector3 last = start;
            float nextTrace = 0f;
            string trace = "";
            while (handler.GetBestOrder() == null && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup > nextTrace)
                {
                    nextTrace = Time.realtimeSinceStartup + 3f;
                    trace += "[" + scooter.transform.position.ToString("0") + (bot.Target != null ? " to " + bot.Target.Value.ToString("0") : "") + " state " + State() + "] ";
                }
                travelled += Vector3.Distance(last, scooter.transform.position);
                last = scooter.transform.position;
                if (bot.Target != null)
                    closest = Mathf.Min(closest, Vector3.Distance(new Vector3(bot.Target.Value.x, 0, bot.Target.Value.z), new Vector3(last.x, 0, last.z)));
                yield return null;
            }

            string orders = "scooter y " + last.y.ToString("0") + "; orders: ";
            foreach (Order order in OrderManager.Instance.ActiveOrders)
                if (order != null)
                    orders += order.name + " act=" + order.IsActive + " pick=" + order.CanPickup + " y=" + order.transform.position.y.ToString("0") + " live=" + order.gameObject.activeInHierarchy + "; ";
            Debug.Log("[BOT] " + orders);
            Assert.IsNotNull(bot.Target, "it found something to do");
            Assert.Greater(travelled, 10f, "it drove");
            Assert.IsNotNull(handler.GetBestOrder(), "it picked up an order (drove " + travelled.ToString("0") + " m, came within " + closest.ToString("0.0") + " m of its target, target height " + (bot.Target != null ? (bot.Target.Value.y - last.y).ToString("0.0") : "-") + ") " + orders + " TRACE " + trace);
        }
    }
}
