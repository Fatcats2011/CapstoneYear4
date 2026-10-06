using System.Linq;
using UnityEngine.InputSystem.Controls;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>
/// The keyboard as one player: a keyboard-only scheme and a key for every action, added in memory to the game's input
/// actions (Resources/CapstoneYear4) before the first scene loads, so the asset on disk never changes. The asset's own
/// Keyboard&amp;Mouse scheme has no bindings and needs a mouse too, so it's swapped for this one. In the editor the asset is
/// put back when Play Mode ends; installing twice changes nothing. A keyboard joins only on Space or Enter
/// (PlayerInstantiate.AddPlayerReference). The keys are in docs/controls.md
/// </summary>
public static class KeyboardControls
{
    public const string SCHEME = "Keyboard";
    public const string OLD_SCHEME = "Keyboard&Mouse";
    public const string ASSET = "CapstoneYear4";

    static bool undoOnQuit;
    static Keyboard joinKeyboard; // the keyboard whose Space or Enter was pressed while it was nobody's,
    static int joinFrame = -1;    // and on which frame

    /// <summary>
    /// Whether this frame a keyboard that was nobody's had Space or Enter pressed: the join that press starts may run when
    /// the key is already up again (a tap within one update), so the press is read from its event
    /// </summary>
    public static bool JoinKeyUsedThisFrame(Keyboard keyboard)
    {
        return keyboard != null && keyboard == joinKeyboard && joinFrame == Time.frameCount;
    }

    // Before the scene's PlayerInputManager subscribes, so this runs before the join it starts
    static void OnUnpairedDeviceUsed(InputControl control, InputEventPtr inputEvent)
    {
        if (!(control.device is Keyboard keyboard))
            return;

        if (PressedIn(keyboard.spaceKey, inputEvent) || PressedIn(keyboard.enterKey, inputEvent) || PressedIn(keyboard.numpadEnterKey, inputEvent))
        {
            joinKeyboard = keyboard;
            joinFrame = Time.frameCount;
        }
    }

    static bool PressedIn(KeyControl key, InputEventPtr inputEvent)
    {
        return key.ReadValueFromEvent(inputEvent, out float value) && value > 0.5f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void InstallOnLaunch()
    {
        InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
        InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;

        InputActionAsset asset = Resources.Load<InputActionAsset>(ASSET);
        if (asset == null || !Install(asset))
            return;

        // In the editor the asset is a project file, loaded once: Play Mode leaves it as it found it
        if (Application.isEditor && !undoOnQuit)
        {
            undoOnQuit = true;
            Application.quitting += UninstallOnQuit;
        }
    }

    static void UninstallOnQuit()
    {
        Application.quitting -= UninstallOnQuit;
        undoOnQuit = false;
        InputActionAsset asset = Resources.Load<InputActionAsset>(ASSET);
        if (asset == null)
            return;

        asset.Disable();
        Uninstall(asset);
    }

    /// <summary>
    /// Adds the keyboard scheme and its keys. False when the asset has them already
    /// </summary>
    public static bool Install(InputActionAsset asset)
    {
        if (HasScheme(asset, SCHEME))
            return false;

        if (HasScheme(asset, OLD_SCHEME))
            asset.RemoveControlScheme(OLD_SCHEME);
        asset.AddControlScheme(SCHEME).WithRequiredDevice("<Keyboard>");

        // Driving (the Player map)
        Axis(asset, "Player/Steer", "a", "d");
        Axis(asset, "Player/Steer", "leftArrow", "rightArrow");
        Keys(asset, "Player/Accelerate", "w", "upArrow");
        Keys(asset, "Player/Brake", "s", "downArrow");
        Keys(asset, "Player/Boost", "space");
        Keys(asset, "Player/Drift", "leftShift");
        Axis(asset, "Player/Camera Rotation X", "q", "e");
        Axis(asset, "Player/Camera Rotation Y", "z", "c");
        Keys(asset, "Player/Camera Swap", "tab");
        Keys(asset, "Player/Pause", "escape");

        // Menus (the UI map)
        Keys(asset, "UI/ReadyUp", "space", "enter", "numpadEnter");
        Keys(asset, "UI/UnReadyUp", "backspace");
        Keys(asset, "UI/Left D-Pad", "a", "leftArrow");
        Keys(asset, "UI/Right D-Pad", "d", "rightArrow");
        Keys(asset, "UI/Up D-Pad", "w", "upArrow");
        Keys(asset, "UI/Down D-Pad", "s", "downArrow");
        Keys(asset, "UI/UnPause", "escape");
        Keys(asset, "UI/North Face", "tab");

        // The loading screen (the Load map)
        Keys(asset, "Load/Confirm", "space", "enter", "numpadEnter");
        return true;
    }

    /// <summary>
    /// Takes the keyboard scheme and its keys out again, and brings back the asset's own Keyboard&amp;Mouse scheme
    /// </summary>
    public static void Uninstall(InputActionAsset asset)
    {
        foreach (InputActionMap map in asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                for (int i = action.bindings.Count - 1; i >= 0; i--)
                {
                    InputBinding binding = action.bindings[i];
                    if (binding.isPartOfComposite)
                        continue;

                    bool ours = binding.isComposite ? CompositeIsOurs(action, i) : InGroup(binding);
                    if (ours)
                        action.ChangeBinding(i).Erase(); // a composite goes with its parts
                }
            }
        }

        if (HasScheme(asset, SCHEME))
            asset.RemoveControlScheme(SCHEME);
        if (!HasScheme(asset, OLD_SCHEME))
            asset.AddControlScheme(OLD_SCHEME).WithRequiredDevice("<Keyboard>").WithRequiredDevice("<Mouse>");
    }

    static bool HasScheme(InputActionAsset asset, string name)
    {
        return asset.controlSchemes.Any(scheme => scheme.name == name);
    }

    static bool InGroup(InputBinding binding)
    {
        return binding.groups != null && binding.groups.Split(InputBinding.Separator).Contains(SCHEME);
    }

    // A composite is ours when its parts are
    static bool CompositeIsOurs(InputAction action, int composite)
    {
        int next = composite + 1;
        return next < action.bindings.Count && action.bindings[next].isPartOfComposite && InGroup(action.bindings[next]);
    }

    static void Keys(InputActionAsset asset, string action, params string[] keys)
    {
        InputAction found = asset.FindAction(action, throwIfNotFound: true);
        foreach (string key in keys)
            found.AddBinding("<Keyboard>/" + key, groups: SCHEME);
    }

    static void Axis(InputActionAsset asset, string action, string negative, string positive)
    {
        asset.FindAction(action, throwIfNotFound: true).AddCompositeBinding("1DAxis")
            .With("Negative", "<Keyboard>/" + negative, SCHEME)
            .With("Positive", "<Keyboard>/" + positive, SCHEME);
    }
}
