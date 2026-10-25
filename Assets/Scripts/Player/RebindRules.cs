using System.Collections.Generic;
using UnityEngine.InputSystem;

/// <summary>Which of a player's two binding sets: the keyboard's keys, or the controller's buttons</summary>
public enum BindingSet { Keyboard, Controller }

/// <summary>A driving control a player can rebind: its name on the Controls panel and its binding in the Player map</summary>
public readonly struct RebindSlot
{
    public readonly string Name;
    public readonly string Action;
    public readonly int BindingIndex;

    public RebindSlot(string name, string action, int bindingIndex)
    {
        Name = name;
        Action = action;
        BindingIndex = bindingIndex;
    }
}

/// <summary>
/// The rules of rebinding the driving controls (the Controls panel in the pause menu), on a player's own copy of the
/// Player map:
/// - Keyboard: the first key of each driving action (W, S, A / D, Space, Left Shift, Tab, Q / E, Z / C). The second
///   keys (the arrows) stay as fixed alternates and can't be bound.
/// - Controller: the triggers' and buttons' actions; the sticks stay as they are.
/// - Never bound: Pause (Start / Escape), Back while waiting (B / circle, Backspace), the sticks' directions.
/// - A key or button another of the set's controls has swaps: nothing is ever left unbound.
/// Changes are binding overrides, so Reset brings back the asset's (and KeyboardControls') keys.
/// See docs/superpowers/specs/2026-10-23-control-rebinding-design.md
/// </summary>
public static class RebindRules
{
    const string KEYBOARD = "<Keyboard>/";
    const string GAMEPAD = "<Gamepad>/";

    // Each set's controls: the name shown, the action, and the composite part (null: a plain binding)
    static readonly string[,] KEYBOARD_SLOTS =
    {
        { "ACCELERATE", "Accelerate", null },
        { "BRAKE", "Brake", null },
        { "STEER LEFT", "Steer", "Negative" },
        { "STEER RIGHT", "Steer", "Positive" },
        { "BOOST", "Boost", null },
        { "DRIFT", "Drift", null },
        { "SWAP CAMERA", "Camera Swap", null },
        { "CAMERA LEFT", "Camera Rotation X", "Negative" },
        { "CAMERA RIGHT", "Camera Rotation X", "Positive" },
        { "CAMERA UP", "Camera Rotation Y", "Negative" },
        { "CAMERA DOWN", "Camera Rotation Y", "Positive" },
    };

    static readonly string[,] CONTROLLER_SLOTS =
    {
        { "ACCELERATE", "Accelerate", null },
        { "BRAKE", "Brake", null },
        { "BOOST", "Boost", null },
        { "DRIFT", "Drift", null },
        { "SWAP CAMERA", "Camera Swap", null },
    };

    static readonly HashSet<string> NEVER_KEYS = new HashSet<string> { "escape", "backspace", "anyKey" };

    static readonly HashSet<string> CONTROLLER_ALLOWED = new HashSet<string>
    {
        "buttonSouth", "buttonWest", "buttonNorth", "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
        "leftStickPress", "rightStickPress", "dpad/up", "dpad/down", "dpad/left", "dpad/right",
    };

    // The names shown for a controller's buttons: Xbox's, and PlayStation's where they differ
    static readonly Dictionary<string, string> XBOX_NAMES = new Dictionary<string, string>
    {
        { "buttonSouth", "A" }, { "buttonEast", "B" }, { "buttonWest", "X" }, { "buttonNorth", "Y" },
        { "leftShoulder", "LB" }, { "rightShoulder", "RB" }, { "leftTrigger", "LT" }, { "rightTrigger", "RT" },
        { "leftStickPress", "LS" }, { "rightStickPress", "RS" },
        { "dpad/up", "controls.dpad.up" }, { "dpad/down", "controls.dpad.down" }, { "dpad/left", "controls.dpad.left" }, { "dpad/right", "controls.dpad.right" },
    };

    static readonly Dictionary<string, string> PLAYSTATION_NAMES = new Dictionary<string, string>
    {
        { "buttonSouth", "controls.ps.cross" }, { "buttonEast", "controls.ps.circle" }, { "buttonWest", "controls.ps.square" }, { "buttonNorth", "controls.ps.triangle" },
        { "leftShoulder", "L1" }, { "rightShoulder", "R1" }, { "leftTrigger", "L2" }, { "rightTrigger", "R2" },
        { "leftStickPress", "L3" }, { "rightStickPress", "R3" },
    };

    /// <summary>A set's controls, in the panel's order, with their bindings in this map</summary>
    public static List<RebindSlot> SlotsFor(InputActionMap map, BindingSet set)
    {
        string[,] table = set == BindingSet.Keyboard ? KEYBOARD_SLOTS : CONTROLLER_SLOTS;
        List<RebindSlot> slots = new List<RebindSlot>();
        for (int i = 0; i < table.GetLength(0); i++)
        {
            InputAction action = map.FindAction(table[i, 1]);
            int index = action != null ? IndexOf(action, set, table[i, 2]) : -1;
            if (index >= 0)
                slots.Add(new RebindSlot(table[i, 0], table[i, 1], index));
        }
        return slots;
    }

