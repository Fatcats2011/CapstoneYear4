using System.Linq;
using DG.Tweening;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// Props in a ghost's hands (GhostProps): the carried order's package goes to the hands during a parcel move and back
    /// exactly as it was (position, turn, size, parent, its tweens), smaller in the hands (PARCEL_HELD); nothing when nothing is carried; an order that leaves
    /// mid-move is let go, not put back on this scooter; the crown shows during the crown moves and hides on Stop and
    /// OnDisable. Also which finishing places play the last place's emotes. Edit Mode: steps are driven by hand
    /// </summary>
    public class GhostPropsTests
    {
        const float DT = 0.05f;

        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            foreach (Order order in Object.FindObjectsOfType<Order>(true))
                if (order.orderMeshObject != null)
                    DOTween.Kill(order.orderMeshObject.transform);
            objects.DestroyAll();
        }

        sealed class Player
        {
            public OrderHandler handler;
            public PlayerEmotes player;
            public GhostEmotes emotes;
            public Transform rack;
        }

        // A player as PlayerEmotes finds one: an OrderHandler with a ball, and the ghost's Animator over the bones the
        // rig looks for, the hands out in front of the body
        Player NewPlayer()
        {
            GameObject top = objects.NewGameObject("Player");
            GameObject control = new GameObject("Control");
            control.transform.SetParent(top.transform);
            OrderHandler handler = control.AddComponent<OrderHandler>();
            handler.ball = control.AddComponent<BallDriving>();

            GameObject model = new GameObject("Player Model");
            model.transform.SetParent(control.transform, false);
            Animator animator = model.AddComponent<Animator>();
            Transform armature = new GameObject("Armature.001").transform;
            armature.SetParent(model.transform, false);
            Transform torso = Bone("Torso", armature, new Vector3(0f, 1f, 0f));
            Bone("Head", torso, new Vector3(0f, 1f, 0f));
            Bone("Hand.Left", torso, new Vector3(-0.6f, 0.2f, 0.5f));
            Bone("Hand.Right", torso, new Vector3(0.6f, 0.2f, 0.5f));
            handler.ball.playerAnimator = animator;

            Transform rack = new GameObject("Order 1 Position").transform;
            rack.SetParent(control.transform, false);
            rack.localPosition = new Vector3(0f, 1.5f, -1.5f);
            rack.localScale = Vector3.one * 0.8f;

            PlayerEmotes player = PlayerEmotes.Attach(top);
            Assert.IsNotNull(player.Emotes, "the ghost has emotes");
            Assert.IsNotNull(player.Emotes.Rig.HandLeft, "the rig found the hands");
            return new Player { handler = handler, player = player, emotes = player.Emotes, rack = rack };
        }

        static Transform Bone(string name, Transform parent, Vector3 at)
        {
            Transform bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = at;
            return bone;
        }

        // An order the player picked up, as OrderHandler holds one: on the rack, its package floating and bobbing
        // (the tweens Order.Pickup starts)
        Order Give(Player p)
        {
            Order order = objects.Add<Order>();
            GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mesh.name = "Order Mesh";
            mesh.transform.localScale = Vector3.one * 0.7f;
            mesh.transform.localRotation = Quaternion.Euler(-90f, 0f, 180f);
            order.orderMeshObject = mesh;
            order.IsActive = true;
            order.playerHolding = p.handler;
            p.handler.order1 = order;
            order.transform.parent = p.rack;
            order.SetMeshPosition(p.rack.position);
            mesh.transform.DOLocalRotate(new Vector3(0f, 90f, 0f), 2f, RotateMode.FastBeyond360)
                .SetLoops(-1, LoopType.Incremental).SetRelative().SetEase(Ease.Linear);
            mesh.transform.DOLocalMove(new Vector3(0f, 0.2f, 0f), 1f).SetLoops(-1, LoopType.Yoyo).SetRelative().SetEase(Ease.Linear);
            return order;
        }

        static GhostMove Move(string name)
        {
            return GhostMoves.All.First(m => m.Name == name);
        }

        static void Run(GhostEmotes emotes, float seconds)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += DT)
                emotes.Step(DT);
        }

        static bool Playing(Transform mesh)
        {
            var tweens = DOTween.TweensByTarget(mesh);
            return tweens != null && tweens.Count == 2 && tweens.All(t => t.IsActive() && t.IsPlaying());
        }

        static bool Paused(Transform mesh)
        {
            var tweens = DOTween.TweensByTarget(mesh);
            return tweens != null && tweens.Count == 2 && tweens.All(t => t.IsActive() && !t.IsPlaying());
        }

        static Vector3 Hands(GhostEmotes emotes)
        {
            return (emotes.Rig.HandLeft.position + emotes.Rig.HandRight.position) * 0.5f;
        }

        [Test]
        public void ParcelMoves_AreMarked()
        {
            string[] parcels = { "Parcel Shake", "Parcel Spin", "Parcel Overhead", "Parcel Hug", "Presenting", "Parcel Clutch" };
            foreach (GhostMove move in GhostMoves.All)
            {
                bool parcel = parcels.Contains(move.Name);
                Assert.AreEqual(parcel, move.HoldsParcel, move.Name);
                bool crown = move.Name == "Crown Overhead" || move.Name == "Polish";
                Assert.AreEqual(crown, move.HoldsCrown, move.Name);
            }
            Assert.AreEqual(GhostProp.ParcelSpin, Move("Parcel Spin").Prop);
            Assert.AreEqual(GhostProp.CrownLeft, Move("Polish").Prop);
            Assert.AreEqual(.15f, Move("Crown Overhead").PropIn, 1e-6f);
            Assert.AreEqual(.85f, Move("Crown Overhead").PropOut, 1e-6f);
            Assert.AreEqual(.78f, Move("Polish").PropOut, 1e-6f);
        }

        [Test]
        public void CarriedOrder_FollowsTheHands_ThenGoesBackExactly()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            Transform parent = mesh.parent;
            Vector3 position = mesh.localPosition, scale = mesh.localScale, size = mesh.lossyScale;
            Quaternion rotation = mesh.localRotation;
            Assert.IsTrue(Playing(mesh), "its tweens run before");

            GhostMove move = Move("Parcel Overhead");
            p.emotes.Play(move, 1f);
            Run(p.emotes, DT);
            float early = mesh.lossyScale.x / size.x;
            Assert.Less(early, 1f, "shrinking on the way");
            Assert.Greater(early, GhostProps.PARCEL_HELD, "not all at once");
            Run(p.emotes, move.Length * 0.5f - DT);

            Assert.AreSame(mesh, p.emotes.Props.Parcel, "it holds the carried order's package");
            Assert.AreEqual(1f, p.emotes.Props.ParcelWeight, 1e-4f, "in the hands mid-move");
            Assert.Less(Vector3.Distance(Hands(p.emotes), mesh.position), 1e-3f, "between the hands");
            Assert.Less((mesh.lossyScale - size * GhostProps.PARCEL_HELD).sqrMagnitude, 1e-8f, "smaller in the hands");
            Assert.AreSame(parent, mesh.parent, "its parent kept");
            Assert.IsTrue(Paused(mesh), "its tweens wait");

            Run(p.emotes, move.Length * 0.5f + 0.3f);

            Assert.IsNull(p.emotes.Playing);
            Assert.IsNull(p.emotes.Props.Parcel, "let go");
            Assert.AreSame(parent, mesh.parent);
            Assert.AreEqual(position, mesh.localPosition, "local position exactly");
            Assert.AreEqual(rotation, mesh.localRotation, "local turn exactly");
            Assert.AreEqual(scale, mesh.localScale, "local scale exactly");
            Assert.IsTrue(Playing(mesh), "its tweens go on");
        }

        /// <summary>On its way from the rack to the hands it arcs up over the head, not through the body</summary>
        [Test]
        public void CarriedOrder_ArcsOverTheHeadOnTheWay()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            GhostRig rig = p.emotes.Rig;
            p.emotes.Play(Move("Parcel Overhead"), 1f);
            float highest = float.MinValue;
            for (float t = 0f; t < GhostProps.PARCEL_IN; t += DT)
            {
                Run(p.emotes, DT);
                highest = Mathf.Max(highest, Vector3.Dot(mesh.position - rig.Head.position, rig.Up) / rig.U);
            }
            Assert.Greater(highest, GhostProps.PARCEL_OVER - 0.3f, "over the hat part way");
        }

        [Test]
        public void ParcelSpin_SitsOverTheRightHand()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            GhostMove move = Move("Parcel Spin");
            p.emotes.Play(move, 1f);
            Run(p.emotes, 1f);

            Vector3 hand = p.emotes.Rig.HandRight.position;
            Vector3 up = p.emotes.Rig.Up;
            Vector3 off = mesh.position - hand;
            Assert.Greater(Vector3.Dot(off, up), 0.2f, "above the hand");
            Assert.Less((off - up * Vector3.Dot(off, up)).magnitude, 1e-3f, "right over it");
            Quaternion a = mesh.rotation;
            Run(p.emotes, DT);
            Assert.Greater(Quaternion.Angle(a, mesh.rotation), 1f, "it spins");
        }

        [Test]
        public void InterruptedByAnotherParcelMove_KeepsHoldingIt_StopPutsItBack()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            Vector3 position = mesh.localPosition;
            Vector3 scale = mesh.localScale;
            Quaternion rotation = mesh.localRotation;

            p.emotes.Play(Move("Parcel Hug"), 1f);
            Run(p.emotes, 0.5f);
            p.emotes.Play(Move("Parcel Shake"), 1f);
            Run(p.emotes, DT);
            Assert.AreSame(mesh, p.emotes.Props.Parcel);
            Assert.Greater(p.emotes.Props.ParcelWeight, 0.9f, "no trip back to the rack between them");

            p.emotes.Stop();
            Assert.IsNull(p.emotes.Props.Parcel);
            Assert.AreEqual(position, mesh.localPosition);
            Assert.AreEqual(rotation, mesh.localRotation);
            Assert.AreEqual(scale, mesh.localScale, "its size back");
            Assert.IsTrue(Playing(mesh));
        }

        [Test]
        public void InterruptedByAMoveWithoutIt_GoesBackSoon()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            Vector3 position = mesh.localPosition;
            Vector3 scale = mesh.localScale;

            p.emotes.Play(Move("Parcel Clutch"), 1f);
            Run(p.emotes, 1f);
            p.emotes.Play(Move("Fist Pump"), 1f);
            Run(p.emotes, GhostProps.PARCEL_CUT + 2 * DT);
            Assert.IsNull(p.emotes.Props.Parcel);
            Assert.AreEqual(position, mesh.localPosition);
            Assert.AreEqual(scale, mesh.localScale, "its size back");
            Assert.IsTrue(Playing(mesh));
        }

        [Test]
        public void OnDisable_PutsItBack()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            Vector3 position = mesh.localPosition;
            Vector3 scale = mesh.localScale;
            Quaternion rotation = mesh.localRotation;

            p.emotes.Play(Move("Presenting"), 1f);
            Run(p.emotes, 1f);
            Reflect.Invoke(p.emotes, "OnDisable");
            Assert.AreEqual(position, mesh.localPosition);
            Assert.AreEqual(rotation, mesh.localRotation);
            Assert.AreEqual(scale, mesh.localScale, "its size back");
            Assert.IsTrue(Playing(mesh));
        }

        [Test]
        public void NothingCarried_PlaysWithoutAProp()
        {
            Player p = NewPlayer();
            GhostMove move = Move("Parcel Overhead");
            p.emotes.Play(move, 1f);
            Assert.DoesNotThrow(() => Run(p.emotes, move.Length + 0.3f));
            Assert.IsNull(p.emotes.Props.Parcel);

            // An order it doesn't hold any more (another's now) isn't taken either
            Order order = Give(p);
            order.playerHolding = null;
            p.emotes.Play(move, 1f);
            Run(p.emotes, 0.5f);
            Assert.IsNull(p.emotes.Props.Parcel);
        }

        [Test]
        public void OrderLeavesMidMove_LetGo_NotPutBackOnThisScooter()
        {
            Player thief = NewPlayer();
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            Vector3 position = mesh.localPosition;
            Vector3 scale = mesh.localScale;

            GhostMove move = Move("Parcel Overhead");
            p.emotes.Play(move, 1f);
            Run(p.emotes, 1f);

            // Stolen: off this scooter and onto the thief's (as the steal and the pickup do it)
            p.handler.order1 = null;
            order.playerHolding = thief.handler;
            thief.handler.order1 = order;
            thief.handler.transform.position = new Vector3(40f, 0f, 0f);
            order.transform.parent = thief.rack;
            order.SetMeshPosition(thief.rack.position);

            Assert.DoesNotThrow(() => Run(p.emotes, move.Length));
            Assert.IsNull(p.emotes.Props.Parcel, "let go at once");
            Assert.AreEqual(position, mesh.localPosition, "where the thief's rack put it");
            Assert.AreEqual(scale, mesh.localScale, "its size back");
            Assert.Greater(Vector3.Distance(mesh.position, p.handler.transform.position), 30f, "not back on this scooter");
            Assert.IsTrue(Playing(mesh), "the tweens it paused go on");
        }

        [Test]
        public void OrderDroppedOrDestroyedMidMove_DoesNotThrow()
        {
            Player p = NewPlayer();
            Order order = Give(p);
            Transform mesh = order.orderMeshObject.transform;
            GhostMove move = Move("Parcel Shake");
            p.emotes.Play(move, 1f);
            Run(p.emotes, 0.6f);

            // Dropped: off the scooter, flying away on its own
            p.handler.order1 = null;
            order.playerHolding = null;
            order.transform.SetParent(null, true);
            order.transform.position = new Vector3(0f, 0f, 60f);
            Run(p.emotes, DT);
            Assert.IsNull(p.emotes.Props.Parcel);
            Assert.Greater(Vector3.Distance(mesh.position, p.handler.transform.position), 50f, "it went with its order");

            // And one destroyed while held
            Order second = Give(p);
            p.emotes.Play(move, 1f);
            Run(p.emotes, 0.6f);
            DOTween.Kill(second.orderMeshObject.transform);
            Object.DestroyImmediate(second.orderMeshObject);
            Assert.DoesNotThrow(() => Run(p.emotes, DT));
            Assert.IsNull(p.emotes.Props.Parcel);
            Assert.DoesNotThrow(() => p.emotes.Stop());
        }

        [TestCase("Crown Overhead")]
        [TestCase("Polish")]
        public void Crown_ShowsDuringTheMove_HiddenAfter(string name)
        {
            Player p = NewPlayer();
            GhostMove move = Move(name);
            GhostProps props = p.emotes.Props;
            Assert.IsNull(props.Crown, "made only when needed");

            p.emotes.Play(move, 1f);
            Run(p.emotes, move.Length * 0.5f);
            Assert.IsTrue(props.CrownShown, "in hand mid-move");
            Assert.AreSame(p.emotes.transform, props.Crown.transform.parent, "under the ghost");
            Vector3 hold = move.Prop == GhostProp.CrownLeft ? p.emotes.Rig.HandLeft.position : Hands(p.emotes);
            Assert.Less(Vector3.Distance(props.Crown.transform.position, hold), p.emotes.Rig.U, "at the hands");
            Assert.AreEqual(GhostProps.CROWN_WIDE * GhostProps.CROWN_HELD * p.emotes.Rig.U, props.Crown.bounds.size.x, 0.05f,
                "its width: bigger in the hands");

            Run(p.emotes, move.Length * 0.5f + 0.2f);
            Assert.IsFalse(props.CrownShown, "hidden at the end");
            SpriteRenderer pooled = props.Crown;

            // Again: the same crown; Stop hides it
            p.emotes.Play(move, 1f);
            Run(p.emotes, move.Length * 0.5f);
            Assert.AreSame(pooled, props.Crown, "kept for next time");
            Assert.IsTrue(props.CrownShown);
            p.emotes.Stop();
            Assert.IsFalse(props.CrownShown, "Stop hides it");

            // And OnDisable
            p.emotes.Play(move, 1f);
            Run(p.emotes, move.Length * 0.5f);
            Assert.IsTrue(props.CrownShown);
            Reflect.Invoke(p.emotes, "OnDisable");
            Assert.IsFalse(props.CrownShown, "OnDisable hides it");

            // An interrupt by another move hides it too
            p.emotes.Play(move, 1f);
            Run(p.emotes, move.Length * 0.5f);
            p.emotes.Play(Move("Fist Pump"), 1f);
            Assert.IsFalse(props.CrownShown, "interrupted");

            // It goes with the ghost
            props.Destroy();
            Assert.IsTrue(pooled == null, "destroyed with the ghost");
        }

        [Test]
        public void NotACrownMove_NoCrownMade()
        {
            Player p = NewPlayer();
            p.emotes.Play(Move("Parcel Overhead"), 1f);
            Run(p.emotes, 1f);
            Assert.IsNull(p.emotes.Props.Crown);
        }

        [Test]
        public void Began_FiresForEachMoveThatStarts_NotForARefusedOne()
        {
            Player p = NewPlayer();
            var began = new System.Collections.Generic.List<GhostMove>();
            System.Action<GhostEmotes, GhostMove> hear = (e, m) => { Assert.AreSame(p.emotes, e); began.Add(m); };
            GhostEmotes.Began += hear;
            try
            {
                Assert.IsTrue(p.emotes.Play(GhostAction.Bored));
                Assert.IsFalse(p.emotes.Play(GhostAction.Bored), "cooling down");
                Assert.AreEqual(1, began.Count);
                Assert.AreEqual(GhostAction.Bored, began[0].Action);
                Assert.AreSame(p.emotes.Playing, began[0], "the version that plays");
            }
            finally
            {
                GhostEmotes.Began -= hear;
            }
        }

        [Test]
        public void GoldenPickup_TurnedAway_TreasureHoldPlaysOnceItCan()
        {
            Player p = NewPlayer();
            PlayerEmotes.MatchOn = () => true;
            try
            {
                // The thief's Cackle plays as the golden order comes over
                Assert.IsTrue(p.emotes.Play(GhostAction.Cackle));
                Order order = Give(p);
                order.value = Constants.OrderValue.Golden;
                p.player.Tick(DT);
                Assert.AreEqual(GhostAction.TreasureHold, p.player.Asked, "asked for as it's picked up");
                Assert.AreEqual(GhostAction.Cackle, p.emotes.Playing.Action, "turned away meanwhile");

                // Once the Cackle is over, it plays (not GOLDEN_EVERY later)
                for (int i = 0; i < 80 && p.emotes.Playing != null && p.emotes.Playing.Action == GhostAction.Cackle; i++)
                {
                    p.emotes.Step(DT);
                    p.player.Tick(DT);
                }
                Assert.IsNotNull(p.emotes.Playing);
                Assert.AreEqual(GhostAction.TreasureHold, p.emotes.Playing.Action);
                int asked = p.player.AskedFor(GhostAction.TreasureHold);
                p.emotes.Step(DT);
                p.player.Tick(DT);
                Assert.AreEqual(asked, p.player.AskedFor(GhostAction.TreasureHold), "no more asks once it played");
            }
            finally
            {
                PlayerEmotes.MatchOn = null;
            }
        }

        // ---- The results: which places play the last place's emotes

        [TestCase(2, new[] { PodiumMood.Winner, PodiumMood.Last })]
        [TestCase(3, new[] { PodiumMood.Winner, PodiumMood.Runner, PodiumMood.Last })]
        [TestCase(4, new[] { PodiumMood.Winner, PodiumMood.Runner, PodiumMood.Runner, PodiumMood.Last })]
        public void PodiumEmoteMood_LastFinishingPlaceIsLast(int players, PodiumMood[] moods)
        {
            for (int place = 0; place < players; place++)
                Assert.AreEqual(moods[place], PlayerEmoteRules.PodiumEmoteMood(place, players), "place " + place + " of " + players);
            Assert.AreEqual(PodiumMood.Winner, PlayerEmoteRules.PodiumEmoteMood(0, 1), "alone: the winner");
        }

        [Test]
        public void PodiumEmotes_LastOfTwoAndThree_Emote_ActsUnchanged()
        {
            Player p = NewPlayer();
            Assert.IsNotNull(PodiumEmotes.For(1, 2, p.emotes), "2nd of 2");
            Assert.IsNotNull(PodiumEmotes.For(2, 3, p.emotes), "3rd of 3");
            Assert.IsNull(PodiumEmotes.For(1, 3, p.emotes), "2nd of 3 is a runner");
            Assert.AreEqual(PodiumMood.Runner, PodiumActs.MoodOf(1, 2), "the acts keep their moods");

            Transform model = objects.NewGameObject("Podium Model").transform;
            PodiumPerformer performer = PodiumPerformer.On(model, p.emotes.GetComponent<Animator>(), 1, 2, null, new System.Random(7));
            Assert.IsNotNull(performer.Emoting, "the last of two emotes on the podium");
            performer.Stop();
        }
    }
}
