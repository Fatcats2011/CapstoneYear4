using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The Controls panel in a player's pause menu (ControlsPanel): a row per control of the set they're using and Reset
    /// last; A on a row waits for a press on their own device and binds it (a press that can't be bound keeps it
    /// waiting, Back cancels), A on Reset resets, Back closes and saves. Presses are faked: the panel's listener is
    /// swapped
    /// </summary>
    public class ControlsPanelTests
    {
        readonly TestObjects objects = new TestObjects();
        InputActionAsset asset;
        InputActionMap map;
        ControlsPanel panel;

        static Action<string> hear; // the press the panel waits for
        static int listens;
        static int stops;
        static int saves;
        static int frame;

        static void Listen(Func<InputDevice, bool> mine, Action<string> heard) { hear = heard; listens++; }
        static void Stop() { hear = null; stops++; }
        static void Save(int seat, InputActionMap map) { saves++; }
        static int Frame() { return frame; }
        static bool Mine(InputDevice device) { return true; }

        [SetUp]
        public void SetUp()
        {
            hear = null;
            listens = stops = saves = frame = 0;
            asset = UnityEngine.Object.Instantiate(Resources.Load<InputActionAsset>(KeyboardControls.ASSET));
            KeyboardControls.Install(asset);
            map = asset.FindActionMap("Player");

            RectTransform parent = objects.NewGameObject("Pause Menu").AddComponent<RectTransform>();
            panel = ControlsPanel.Build(parent, null);
            panel.Listen = Listen;
            panel.StopListening = Stop;
            panel.Saved = Save;
            panel.Frame = Frame;
        }

        [TearDown]
        public void TearDown()
        {
            objects.DestroyAll();
            UnityEngine.Object.DestroyImmediate(asset);
        }

        void NextFrame()
        {
            frame++;
            panel.Tick();
        }

        void Open(BindingSet set)
        {
            panel.Open(map, set, 1, Mine);
            NextFrame();
        }

        // Down to a row by its name
        void Select(string name)
        {
            for (int i = 0; i < 20 && panel.RowName(panel.Row) != name; i++)
                panel.Move(true);
            Assert.AreEqual(name, panel.RowName(panel.Row));
        }

        void Press(string path)
        {
            Assert.IsNotNull(hear, "listening");
            hear(path);
            NextFrame();
        }

        [Test]
        public void Open_ARowPerControl_AndResetLast()
        {
            Open(BindingSet.Controller);

            Assert.IsTrue(panel.IsOpen);
            Assert.AreEqual("CONTROLS — CONTROLLER", panel.Title);
            Assert.AreEqual(6, panel.RowCount);
            Assert.AreEqual("ACCELERATE", panel.RowName(0));
            Assert.AreEqual("RT", panel.RowValue(0));
            Assert.AreEqual(ControlsPanel.RESET, panel.RowName(5));
        }

        [Test]
        public void Open_TheKeyboardSet()
        {
            Open(BindingSet.Keyboard);

            Assert.AreEqual("CONTROLS — KEYBOARD", panel.Title);
            Assert.AreEqual(12, panel.RowCount);
            Assert.AreEqual("W", panel.RowValue(0));
        }

        [Test]
        public void A_WaitsForAPress_ThenBindsIt()
        {
            Open(BindingSet.Keyboard);
            Select("BOOST");

            panel.Confirm();
            Assert.IsTrue(panel.Waiting);
            Assert.AreEqual("PRESS A KEY...", panel.RowValue(panel.Row));
            Assert.AreEqual(0, listens, "listening starts next frame: the A that chose the row doesn't count");
            NextFrame();
            Press("<Keyboard>/k");

            Assert.IsFalse(panel.Waiting);
            Assert.AreEqual("K", panel.RowValue(panel.Row));
        }

        [Test]
        public void APressThatCantBeBound_KeepsItWaiting()
        {
            Open(BindingSet.Controller);
            panel.Confirm();
            NextFrame();

            Press("<Gamepad>/start");

            Assert.IsTrue(panel.Waiting, "Pause can't be bound");
            Assert.AreEqual(2, listens, "listening again");
            Press("<Gamepad>/buttonNorth");
            Assert.IsFalse(panel.Waiting);
            Assert.AreEqual("Y", panel.RowValue(0));
        }

        [Test]
        public void BackWhileWaiting_Cancels_TheControlUnchanged()
        {
            Open(BindingSet.Controller);
            panel.Confirm();
            NextFrame();

            Press("<Gamepad>/buttonEast");

            Assert.IsFalse(panel.Waiting);
            Assert.IsTrue(panel.IsOpen, "only the wait ends");
            Assert.AreEqual("RT", panel.RowValue(0));
        }

        [Test]
        public void Backspace_CancelsTheKeyboardsWait()
        {
            Open(BindingSet.Keyboard);
            panel.Confirm();
            NextFrame();

            Press("<Keyboard>/backspace");

            Assert.IsFalse(panel.Waiting);
            Assert.AreEqual("W", panel.RowValue(0));
        }

        [Test]
        public void WhileWaiting_TheMenuButtonsDoNothing()
        {
            Open(BindingSet.Keyboard);
            panel.Confirm();
            NextFrame();

            panel.Move(true);
            panel.Back();
            panel.Confirm();

            Assert.IsTrue(panel.Waiting);
            Assert.AreEqual(0, panel.Row);
            Assert.IsTrue(panel.IsOpen);
        }

        [Test]
        public void TheFrameOfABind_TheMenuButtonsDoNothing()
        {
            Open(BindingSet.Keyboard);
            panel.Confirm();
            NextFrame();

            hear("<Keyboard>/space"); // Space is also the menus' A: the same press mustn't choose the row again
            panel.Confirm();

            Assert.IsFalse(panel.Waiting);
        }

        [Test]
        public void AKeyInUse_Swaps_AndBothRowsShowIt()
        {
            Open(BindingSet.Controller);
            Select("BOOST");
            panel.Confirm();
            NextFrame();

            Press("<Gamepad>/buttonWest");

            Assert.AreEqual("X", panel.RowValue(2));
            Assert.AreEqual("DRIFT", panel.RowName(3));
            Assert.AreEqual("A", panel.RowValue(3));
        }

        [Test]
        public void Reset_BackToTheGamesControls()
        {
            Open(BindingSet.Keyboard);
            panel.Confirm();
            NextFrame();
            Press("<Keyboard>/k");

            Select(ControlsPanel.RESET);
            panel.Confirm();

            Assert.AreEqual("W", panel.RowValue(0));
            Assert.IsFalse(panel.Waiting);
        }

        [Test]
        public void Back_Closes_AndSaves()
        {
            Open(BindingSet.Keyboard);

            panel.Back();

            Assert.IsFalse(panel.IsOpen);
            Assert.AreEqual(1, saves);
        }

        [Test]
        public void Close_WhileWaiting_StopsListening_AndSaves()
        {
            Open(BindingSet.Keyboard);
            panel.Confirm();
            NextFrame();

            panel.Close();

            Assert.IsFalse(panel.IsOpen);
            Assert.IsFalse(panel.Waiting);
            Assert.GreaterOrEqual(stops, 1);
            Assert.AreEqual(1, saves);
        }

        [Test]
        public void Up_FromTheTop_GoesRoundToReset()
        {
            Open(BindingSet.Controller);

            panel.Move(false);

            Assert.AreEqual(ControlsPanel.RESET, panel.RowName(panel.Row));
        }

        [Test]
        public void Closed_ThePanelIgnoresEverything()
        {
            Open(BindingSet.Keyboard);
            panel.Close();

            panel.Confirm();
            NextFrame();

            Assert.AreEqual(0, listens);
            Assert.AreEqual(1, saves);
        }
    }
}
