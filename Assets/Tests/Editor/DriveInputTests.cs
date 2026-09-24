using System;
using Cinemachine;
using NUnit.Framework;
using UnityEngine;

namespace DoA.Tests
{
    /// <summary>
    /// A driver whose sticks and triggers a test sets by hand
    /// </summary>
    class FakeDriveInput : IDriveInput
    {
#pragma warning disable 0067 // tests count the subscribers, they never raise these
        public event Action<bool> DriftButton;
        public event Action<bool> BoostButton;
        public event Action<Vector2> EmotePad;
#pragma warning restore 0067

        public float Steer { get; set; }
        public float Accelerate { get; set; }
        public float Brake { get; set; }
        public float CameraX { get; set; }
        public float CameraY { get; set; }
        public bool LookBehind { get; set; }
    }

    public class DriveInputTests
    {
        /// <summary>
        /// Something that listens to a driver's buttons, so a test can count its subscriptions
        /// </summary>
        class Listener
        {
            public void Button(bool pressed) { }
            public void Pad(Vector2 direction) { }
        }

        readonly TestObjects objects = new TestObjects();

        [TearDown]
        public void TearDown()
        {
            Reflect.SetSingleton<PlayerInstantiate>(null);
            objects.DestroyAll();
        }

        [Test]
        public void InputManager_AsADriver_ReportsItsSticksAndTriggers()
        {
            InputManager controller = objects.Add<InputManager>();
            Reflect.SetField(controller, "leftStickValue", -0.5f);
            Reflect.SetField(controller, "rightTriggerValue", 0.75f);
            Reflect.SetField(controller, "leftTriggerValue", 0.25f);
            Reflect.SetField(controller, "rightStickXValue", 0.3f);
            Reflect.SetField(controller, "rightStickYValue", -0.4f);
            Reflect.SetField(controller, "rightStickValue", true);

            IDriveInput driver = controller;

            Assert.AreEqual(-0.5f, driver.Steer, "steer = left stick");
            Assert.AreEqual(0.75f, driver.Accelerate, "accelerate = right trigger");
            Assert.AreEqual(0.25f, driver.Brake, "brake = left trigger");
            Assert.AreEqual(0.3f, driver.CameraX, "camera X = right stick X");
            Assert.AreEqual(-0.4f, driver.CameraY, "camera Y = right stick Y");
            Assert.IsTrue(driver.LookBehind, "look behind = right stick press");
        }

