using System;
using System.Linq;
using Cinemachine;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The player prefab is split: PlayerAvatar.prefab is the scooter every machine sees, and the local player's prefab
    /// (Player - Cinemachine: controller, cameras, menus) nests one avatar and adds its view parts to the avatar's Control
    /// as prefab overrides. See docs/player-prefab.md.
    /// </summary>
    public class PlayerPrefabTests
    {
        const string VIEW = "Assets/Prefabs/Player Prefabs/Player - Cinemachine.prefab";
        const string AVATAR = "Assets/Prefabs/Player Prefabs/PlayerAvatar.prefab";

        static readonly Type[] ViewComponentsOnControl = { typeof(OrbitalCamera), typeof(Compass), typeof(DynamicNumberUI), typeof(TutorialHandler), typeof(Rumbler) };

        static GameObject Load(string path)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.IsNotNull(prefab, path + " is missing");
            return prefab;
        }

        static UnityEngine.Object Link(Component owner, string field)
        {
            SerializedProperty property = new SerializedObject(owner).FindProperty(field);
            Assert.IsNotNull(property, owner.GetType().Name + " has no serialized field " + field);
            return property.objectReferenceValue;
        }

        [Test]
        public void Avatar_HoldsTheWholeScooter()
        {
            GameObject avatar = Load(AVATAR);

            // Sphere triggers climb to this root and search down; players' GetComponentInChildren<Rigidbody>() must reach the sphere first
            Assert.AreEqual("Ball Of Fun", avatar.transform.GetChild(0).name, "first child");
            Assert.AreEqual("Control", avatar.transform.GetChild(1).name, "second child");
            string[] missing = new[] { typeof(BallDriving), typeof(OrderHandler), typeof(Respawn), typeof(CanKicker), typeof(SoundPool), typeof(CompassMarker),
                    typeof(PhaseIndicator), typeof(DrivingIndicators), typeof(TrailHandler), typeof(SkideeSkidoo), typeof(HeadlightController) }
                .Where(part => avatar.GetComponentInChildren(part, true) == null).Select(part => part.Name).ToArray();
            CollectionAssert.IsEmpty(missing, "scooter parts missing from the avatar");
        }

        [Test]
        public void Avatar_HoldsNothingOfTheLocalView()
        {
            GameObject avatar = Load(AVATAR);

            string[] viewParts = new[] { typeof(PlayerInput), typeof(InputManager), typeof(PlayerUIHandler), typeof(PlayerCameraResizer), typeof(MenuInteractions),
                    typeof(Camera), typeof(Canvas), typeof(AudioListener), typeof(CinemachineBrain), typeof(CinemachineVirtualCamera), typeof(PhaseChecker) }
                .Concat(ViewComponentsOnControl)
                .Where(part => avatar.GetComponentInChildren(part, true) != null).Select(part => part.Name).ToArray();
            CollectionAssert.IsEmpty(viewParts, "the local view's parts don't belong in the scooter every machine sees");
        }

        [Test]
        public void LocalPlayer_IsTheViewAroundOneAvatar()
        {
            GameObject view = Load(VIEW);
            GameObject avatar = view.transform.GetChild(0).gameObject;

            Assert.AreSame(Load(AVATAR), PrefabUtility.GetCorrespondingObjectFromSource(avatar), "the view's first child is a PlayerAvatar");
            Assert.AreEqual(1, view.GetComponentsInChildren<BallDriving>(true).Length, "scooters in the local player");
            foreach (Type controls in new[] { typeof(PlayerInput), typeof(InputManager), typeof(PlayerUIHandler), typeof(PlayerCameraResizer) })
                Assert.IsNotNull(view.GetComponent(controls), controls.Name + " on the view's root");
            Transform control = avatar.transform.Find("Control");
            foreach (string viewChild in new[] { "Camera Holder", "Driving Canvas", "Menu Canvas", "Compass Calc" })
                Assert.IsNotNull(control.Find(viewChild), viewChild + " rides on the scooter's Control");
            foreach (Type viewPart in ViewComponentsOnControl)
                Assert.IsNotNull(control.GetComponent(viewPart), viewPart.Name + " sits on the scooter's Control, where the scooter's scripts find it");
        }

        // The fields the other half fills: prefab overrides on the nested avatar, or view fields pointing into it
        [TestCase(typeof(BallDriving), new[] { "inp", "cameraResizer", "orbitalCamera" })]
        [TestCase(typeof(OrderHandler), new[] { "numberHandler", "playerAnimator" })]
        [TestCase(typeof(PhaseIndicator), new[] { "hornSliderLeft", "hornSliderRight" })]
        [TestCase(typeof(DrivingIndicators), new[] { "iconCamera", "thisPlayer" })]
        [TestCase(typeof(OrbitalCamera), new[] { "inputManager", "virtualCameraMain", "virtualCameraIcon", "CameraFocus" })]
        [TestCase(typeof(Compass), new[] { "compassImage", "compassCalc", "player", "orbitalCamera", "playerCam" })]
        [TestCase(typeof(DynamicNumberUI), new[] { "numHandler", "centerText" })]
        [TestCase(typeof(TutorialHandler), new[] { "tutorialText", "tutorialImage" })]
        [TestCase(typeof(PlayerCameraResizer), new[] { "ballDriving", "drivingIndicators", "scooterModel", "playerAnimator" })]
        [TestCase(typeof(PhaseChecker), new[] { "target" })]
        public void LocalPlayer_LinksBetweenViewAndScooter_AreSet(Type component, string[] fields)
        {
            Component owner = Load(VIEW).GetComponentInChildren(component, true);
            Assert.IsNotNull(owner, component.Name + " in the local player");

            string[] empty = fields.Where(field => Link(owner, field) == null).ToArray();
            CollectionAssert.IsEmpty(empty, component.Name + " fields left empty");
        }

        [Test]
        public void LocalPlayer_ScooterUsesTheViewsControllerAndAnimator()
        {
            GameObject view = Load(VIEW);
            InputManager controller = view.GetComponent<InputManager>();
            BallDriving scooter = view.GetComponentInChildren<BallDriving>(true);

            Assert.AreSame(controller, Link(scooter, "inp"), "the scooter drives with the view's controller");
            Assert.AreSame(controller, Link(scooter.GetComponent<OrbitalCamera>(), "inputManager"), "the camera turns with it");
            Assert.AreSame(view.GetComponent<PlayerInput>(), Link(scooter.GetComponent<DrivingIndicators>(), "thisPlayer"), "the indicator skips its own view");
            Assert.AreSame(view.GetComponent<PlayerCameraResizer>().playerAnimator, scooter.GetComponent<OrderHandler>().PlayerAnimator,
                "the results screen animates the scooter through its order handler");
        }
    }
}
