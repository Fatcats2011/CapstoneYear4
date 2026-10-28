using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DoA.Tests
{
    /// <summary>
    /// Bots phase G1, for real: a CPU driver given Player 1's scooter in a local match finds a path on the Scooter NavMesh
    /// to an order, follows it and picks the order up. Enters Play Mode and loads the game scene (a minute or two). No lambda captures a local: after EnterPlayMode
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
            HouseRules.Playing = HouseRules.Normal;
            if (Application.isPlaying)
                yield return new ExitPlayMode();

            EditorSceneManager.playModeStartScene = null;
            TestPlayers.RemoveAll();
            SceneFlow.Current = null;
        }

        // How it's driving, for the trace: speed, and what the bot presses
        static string Inputs(BallDriving scooter, BotDriver bot)
        {
            IDriveInput input = bot;
            Rigidbody ball = scooter.Sphere.GetComponent<Rigidbody>();
            Respawn respawn = scooter.respawn;
            return (respawn != null && respawn.IsRespawning ? " RESPAWNING" : "") + " speed " + ball.velocity.magnitude.ToString("0.0") + " steer " + input.Steer.ToString("0.0") + " gas " + input.Accelerate.ToString("0.0") + " brake " + input.Brake.ToString("0.0") + " fps " + (1f / Mathf.Max(Time.smoothDeltaTime, 1e-4f)).ToString("0") + " heading " + scooter.transform.eulerAngles.y.ToString("0");
        }

        // What stands between the scooter and where it's steering, for the trace
        static string Blocker(BallDriving scooter, BotDriver bot)
        {
            Vector3 from = scooter.transform.position + Vector3.up * 0.5f;
            Vector3 to = bot.Aim + Vector3.up * 0.5f;
            if (!Physics.Linecast(from, to, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore))
                return "";
            return " blocked " + hit.distance.ToString("0.0") + " m by " + hit.collider.name + " (" + LayerMask.LayerToName(hit.collider.gameObject.layer) + ", under " + (hit.collider.transform.parent != null ? hit.collider.transform.parent.name : "-") + ")";
        }

        static GameState State()
        {
            return GameManager.Instance == null ? GameState.Default : GameManager.Instance.MainState;
        }

        [UnityTest]
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
            bool pathed = false;
            while (handler.GetBestOrder() == null && Time.realtimeSinceStartup < deadline)
            {
                if (Time.realtimeSinceStartup > nextTrace)
                {
                    nextTrace = Time.realtimeSinceStartup + 1f;
                    trace += "[" + scooter.transform.position.ToString("0") + (bot.Target != null ? " to " + bot.Target.Value.ToString("0") : "") + " " + bot.PathReport + " state " + State() + Blocker(scooter, bot) + Inputs(scooter, bot) + "] ";
                }
                pathed |= bot.HasPath;
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
            Assert.IsTrue(pathed, "it found a path on the Scooter NavMesh");
            Assert.Greater(travelled, 10f, "it drove");
            Assert.IsNotNull(handler.GetBestOrder(), "it picked up an order (drove " + travelled.ToString("0") + " m, came within " + closest.ToString("0.0") + " m of its target, target height " + (bot.Target != null ? (bot.Target.Value.y - last.y).ToString("0.0") : "-") + ") " + orders + " TRACE " + trace);
        }

        const float WATCH = 1.5f; // a bot chooses afresh every half second: three choices

        // Gives a player a free order to carry; false with none free
        static bool GiveOrder(OrderHandler player)
        {
            if (player.GetBestOrder() != null)
                return true;
            foreach (Order order in OrderManager.Instance.ActiveOrders)
            {
                if (order != null && order.IsActive && order.CanPickup && order.PlayerHolding == null)
                {
                    player.AddOrder(order);
                    return player.GetBestOrder() != null;
                }
            }
            return false;
        }

        // Personalities in a real match (one scene load for all four checks): Player 1 is the bot, Player 2 carries an
        // order. The thief chases the carrier; the courier doesn't; a thief whose victim lets go goes back to orders; with
        // steals off, the thief only collects
        [UnityTest]
        public IEnumerator Personalities_ChaseCarriers_OnlyAsTheirKindAndTheRulesAllow()
        {
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MENU_SCENE);
            yield return new EnterPlayMode();
            LogAssert.ignoreFailingMessages = true;
            float deadline = Time.realtimeSinceStartup + 60;
            while (State() != GameState.Menu && Time.realtimeSinceStartup < deadline)
                yield return null;
            // The first pad joins on the title screen, the second in player select (OnlineSeatTests)
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 1 && Time.realtimeSinceStartup < deadline)
                yield return null;
            GameManager.Instance.SetGameState(GameState.PlayerSelect);
            TestPlayers.Add();
            deadline = Time.realtimeSinceStartup + 10;
            while (PlayerInstantiate.Instance.PlayerCount < 2 && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.AreEqual(2, PlayerInstantiate.Instance.PlayerCount, "two players in");
            SceneFlow.Current.Rematch();
            deadline = Time.realtimeSinceStartup + LOADING;
            while (State() != GameState.Begin && State() != GameState.MainLoop && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(State() == GameState.Begin || State() == GameState.MainLoop, "in the match");
            deadline = Time.realtimeSinceStartup + 10;
            while (OrderManager.Instance.ActiveOrders.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;

            BallDriving scooter = PlayerInstantiate.Instance.Roster[0].Player.GetComponentInChildren<BallDriving>();
            BotDriver bot = scooter.gameObject.AddComponent<BotDriver>();
            bot.Kind = BotKind.Thief;
            bot.Drive(scooter);
            OrderHandler carrier = null;
            foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
                if (slot.Index != 0 && slot.Player != null)
                    carrier = slot.Player.GetComponentInChildren<OrderHandler>(true);
            Assert.IsNotNull(carrier, "a second player in the match (" + PlayerInstantiate.Instance.PlayerCount + " players)");

            // The thief goes after the carrier (once an order is out to take: the first wave may not have spawned yet)
            deadline = Time.realtimeSinceStartup + 30;
            while (!GiveOrder(carrier) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(GiveOrder(carrier), "player 2 carries an order (" + OrderManager.Instance.ActiveOrders.Count + " orders out, state " + State() + ")");
            bool chased = false;
            deadline = Time.realtimeSinceStartup + WATCH;
            while (!chased && Time.realtimeSinceStartup < deadline)
            {
                chased = bot.Victim == carrier;
                yield return null;
            }
            Assert.IsTrue(chased, "the thief chases the carrier");

            // The carrier lets go: the thief goes back to orders
            carrier.ReleaseOrders();
            yield return new WaitForSeconds(1f);
            Assert.IsNull(bot.Victim, "nobody to rob");
            bool anyFree = false;
            foreach (Order order in OrderManager.Instance.ActiveOrders)
                anyFree |= order != null && order.IsActive && order.CanPickup && order.PlayerHolding == null;
            if (anyFree)
                Assert.IsNotNull(bot.Target, "it has an order to go for");

            // The courier never chases
            bot.Kind = BotKind.Courier;
            deadline = Time.realtimeSinceStartup + 30;
            while (!GiveOrder(carrier) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(GiveOrder(carrier), "player 2 carries an order again");
            chased = false;
            deadline = Time.realtimeSinceStartup + WATCH;
            while (Time.realtimeSinceStartup < deadline)
            {
                chased |= bot.Victim != null;
                yield return null;
            }
            Assert.IsFalse(chased, "the courier doesn't steal");

            // Steals off: the thief only collects
            HouseRules.Playing = new HouseRules(MatchLength.Normal, false, true);
            bot.Kind = BotKind.Thief;
            deadline = Time.realtimeSinceStartup + 30;
            while (!GiveOrder(carrier) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.IsTrue(GiveOrder(carrier), "player 2 still carries an order");
            chased = false;
            deadline = Time.realtimeSinceStartup + WATCH;
            while (Time.realtimeSinceStartup < deadline)
            {
                chased |= bot.Victim != null;
                yield return null;
            }
            Assert.IsFalse(chased, "no steals in a no-steals match");
        }
    }
}