        [Test]
        public void InputManager_AsADriver_PassesOnItsDriftBoostAndEmoteButtons()
        {
            InputManager controller = objects.Add<InputManager>();
            IDriveInput driver = controller;
            Listener listener = new Listener();

            driver.DriftButton += listener.Button;
            driver.BoostButton += listener.Button;
            driver.EmotePad += listener.Pad;
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "WestFaceEvent", listener), "drift = X");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "SouthFaceEvent", listener), "boost = A");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "DPadEvent", listener), "emotes = d-pad");

            driver.DriftButton -= listener.Button;
            driver.BoostButton -= listener.Button;
            driver.EmotePad -= listener.Pad;
            Assert.AreEqual(0, Reflect.HandlerCount(controller, "WestFaceEvent", listener)
                + Reflect.HandlerCount(controller, "SouthFaceEvent", listener)
                + Reflect.HandlerCount(controller, "DPadEvent", listener), "after unsubscribing");
        }

        // A controller whose scooter is gone, or whose scooter listens to another driver, has nobody on its buttons
        [Test]
        public void InputManager_ButtonPressesNobodyListensTo_AreIgnored()
        {
            InputManager controller = objects.Add<InputManager>();

            Assert.DoesNotThrow(() => controller.SouthFaceTrigger(default), "A");
            Assert.DoesNotThrow(() => controller.WestFaceTrigger(default), "X");
            Assert.DoesNotThrow(() => controller.NorthFaceTrigger(default), "Y");
        }

        /// <summary>
        /// A scooter with just enough around it to run Start
        /// </summary>
        BallDriving StartedScooter(InputManager controller, IDriveInput driverSetBeforeStart = null)
        {
            Reflect.SetSingleton(objects.Add<PlayerInstantiate>()); // Start looks up this player's gamepad for rumble
            BallDriving scooter = objects.Add<BallDriving>();
            scooter.playerIndex = 1;
            GameObject sphere = objects.NewGameObject("Ball Of Fun");
            sphere.AddComponent<Rigidbody>();
            sphere.AddComponent<SphereCollider>();
            GameObject sparks = objects.NewGameObject("Particle Basket");
            for (int i = 0; i < 6; i++)
                new GameObject("Spark " + i).transform.SetParent(sparks.transform);
            Reflect.SetField(scooter, "sphere", sphere);
            Reflect.SetField(scooter, "particleBasket", sparks.transform);
            Reflect.SetField(scooter, "orderHandler", objects.Add<OrderHandler>());
            Reflect.SetField(scooter, "inp", controller);
            if (driverSetBeforeStart != null)
                scooter.DriveInput = driverSetBeforeStart;

            Reflect.Invoke(scooter, "Start");
            return scooter;
        }

        [Test]
        public void BallDriving_Start_ListensToItsControllersDriftAndBoostButtons()
        {
            InputManager controller = objects.Add<InputManager>();

            BallDriving scooter = StartedScooter(controller);

            Assert.AreSame(controller, scooter.DriveInput);
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "WestFaceEvent", scooter), "drift (X)");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "SouthFaceEvent", scooter), "boost (A)");
        }

        [Test]
        public void BallDriving_DriverChangedWhileDriving_ListensOnlyToTheNewDriver()
        {
            InputManager controller = objects.Add<InputManager>();
            BallDriving scooter = StartedScooter(controller);
            FakeDriveInput online = new FakeDriveInput();

            scooter.DriveInput = online;

            Assert.AreEqual(0, Reflect.HandlerCount(controller, "WestFaceEvent", scooter)
                + Reflect.HandlerCount(controller, "SouthFaceEvent", scooter), "old driver");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "DriftButton", scooter), "new driver's drift");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "BoostButton", scooter), "new driver's boost");
        }

        [Test]
        public void BallDriving_DriverSetBeforeStart_IsTheOneItListensTo()
        {
            FakeDriveInput online = new FakeDriveInput();

            BallDriving scooter = StartedScooter(null, online); // a scooter with no controller of its own (after Task 2.2)

            Assert.AreEqual(1, Reflect.HandlerCount(online, "DriftButton", scooter), "drift");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "BoostButton", scooter), "boost");
        }

        [Test]
        public void BallDriving_Destroyed_StopsListeningToItsDriver()
        {
            FakeDriveInput online = new FakeDriveInput();
            BallDriving scooter = StartedScooter(null, online);

            Reflect.Invoke(scooter, "OnDestroy");

            Assert.AreEqual(0, Reflect.HandlerCount(online, "DriftButton", scooter)
                + Reflect.HandlerCount(online, "BoostButton", scooter));
        }

        [Test]
        public void EmoteHandler_DriverChangedWhileEnabled_ListensOnlyToTheNewDriver()
        {
            InputManager controller = objects.Add<InputManager>();
            EmoteHandler emotes = objects.Add<EmoteHandler>();
            Reflect.SetField(emotes, "input", controller);
            Reflect.Invoke(emotes, "OnEnable");
            Assert.AreEqual(1, Reflect.HandlerCount(controller, "DPadEvent", emotes), "its controller at first");

            FakeDriveInput online = new FakeDriveInput();
            emotes.DriveInput = online;
            Assert.AreEqual(0, Reflect.HandlerCount(controller, "DPadEvent", emotes), "old driver");
            Assert.AreEqual(1, Reflect.HandlerCount(online, "EmotePad", emotes), "new driver");

            Reflect.Invoke(emotes, "OnDisable");
            Assert.AreEqual(0, Reflect.HandlerCount(online, "EmotePad", emotes), "after disabling");
        }

        [Test]
        public void EmoteHandler_DriverSetBeforeEnable_IsTheOneItListensTo()
        {
            EmoteHandler emotes = objects.Add<EmoteHandler>();
            FakeDriveInput online = new FakeDriveInput();

            emotes.DriveInput = online;
            Reflect.Invoke(emotes, "OnEnable");

            Assert.AreEqual(1, Reflect.HandlerCount(online, "EmotePad", emotes));
        }

        /// <summary>
        /// An orbital camera with its main and icon virtual cameras, driven by a test driver
        /// </summary>
        OrbitalCamera CameraRig(FakeDriveInput driver, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit)
        {
            OrbitalCamera rig = objects.Add<OrbitalCamera>();
            CinemachineVirtualCamera main = objects.Add<CinemachineVirtualCamera>();
            CinemachineVirtualCamera icon = objects.Add<CinemachineVirtualCamera>();
            mainOrbit = main.AddCinemachineComponent<CinemachineOrbitalTransposer>();
            iconOrbit = icon.AddCinemachineComponent<CinemachineOrbitalTransposer>();
            Reflect.SetField(rig, "virtualCameraMain", main);
            Reflect.SetField(rig, "virtualCameraIcon", icon);
            Reflect.SetField(rig, "mainOrb", mainOrbit);
            Reflect.SetField(rig, "iconOrb", iconOrbit);
            Reflect.SetField(rig, "CameraFocus", objects.NewGameObject("Camera Focus"));
            rig.DriveInput = driver;
            return rig;
        }

        [Test]
        public void OrbitalCamera_DriverPushesTheRightStickRight_TurnsBothCameras()
        {
            OrbitalCamera rig = CameraRig(new FakeDriveInput { CameraX = 1f }, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit);

            Reflect.Invoke(rig, "Update");

            Assert.Greater(mainOrbit.m_XAxis.Value, 0f, "main camera");
            Assert.AreEqual(mainOrbit.m_XAxis.Value, iconOrbit.m_XAxis.Value, "icon camera follows");
        }

        [Test]
        public void OrbitalCamera_DriverPressesTheRightStick_LooksBehind()
        {
            OrbitalCamera rig = CameraRig(new FakeDriveInput { LookBehind = true }, out CinemachineOrbitalTransposer mainOrbit, out CinemachineOrbitalTransposer iconOrbit);

            Reflect.Invoke(rig, "Update");

            Assert.AreEqual(-180f, mainOrbit.m_XAxis.Value, "main camera");
            Assert.AreEqual(-180f, iconOrbit.m_XAxis.Value, "icon camera");
        }
    }
}
