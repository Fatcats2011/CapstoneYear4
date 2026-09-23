using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>
/// Developer tool: virtual controllers that join as extra players, so 2-4 player split-screen, controller disconnects
/// and 4-player performance can be tested with one real controller. Driven by HotKeys (editor and development builds only).
/// </summary>
public static class TestPlayers
{
    public const string PAD_NAME = "DoA Test Pad";

    static readonly List<Gamepad> pads = new List<Gamepad>();
    static bool removeOnQuitHooked;

    /// <summary>
    /// Test controllers added so far, oldest first (unplugged ones included)
    /// </summary>
    public static IReadOnlyList<Gamepad> Pads => pads;

    ///<summary>
    /// Plugs in a virtual controller and presses A on it, which joins it as the next player
    ///</summary>
    public static Gamepad Add()
    {
        if (!removeOnQuitHooked)
        {
            // Devices outlive Play Mode in the editor, so test controllers are removed when it ends
            Application.quitting += RemoveAll;
            removeOnQuitHooked = true;
        }

        Gamepad pad = InputSystem.AddDevice<Gamepad>(PAD_NAME);
        pads.Add(pad);
        PressSouth(pad);
        return pad;
    }

    ///<summary>
    /// Presses A on every plugged-in test controller (ready up, confirm the loading screen)
    ///</summary>
    public static void PressSouthOnAll()
    {
        foreach (Gamepad pad in pads)
        {
            if (pad.added)
                PressSouth(pad);
        }
    }

    ///<summary>
    /// Unplugs the newest test controller, or plugs it back in if it's unplugged
    ///</summary>
    public static void ToggleNewest()
    {
        if (pads.Count == 0)
            return;

        Gamepad newest = pads[pads.Count - 1];
        if (newest.added)
            InputSystem.RemoveDevice(newest);
        else
            InputSystem.AddDevice(newest);
    }

    ///<summary>
    /// Unplugs and forgets every test controller
    ///</summary>
    public static void RemoveAll()
    {
        foreach (Gamepad pad in pads)
        {
            if (pad.added)
                InputSystem.RemoveDevice(pad);
        }
        pads.Clear();

        Application.quitting -= RemoveAll;
        removeOnQuitHooked = false;
    }

    ///<summary>
    /// Queues a press and release of A; the next input update sees a full button press
    ///</summary>
    static void PressSouth(Gamepad pad)
    {
        InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(GamepadButton.South));
        InputSystem.QueueStateEvent(pad, new GamepadState());
    }
}
