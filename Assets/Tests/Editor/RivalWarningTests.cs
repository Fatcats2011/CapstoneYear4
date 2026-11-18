using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DoA.Tests
{
    /// <summary>
    /// A carrier's rival warning (RivalWarning): one small red mark at the edge of their view, where a rival who could
    /// steal is; steady when near, pulsing when charging, a rumble on the first frame of charging, and who counts as a threat
    /// </summary>
    public class RivalWarningTests
    {
        readonly TestObjects objects = new TestObjects();
        static float now;

        static float FakeClock() { return now; }
        static bool MatchOn() { return true; }
        static bool MatchOff() { return false; }

        [SetUp]
        public void SetUp()
        {
            now = 0f;
            PlayerInstantiate.instance = objects.Add<PlayerInstantiate>();
            RivalWarning.MatchOn = MatchOn;
        }

        [TearDown]
        public void TearDown()
        {
            Comfort.ReduceMotion = false;
            RivalWarning.MatchOn = null;
            PlayerInstantiate.instance = null;
            objects.DestroyAll();
        }

        // A player in the next free seat with a HUD canvas of their own, their ball at a place
        (OrderHandler handler, NumberHandler hud) NewPlayer(string name, Vector3 at)
        {
            GameObject player = objects.NewGameObject(name);
            PlayerInstantiate.Instance.Roster.JoinLocal(player.AddComponent<PlayerInput>());
            GameObject control = new GameObject("Control");
            control.transform.SetParent(player.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();
            GameObject sphere = new GameObject("Sphere");
            sphere.transform.SetParent(player.transform);
            sphere.transform.position = at;
            handler.ball.sphere = sphere;
            GameObject canvas = new GameObject("Driving Canvas", typeof(RectTransform), typeof(Canvas));
            canvas.transform.SetParent(player.transform);
            return (handler, canvas.AddComponent<NumberHandler>());
        }

        static void Hold(OrderHandler handler, int count)
        {
            if (count > 0)
                handler.order1 = handler.gameObject.AddComponent<Order>();
            if (count > 1)
                handler.order2 = handler.gameObject.AddComponent<Order>();
        }

        RivalWarning NewWarning(out Image mark)
        {
            var player = NewPlayer("Viewer", Vector3.zero);
            RivalWarning warning = RivalWarning.AddTo(player.handler, player.hud);
            warning.Clock = FakeClock;
            mark = player.hud.transform.root.GetComponentInChildren<Image>(true);
            return warning;
        }

        [Test]
        public void AddTo_WithoutACanvas_IsNull()
        {
            GameObject player = objects.NewGameObject("Player 1");
            OrderHandler handler = player.AddComponent<OrderHandler>();
            NumberHandler loose = objects.Add<NumberHandler>();

            Assert.IsNull(RivalWarning.AddTo(handler, loose));
            Assert.IsNull(RivalWarning.AddTo(handler, null));
        }

        [Test]
        public void TheMark_IsOneSmallRedImage_NotHitByTheMouse()
        {
            RivalWarning warning = NewWarning(out Image mark);

            Assert.IsNotNull(mark);
            Assert.AreEqual(1, mark.transform.root.GetComponentsInChildren<Image>(true).Length);
            Assert.IsFalse(mark.raycastTarget);
            Assert.IsNotNull(mark.sprite);
            Assert.AreEqual(RivalWarning.SIZE, mark.rectTransform.sizeDelta.x);
            Assert.AreEqual(1f, mark.color.r, 0.001f);
            Assert.AreEqual(0.25f, mark.color.g, 0.001f);
            Assert.AreEqual(0.2f, mark.color.b, 0.001f);
        }

        [Test]
        public void SeeNothing_ShowsNothing()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(Threat.Nothing);

            Assert.AreEqual(ThreatLevel.None, warning.ShownLevel);
            Assert.AreEqual(0f, warning.ShownAlpha);
            Assert.AreEqual(0f, mark.color.a);
        }

        [Test]
        public void ANearRivalOnTheLeft_ShowsSteadyAtTheLeftEdge_PointingLeft()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Left, 20f));

            Assert.AreEqual(ThreatLevel.Near, warning.ShownLevel);
            Assert.AreEqual(RivalWarning.LEFT, warning.ShownAt);
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, warning.ShownAlpha, 0.001f);
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, mark.color.a, 0.001f);
            Assert.AreEqual(90f, Mathf.Repeat(mark.rectTransform.eulerAngles.z, 360f), 0.01f, "left edge points left");
        }

        [Test]
        public void TheRightAndBehindMarks_SitAndPointOutward()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Right, 20f));
            Assert.AreEqual(RivalWarning.RIGHT, warning.ShownAt);
            Assert.AreEqual(270f, Mathf.Repeat(mark.rectTransform.eulerAngles.z, 360f), 0.01f, "right edge points right");

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Behind, 20f));
            Assert.AreEqual(RivalWarning.BEHIND, warning.ShownAt);
            Assert.AreEqual(180f, Mathf.Repeat(mark.rectTransform.eulerAngles.z, 360f), 0.01f, "behind points down");
        }

        [Test]
        public void ARivalInView_ShowsNothing()
        {
            RivalWarning warning = NewWarning(out Image mark);
            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Left, 20f));

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.None, 20f));

            Assert.AreEqual(ThreatLevel.None, warning.ShownLevel);
            Assert.AreEqual(0f, mark.color.a);
        }

        [Test]
        public void ACharging_PulsesFromSolid()
        {
            RivalWarning warning = NewWarning(out Image mark);
            now = 10f;
            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Behind, 10f));
            Assert.AreEqual(1f, warning.ShownAlpha, 0.001f, "solid as it comes up");

            now = 10f + RivalWarningRules.PULSE_SECONDS / 2f;
            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Behind, 10f));
            Assert.AreEqual(RivalWarningRules.NEAR_ALPHA, warning.ShownAlpha, 0.001f, "dimmest half a pulse later");
        }

        [Test]
        public void EnteringCharging_RumblesOnce_AndAgainAfterLeavingIt()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Left, 20f));
            Assert.AreEqual(0, warning.Rumbles);

            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Left, 15f));
            Assert.AreEqual(1, warning.Rumbles);

            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Left, 10f));
            Assert.AreEqual(1, warning.Rumbles, "staying charging doesn't shake again");

            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Left, 20f));
            now += RivalWarningRules.RUMBLE_GAP_SECONDS;
            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Left, 15f));
            Assert.AreEqual(2, warning.Rumbles);
        }

        [Test]
        public void ARivalFlickeringBetweenNearAndCharging_RumblesOnce()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Left, 15f));
            now += 0.1f;
            warning.See(new Threat(ThreatLevel.Near, ThreatSide.Left, 15f));
            now += 0.1f;
            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Left, 14f));

            Assert.AreEqual(1, warning.Rumbles, "a wobble in the distance isn't a new charge");
        }

        [Test]
        public void ChargingComingUpFromNothing_Rumbles()
        {
            RivalWarning warning = NewWarning(out Image mark);

            warning.See(new Threat(ThreatLevel.Charging, ThreatSide.Right, 12f));

            Assert.AreEqual(1, warning.Rumbles);
        }

        [Test]
        public void Look_CarrierHoldingNothing_IsNothing()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var rival = NewPlayer("Rival", new Vector3(20f, 0f, 0f));
            Transform camera = objects.NewGameObject("Camera").transform;

            Threat threat = RivalWarning.Look(carrier.handler, camera, new[] { carrier.handler, rival.handler }, new Dictionary<OrderHandler, float>());

            Assert.AreEqual(ThreatLevel.None, threat.Level);
            Assert.AreEqual(ThreatSide.None, threat.Side);
        }

        [Test]
        public void Look_ARivalToTheRight_IsNearOnTheRight()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var rival = NewPlayer("Rival", new Vector3(20f, 0f, 0f));
            Hold(carrier.handler, 1);
            Transform camera = objects.NewGameObject("Camera").transform; // looks along +z

            Threat threat = RivalWarning.Look(carrier.handler, camera, new[] { carrier.handler, rival.handler }, new Dictionary<OrderHandler, float>());

            Assert.AreEqual(ThreatLevel.Near, threat.Level);
            Assert.AreEqual(ThreatSide.Right, threat.Side);
            Assert.AreEqual(20f, threat.Distance, 0.001f);
        }

        [Test]
        public void Look_IgnoresTheCarrierItself_AndNullEntries()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            Hold(carrier.handler, 1);
            Transform camera = objects.NewGameObject("Camera").transform;

            Threat threat = RivalWarning.Look(carrier.handler, camera, new OrderHandler[] { null, carrier.handler }, new Dictionary<OrderHandler, float>());

            Assert.AreEqual(ThreatLevel.None, threat.Level);
        }

        [Test]
        public void Look_IgnoresARivalWithNoRoom()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var rival = NewPlayer("Rival", new Vector3(20f, 0f, 0f));
            Hold(carrier.handler, 1);
            Hold(rival.handler, 2);
            Transform camera = objects.NewGameObject("Camera").transform;

            Threat threat = RivalWarning.Look(carrier.handler, camera, new[] { carrier.handler, rival.handler }, new Dictionary<OrderHandler, float>());

            Assert.AreEqual(ThreatLevel.None, threat.Level);
        }

        [Test]
        public void Look_IgnoresAFarRival_AndNoMatch()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var far = NewPlayer("Far", new Vector3(50f, 0f, 0f));
            var near = NewPlayer("Near", new Vector3(20f, 0f, 0f));
            Hold(carrier.handler, 1);
            Transform camera = objects.NewGameObject("Camera").transform;

            Threat onlyFar = RivalWarning.Look(carrier.handler, camera, new[] { far.handler }, new Dictionary<OrderHandler, float>());
            Assert.AreEqual(ThreatLevel.None, onlyFar.Level);

            RivalWarning.MatchOn = MatchOff;
            Threat noMatch = RivalWarning.Look(carrier.handler, camera, new[] { near.handler }, new Dictionary<OrderHandler, float>());
            Assert.AreEqual(ThreatLevel.None, noMatch.Level);
        }

        [Test]
        public void Look_ABoostingRivalClosingIn_IsChargingAndRemembersTheDistance()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var rival = NewPlayer("Rival", new Vector3(20f, 0f, 0f));
            Hold(carrier.handler, 1);
            rival.handler.ball.boosting = true;
            Transform camera = objects.NewGameObject("Camera").transform;
            var last = new Dictionary<OrderHandler, float>();

            Threat first = RivalWarning.Look(carrier.handler, camera, new[] { rival.handler }, last);
            Assert.AreEqual(ThreatLevel.Near, first.Level, "no last distance: not yet closing");
            Assert.AreEqual(20f, last[rival.handler], 0.001f);

            rival.handler.ball.sphere.transform.position = new Vector3(15f, 0f, 0f);
            Threat second = RivalWarning.Look(carrier.handler, camera, new[] { rival.handler }, last);
            Assert.AreEqual(ThreatLevel.Charging, second.Level);
            Assert.AreEqual(15f, last[rival.handler], 0.001f);
        }

        [Test]
        public void Look_ARivalOnTheCarrier_OrNoCameraDirection_IsNoMark()
        {
            var carrier = NewPlayer("Carrier", Vector3.zero);
            var rival = NewPlayer("Rival", Vector3.zero);
            Hold(carrier.handler, 1);
            Transform camera = objects.NewGameObject("Camera").transform;

            Threat onTop = RivalWarning.Look(carrier.handler, camera, new[] { rival.handler }, new Dictionary<OrderHandler, float>());
            Assert.AreEqual(ThreatSide.None, onTop.Side);

            Threat noCamera = RivalWarning.Look(carrier.handler, null, new[] { rival.handler }, new Dictionary<OrderHandler, float>());
            Assert.AreEqual(ThreatSide.None, noCamera.Side);
        }

        [Test]
        public void Judge_WorksOutAThreatFromPlainValues()
        {
            Threat counted = RivalWarning.Judge(true, 20f, new Vector3(20f, 0f, 0f), Vector3.forward, true, true);
            Assert.AreEqual(ThreatLevel.Charging, counted.Level);
            Assert.AreEqual(ThreatSide.Right, counted.Side);

            Threat notCounted = RivalWarning.Judge(false, 20f, new Vector3(20f, 0f, 0f), Vector3.forward, true, true);
            Assert.AreEqual(ThreatLevel.None, notCounted.Level);

            Threat tooFar = RivalWarning.Judge(true, 40f, new Vector3(40f, 0f, 0f), Vector3.forward, true, true);
            Assert.AreEqual(ThreatLevel.None, tooFar.Level);
        }
    }
}
