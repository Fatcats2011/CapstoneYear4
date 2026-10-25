using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// Rebinding the driving controls (RebindRules, BindingStore): which bindings each set covers, the presses that can
    /// and can't be bound, a key or button already in use swapping, reset, the names shown, and bindings.cfg's lines
    /// (a damaged line loses only that set). On a copy of the game's own input actions. See
    /// docs/superpowers/specs/2026-10-23-control-rebinding-design.md
    /// </summary>
    public class RebindRulesTests
    {
        InputActionAsset asset;
        InputActionMap map;

        [SetUp]
        public void SetUp()
        {
            asset = Object.Instantiate(Resources.Load<InputActionAsset>(KeyboardControls.ASSET));
            KeyboardControls.Install(asset); // as the game: the keyboard keys (already in the asset on disk: no change)
            map = asset.FindActionMap("Player");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(asset);
        }

        static string[] Names(List<RebindSlot> slots)
        {
            string[] names = new string[slots.Count];
            for (int i = 0; i < names.Length; i++)
                names[i] = slots[i].Name;
            return names;
        }

        string PathOf(BindingSet set, string name)
        {
            foreach (RebindSlot slot in RebindRules.SlotsFor(map, set))
            {
                if (slot.Name == name)
                    return RebindRules.PathOf(map, slot);
            }
            Assert.Fail("no " + name);
            return null;
        }

        [Test]
        public void Keyboard_EveryDrivingKey_TheFirstOfEach()
        {
            CollectionAssert.AreEqual(new[]
            {
                "ACCELERATE", "BRAKE", "STEER LEFT", "STEER RIGHT", "BOOST", "DRIFT", "SWAP CAMERA",
                "CAMERA LEFT", "CAMERA RIGHT", "CAMERA UP", "CAMERA DOWN",
            }, Names(RebindRules.SlotsFor(map, BindingSet.Keyboard)));
            Assert.AreEqual("<Keyboard>/w", PathOf(BindingSet.Keyboard, "ACCELERATE"));
            Assert.AreEqual("<Keyboard>/a", PathOf(BindingSet.Keyboard, "STEER LEFT"));
            Assert.AreEqual("<Keyboard>/c", PathOf(BindingSet.Keyboard, "CAMERA DOWN"));
        }

        [Test]
        public void Controller_TheButtons_NotTheSticks()
        {
            CollectionAssert.AreEqual(new[] { "ACCELERATE", "BRAKE", "BOOST", "DRIFT", "SWAP CAMERA" },
                Names(RebindRules.SlotsFor(map, BindingSet.Controller)));
            Assert.AreEqual("<Gamepad>/rightTrigger", PathOf(BindingSet.Controller, "ACCELERATE"));
        }

        [TestCase("<Keyboard>/k", true)]
        [TestCase("<Keyboard>/space", true)]
        [TestCase("<Keyboard>/enter", true)]
        [TestCase("<Keyboard>/escape", false)]
        [TestCase("<Keyboard>/backspace", false)]
        [TestCase("<Keyboard>/upArrow", false)]
        [TestCase("<Keyboard>/leftArrow", false)]
        [TestCase("<Gamepad>/buttonSouth", false)]
        public void Keyboard_WhatCanBeBound(string path, bool allowed)
        {
            Assert.AreEqual(allowed, RebindRules.CanBind(map, BindingSet.Keyboard, path));
        }

        [TestCase("<Gamepad>/buttonNorth", true)]
        [TestCase("<Gamepad>/leftShoulder", true)]
        [TestCase("<Gamepad>/rightTrigger", true)]
        [TestCase("<Gamepad>/leftStickPress", true)]
        [TestCase("<Gamepad>/dpad/up", true)]
        [TestCase("<Gamepad>/buttonEast", false)]
        [TestCase("<Gamepad>/start", false)]
        [TestCase("<Gamepad>/select", false)]
        [TestCase("<Gamepad>/leftStick/x", false)]
        [TestCase("<Gamepad>/rightStick/up", false)]
        [TestCase("<Keyboard>/k", false)]
        public void Controller_WhatCanBeBound(string path, bool allowed)
        {
            Assert.AreEqual(allowed, RebindRules.CanBind(map, BindingSet.Controller, path));
        }

        [Test]
        public void Bind_ANewKey()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "BOOST", "<Keyboard>/k");

            Assert.AreEqual("<Keyboard>/k", PathOf(BindingSet.Keyboard, "BOOST"));
            Assert.AreEqual("<Keyboard>/w", PathOf(BindingSet.Keyboard, "ACCELERATE"), "the others stay");
        }

        [Test]
        public void Bind_AKeyInUse_Swaps()
        {
            RebindRules.Bind(map, BindingSet.Controller, "BOOST", "<Gamepad>/buttonWest");

            Assert.AreEqual("<Gamepad>/buttonWest", PathOf(BindingSet.Controller, "BOOST"));
            Assert.AreEqual("<Gamepad>/buttonSouth", PathOf(BindingSet.Controller, "DRIFT"), "Drift gets Boost's old button");
        }

        [Test]
        public void Bind_ACompositesPart_Swaps()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "STEER LEFT", "<Keyboard>/w");

            Assert.AreEqual("<Keyboard>/w", PathOf(BindingSet.Keyboard, "STEER LEFT"));
            Assert.AreEqual("<Keyboard>/a", PathOf(BindingSet.Keyboard, "ACCELERATE"));
        }

        [Test]
        public void Bind_TheActionReallyChanges()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "BOOST", "<Keyboard>/k");

            InputAction boost = map.FindAction("Boost");
            bool kBound = false;
            foreach (InputBinding binding in boost.bindings)
                kBound |= binding.effectivePath == "<Keyboard>/k";
            Assert.IsTrue(kBound);
        }

        [Test]
        public void Reset_TheSet_BackToTheDefaults_TheOtherSetKept()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "BOOST", "<Keyboard>/k");
            RebindRules.Bind(map, BindingSet.Controller, "BOOST", "<Gamepad>/buttonNorth");

            RebindRules.Reset(map, BindingSet.Keyboard);

            Assert.AreEqual("<Keyboard>/space", PathOf(BindingSet.Keyboard, "BOOST"));
            Assert.AreEqual("<Gamepad>/buttonNorth", PathOf(BindingSet.Controller, "BOOST"));
        }

        [TestCase("<Keyboard>/w", GlyphStyle.Keyboard, "W")]
        [TestCase("<Keyboard>/leftShift", GlyphStyle.Keyboard, "LEFT SHIFT")]
        [TestCase("<Gamepad>/buttonSouth", GlyphStyle.Xbox, "A")]
        [TestCase("<Gamepad>/rightTrigger", GlyphStyle.Xbox, "RT")]
        [TestCase("<Gamepad>/rightStickPress", GlyphStyle.Xbox, "RS")]
        [TestCase("<Gamepad>/buttonSouth", GlyphStyle.PlayStation, "CROSS")]
        [TestCase("<Gamepad>/rightTrigger", GlyphStyle.PlayStation, "R2")]
        [TestCase("<Gamepad>/dpad/up", GlyphStyle.Xbox, "D-PAD UP")]
        public void Display_TheKeyOrButtonsName(string path, GlyphStyle style, string shown)
        {
            Assert.AreEqual(shown, RebindRules.Display(path, style));
        }

        [Test]
        public void Store_EachSetLoadsBack()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "BOOST", "<Keyboard>/k");
            RebindRules.Bind(map, BindingSet.Controller, "DRIFT", "<Gamepad>/buttonNorth");
            BindingStore store = new BindingStore();
            store.Keep(1, map);

            BindingStore loaded = BindingStore.Parse(store.ToText());
            InputActionAsset fresh = Object.Instantiate(Resources.Load<InputActionAsset>(KeyboardControls.ASSET));
            try
            {
                InputActionMap freshMap = fresh.FindActionMap("Player");
                loaded.Apply(1, freshMap);

                map = freshMap;
                Assert.AreEqual("<Keyboard>/k", PathOf(BindingSet.Keyboard, "BOOST"));
                Assert.AreEqual("<Gamepad>/buttonNorth", PathOf(BindingSet.Controller, "DRIFT"));
                Assert.AreEqual("<Keyboard>/w", PathOf(BindingSet.Keyboard, "ACCELERATE"), "unchanged ones stay the defaults");
            }
            finally
            {
                Object.DestroyImmediate(fresh);
            }
        }

        [Test]
        public void Store_ASeatWithNothing_AppliesNothing()
        {
            BindingStore.Parse("").Apply(3, map);

            Assert.AreEqual("<Keyboard>/space", PathOf(BindingSet.Keyboard, "BOOST"));
        }

        [Test]
        public void Store_ADamagedLine_LosesOnlyThatSet()
        {
            string text = "seat1.keyboard=BOOST:<Keyboard>/k\nseat1.controller=???garbage\nseat2.keyboard=DRIFT:<Keyboard>/escape;NOPE:<Keyboard>/j\n";

            BindingStore loaded = BindingStore.Parse(text);
            loaded.Apply(1, map);
            Assert.AreEqual("<Keyboard>/k", PathOf(BindingSet.Keyboard, "BOOST"));
            Assert.AreEqual("<Gamepad>/buttonSouth", PathOf(BindingSet.Controller, "BOOST"));

            RebindRules.Reset(map, BindingSet.Keyboard);
            loaded.Apply(2, map);
            Assert.AreEqual("<Keyboard>/leftShift", PathOf(BindingSet.Keyboard, "DRIFT"), "Escape can't be bound: skipped");
        }

        [Test]
        public void Store_Apply_ClearsWhatTheLastSeatsPlayerHad()
        {
            RebindRules.Bind(map, BindingSet.Keyboard, "BOOST", "<Keyboard>/k");

            BindingStore.Parse("").Apply(2, map);

            Assert.AreEqual("<Keyboard>/space", PathOf(BindingSet.Keyboard, "BOOST"), "seat 2 has no changes of its own");
        }

        [Test]
        public void Listener_APress_IsTheGenericPathTheMapBinds()
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>("Rebind Test Keyboard");
            Gamepad pad = InputSystem.AddDevice<Gamepad>("Rebind Test Pad");
            try
            {
                Assert.AreEqual("<Keyboard>/k", RebindListener.GenericPath(keyboard.kKey));
                Assert.AreEqual("<Gamepad>/buttonNorth", RebindListener.GenericPath(pad.buttonNorth));
                Assert.AreEqual("<Gamepad>/dpad/up", RebindListener.GenericPath(pad.dpad.up));
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(pad);
            }
        }

        [Test]
        public void Store_TestPlayers_NeverPickUpThisPCsSavedControls()
        {
            BindingStore.UseNoneSaved();

            Assert.AreEqual("", BindingStore.Current.ToText(), "test players drive on the game's own controls");
        }

        [Test]
        public void Store_SeatsOutOfRange_AreIgnored()
        {
            BindingStore loaded = BindingStore.Parse("seat9.keyboard=BOOST:<Keyboard>/k\nseat0.keyboard=BOOST:<Keyboard>/k\n");

            StringAssert.DoesNotContain("seat9", loaded.ToText());
            StringAssert.DoesNotContain("seat0", loaded.ToText());
        }
    }
}
