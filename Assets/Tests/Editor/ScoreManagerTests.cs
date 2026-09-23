using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class ScoreManagerTests
    {
        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
        }

        static OrderHandler AddOrders(GameObject player)
        {
            GameObject orders = new GameObject("Orders");
            orders.transform.SetParent(player.transform);
            return orders.AddComponent<OrderHandler>();
        }

        [Test]
        public void UpdateOrderHandlers_ListsEveryPlayersOrders_IncludingOnlinePlayers()
        {
            ScoreManager scores = objects.Add<ScoreManager>();
            PlayerRoster roster = new PlayerRoster();
            OrderHandler local = AddOrders(roster.JoinLocal(objects.Add<PlayerInput>()).Player);
            OrderHandler online = AddOrders(roster.JoinRemote(objects.NewGameObject("Online player"), 3).Player);

            scores.UpdateOrderHandlers(roster);

            Assert.AreSame(local, scores.GetHandlerOfIndex(0));
            Assert.AreSame(online, scores.GetHandlerOfIndex(1));
            Assert.IsNull(scores.GetHandlerOfIndex(2));
        }
    }
}
