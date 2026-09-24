using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    public class ResultsMenuTests
    {
        const string END_STATUS = "End Status"; // the results screen's animator parameter: 0 = racing, 1-4 = finishing place

        readonly TestObjects objects = new TestObjects();
        AnimatorController controller;

        [TearDown]
        public void TearDown()
        {
            SceneFlow.Current = null;
            Reflect.SetSingleton<ScoreManager>(null);
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
            if (controller != null)
                Object.DestroyImmediate(controller);
        }

        Animator ScooterAnimator()
        {
            controller = new AnimatorController();
            controller.AddLayer("Base Layer");
            controller.layers[0].stateMachine.AddState("Racing");
            controller.AddParameter(END_STATUS, AnimatorControllerParameterType.Int);
            Animator animator = objects.Add<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            return animator;
        }

        /// <summary>
        /// A local player shaped like the split prefab: view root (controller, cameras) → PlayerAvatar → Control (the scooter's scripts)
        /// </summary>
        OrderHandler NestedScooter(Animator animator)
        {
            GameObject view = objects.NewGameObject("P1");
            view.AddComponent<PlayerCameraResizer>().playerAnimator = animator;
            GameObject avatar = new GameObject("P1");
            avatar.transform.SetParent(view.transform);
            GameObject control = new GameObject("Control");
            control.transform.SetParent(avatar.transform);
            OrderHandler orders = control.AddComponent<OrderHandler>();
            Reflect.SetField(orders, "playerAnimator", animator);
            return orders;
        }

        [Test]
        public void Return_WithScootersNestedInTheirViews_ResetsTheirResultsAnimation()
        {
            FakeSceneFlow flow = new FakeSceneFlow();
            SceneFlow.Current = flow;
            Animator animator = ScooterAnimator();
            animator.SetInteger(END_STATUS, 2); // finished second
            Assert.AreEqual(2, animator.GetInteger(END_STATUS), "the test animator holds its parameter in Edit Mode");
            OrderHandler orders = NestedScooter(animator);
            ScoreManager scores = objects.Add<ScoreManager>();
            Reflect.SetSingleton(scores);
            Reflect.SetField(scores, "orderHandlers", new List<OrderHandler> { orders });
            PlayerInstantiate players = objects.Add<PlayerInstantiate>();
            Reflect.SetSingleton(players);
            players.Roster.JoinLocal(objects.Add<PlayerInput>());
            ResultsMenu results = objects.Add<ResultsMenu>();
            Reflect.SetField(results, "displayText", new TMP_Text[0]);
            Reflect.SetField(results, "canQuit", true);

            results.ConfirmMenu();

            Assert.AreEqual(0, animator.GetInteger(END_STATUS), "the scooter's results animation is back to racing");
            Assert.AreEqual(1, flow.MenuReturns, "back to the menu");
        }
    }
}
