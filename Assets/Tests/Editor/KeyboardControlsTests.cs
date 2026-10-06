using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DoA.Tests
{
    /// <summary>
    /// The keyboard's keys (KeyboardControls), added in memory to a copy of the game's input actions, never the asset on
    /// disk: a keyboard-only scheme instead of the unused Keyboard&amp;Mouse one, a key for every action, the driving keys
    /// working, installing twice changing nothing, uninstalling putting it back, and the pad's bindings untouched
    /// </summary>
    public class KeyboardControlsTests
    {
        readonly List<Object> copies = new List<Object>();
        Keyboard keyboard;

        [TearDown]
        public void TearDown()
        {
            foreach (Object copy in copies)
                Object.DestroyImmediate(copy);
            copies.Clear();
            if (keyboard != null)
                InputSystem.RemoveDevice(keyboard);
            keyboard = null;
        }

        // A copy of the game's actions as on disk (uninstalled, in case a Play Mode run left the loaded asset installed)
        InputActionAsset PristineCopy()
        {
            InputActionAsset copy = InputActionAsset.FromJson(Resources.Load<InputActionAsset>(KeyboardControls.ASSET).ToJson());
            copies.Add(copy);
            KeyboardControls.Uninstall(copy);
            return copy;
        }

        static string[] SchemeNames(InputActionAsset asset)
        {
            return asset.controlSchemes.Select(scheme => scheme.name).ToArray();
        }

        static int BindingCount(InputActionAsset asset)
        {
            return asset.actionMaps.Sum(map => map.bindings.Count);
        }

        static List<string> GamepadBindings(InputActionAsset asset)
        {
            List<string> found = new List<string>();
            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputBinding binding in map.bindings)
                {
                    if (binding.path != null && binding.path.StartsWith("<Gamepad>"))
                        found.Add(map.name + "/" + binding.action + " " + binding.path + " " + binding.groups);
                }
            }
            return found;
        }

        [Test]
        public void Install_AddsAKeyboardOnlyScheme_AndDropsKeyboardAndMouse()
        {
            InputActionAsset asset = PristineCopy();
            CollectionAssert.Contains(SchemeNames(asset), KeyboardControls.OLD_SCHEME, "as on disk");

            Assert.IsTrue(KeyboardControls.Install(asset));

            CollectionAssert.DoesNotContain(SchemeNames(asset), KeyboardControls.OLD_SCHEME);
            InputControlScheme scheme = asset.controlSchemes.First(s => s.name == KeyboardControls.SCHEME);
            Assert.AreEqual(1, scheme.deviceRequirements.Count);
            Assert.AreEqual("<Keyboard>", scheme.deviceRequirements[0].controlPath);
            Assert.IsFalse(scheme.deviceRequirements[0].isOptional);
        }

        [Test]
        public void Install_BindsEveryAction()
        {
            InputActionAsset asset = PristineCopy();
            KeyboardControls.Install(asset);

            foreach (InputActionMap map in asset.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    bool bound = action.bindings.Any(binding => binding.groups != null && binding.groups.Contains(KeyboardControls.SCHEME));
                    Assert.IsTrue(bound, map.name + "/" + action.name + " has a key");
                }
            }
        }

        [Test]
        public void Install_TheDrivingKeys()
        {
            InputActionAsset asset = PristineCopy();
            KeyboardControls.Install(asset);
            keyboard = InputSystem.AddDevice<Keyboard>("DoA Test Keyboard");
            asset.devices = new InputDevice[] { keyboard };
            asset.bindingMask = InputBinding.MaskByGroup(KeyboardControls.SCHEME);
            InputActionMap player = asset.FindActionMap("Player", true);

            // Each key path names a real key on a keyboard (EditMode doesn't feed key presses to actions: Play Mode tests
            // press them, KeyboardPlayerTests)
            CollectionAssert.AreEquivalent(new InputControl[] { keyboard.wKey, keyboard.upArrowKey }, player.FindAction("Accelerate", true).controls.ToArray());
            CollectionAssert.AreEquivalent(new InputControl[] { keyboard.sKey, keyboard.downArrowKey }, player.FindAction("Brake", true).controls.ToArray());
            CollectionAssert.AreEquivalent(new InputControl[] { keyboard.aKey, keyboard.dKey, keyboard.leftArrowKey, keyboard.rightArrowKey },
                player.FindAction("Steer", true).controls.ToArray());
            CollectionAssert.AreEquivalent(new InputControl[] { keyboard.spaceKey }, player.FindAction("Boost", true).controls.ToArray());
        }

        [Test]
        public void Install_Twice_ChangesNothing()
        {
            InputActionAsset asset = PristineCopy();
            KeyboardControls.Install(asset);
            int bindings = BindingCount(asset);

            Assert.IsFalse(KeyboardControls.Install(asset));
            Assert.AreEqual(bindings, BindingCount(asset));
        }

        [Test]
        public void Uninstall_PutsTheAssetBack()
        {
            InputActionAsset pristine = PristineCopy();
            InputActionAsset asset = PristineCopy();
            KeyboardControls.Install(asset);

            KeyboardControls.Uninstall(asset);

            Assert.AreEqual(BindingCount(pristine), BindingCount(asset));
            CollectionAssert.AreEquivalent(SchemeNames(pristine), SchemeNames(asset));
        }

        [Test]
        public void TheGamepadBindings_AreUntouched()
        {
            InputActionAsset pristine = PristineCopy();
            InputActionAsset asset = PristineCopy();

            KeyboardControls.Install(asset);

            CollectionAssert.AreEquivalent(GamepadBindings(pristine), GamepadBindings(asset));
        }
    }
}