    // The binding a control is: the set's first plain binding, or its first composite's part
    static int IndexOf(InputAction action, BindingSet set, string part)
    {
        string device = set == BindingSet.Keyboard ? KEYBOARD : GAMEPAD;
        var bindings = action.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            if (part == null)
            {
                if (!bindings[i].isComposite && !bindings[i].isPartOfComposite && bindings[i].path.StartsWith(device))
                    return i;
                continue;
            }

            if (!bindings[i].isComposite)
                continue;
            for (int p = i + 1; p < bindings.Count && bindings[p].isPartOfComposite; p++)
            {
                if (bindings[p].name == part && bindings[p].path.StartsWith(device))
                    return p;
            }
        }
        return -1;
    }

    /// <summary>The key or button a control is on now</summary>
    public static string PathOf(InputActionMap map, RebindSlot slot)
    {
        return map.FindAction(slot.Action).bindings[slot.BindingIndex].effectivePath;
    }

    /// <summary>Whether a press can be bound in a set (a refused one keeps the panel waiting)</summary>
    public static bool CanBind(InputActionMap map, BindingSet set, string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        if (set == BindingSet.Controller)
            return path.StartsWith(GAMEPAD) && CONTROLLER_ALLOWED.Contains(path.Substring(GAMEPAD.Length));

        if (!path.StartsWith(KEYBOARD) || NEVER_KEYS.Contains(path.Substring(KEYBOARD.Length)))
            return false;
        return !FixedKeys(map).Contains(path);
    }

    // The keyboard's second keys (the arrows): bound to driving actions, but not by the panel
    static HashSet<string> FixedKeys(InputActionMap map)
    {
        HashSet<int> slotted = new HashSet<int>();
        HashSet<string> actions = new HashSet<string>();
        foreach (RebindSlot slot in SlotsFor(map, BindingSet.Keyboard))
        {
            slotted.Add(Key(slot.Action, slot.BindingIndex));
            actions.Add(slot.Action);
        }

        HashSet<string> keys = new HashSet<string>();
        foreach (string name in actions)
        {
            InputAction action = map.FindAction(name);
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (!binding.isComposite && binding.effectivePath.StartsWith(KEYBOARD) && !slotted.Contains(Key(name, i)))
                    keys.Add(binding.effectivePath);
            }
        }
        return keys;
    }

    static int Key(string action, int index)
    {
        return action.GetHashCode() * 31 + index;
    }

    /// <summary>
    /// Puts a control on a key or button. If another of the set's controls has it, that one takes this one's old key or
    /// button. False when the set has no such control
    /// </summary>
    public static bool Bind(InputActionMap map, BindingSet set, string name, string path)
    {
        List<RebindSlot> slots = SlotsFor(map, set);
        int chosen = slots.FindIndex(slot => slot.Name == name);
        if (chosen < 0)
            return false;

        string old = PathOf(map, slots[chosen]);
        for (int i = 0; i < slots.Count; i++)
        {
            if (i != chosen && PathOf(map, slots[i]) == path)
                Override(map, slots[i], old);
        }
        Override(map, slots[chosen], path);
        return true;
    }

    /// <summary>The set back to the game's own keys or buttons</summary>
    public static void Reset(InputActionMap map, BindingSet set)
    {
        foreach (RebindSlot slot in SlotsFor(map, set))
            map.FindAction(slot.Action).RemoveBindingOverride(slot.BindingIndex);
    }

    // A path the binding has anyway is no override
    static void Override(InputActionMap map, RebindSlot slot, string path)
    {
        InputAction action = map.FindAction(slot.Action);
        if (action.bindings[slot.BindingIndex].path == path)
            action.RemoveBindingOverride(slot.BindingIndex);
        else
            action.ApplyBindingOverride(slot.BindingIndex, path);
    }

    /// <summary>A control's name in the language spoken ("STEER LEFT" → controls.steerleft). The slot's own Name stays English: bindings.cfg keys on it</summary>
    public static string Shown(RebindSlot slot)
    {
        return Loc.Get("controls." + slot.Name.Replace(" ", "").ToLowerInvariant());
    }

    // A button's name: a key into the strings ("controls.ps.cross"), or a name every language shares ("RT", "L1")
    static string Shown(string nameOrKey)
    {
        return nameOrKey.StartsWith("controls.") ? Loc.Get(nameOrKey) : nameOrKey;
    }

    /// <summary>The name a key or button shows on the panel: the key's ("W", "LEFT SHIFT"), or the pad's own button</summary>
    public static string Display(string path, GlyphStyle style)
    {
        if (string.IsNullOrEmpty(path))
            return "";
        if (path.StartsWith(GAMEPAD))
        {
            string button = path.Substring(GAMEPAD.Length);
            if (style == GlyphStyle.PlayStation && PLAYSTATION_NAMES.TryGetValue(button, out string playStation))
                return Shown(playStation);
            return XBOX_NAMES.TryGetValue(button, out string xbox) ? Shown(xbox) : button.ToUpperInvariant();
        }
        return InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice).ToUpperInvariant();
    }
}
