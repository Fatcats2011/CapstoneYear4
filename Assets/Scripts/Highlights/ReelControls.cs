using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The players' controls while the highlight reel plays. The reel comes in the match, where the UI map (which the cutscene
/// skip reads, "UI/ReadyUp") is off, so nobody could vote to skip it (playtest, 2026-11-25):
/// - Each local player's skip is read straight from their own devices: a pad's south button (A / cross), or Space or
///   Enter on their keyboard (Pressed).
/// - Boost and pause are off meanwhile (Take), so the A held to skip doesn't boost the hidden scooter or open the pause
///   menu, and an open pause menu closes as the reel starts (online it doesn't stop the game). Give puts them back,
///   unless the game has moved the controls on since (their map switched off: a scene load, the menus).
/// </summary>
internal class ReelControls
{
    /// <summary>The Player map's actions a skip press would also work, off while the reel plays</summary>
    internal static readonly string[] HELD_OFF = { BindingStore.PLAYER_MAP + "/Boost", BindingStore.PLAYER_MAP + "/Pause" };

    readonly List<InputAction> switchedOff = new List<InputAction>();

    /// <summary>A seat's skip held now: a local player's skip button on their own devices (another machine's: never)</summary>
    internal static bool Pressed(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        PlayerSlot slot = players != null && seat >= 0 && seat < Constants.MAX_PLAYERS ? players.Roster[seat] : null;
        return slot != null && slot.IsLocal && slot.Input != null && SkipHeld(slot.Input.devices);
    }

    /// <summary>Whether any of these devices holds the skip: a pad's south button, or Space, Enter or the keypad's Enter</summary>
    internal static bool SkipHeld(IEnumerable<InputDevice> devices)
    {
        foreach (InputDevice device in devices)
        {
            if (device is Gamepad pad && pad.buttonSouth.isPressed)
                return true;
            if (device is Keyboard keys && (keys.spaceKey.isPressed || keys.enterKey.isPressed || keys.numpadEnterKey.isPressed))
                return true;
        }
        return false;
    }

    /// <summary>The reel starts: an open pause menu closes, and every local player's boost and pause go off</summary>
    internal void Take()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null)
            return;

        if (players.IsPaused)
        {
            if (SoundManager.Instance != null)
                SoundManager.Instance.ChangeSnapshot("gameplay");
            players.PlayerPlay();
        }

        List<InputActionAsset> assets = new List<InputActionAsset>();
        foreach (PlayerSlot slot in players.Roster.LocalPlayers)
        {
            if (slot.Input != null && slot.Input.actions != null)
                assets.Add(slot.Input.actions);
        }
        Take(assets);
    }

    /// <summary>Switches off HELD_OFF in each of these controls, remembering which were on</summary>
    internal void Take(IEnumerable<InputActionAsset> assets)
    {
        foreach (InputActionAsset asset in assets)
        {
            foreach (string name in HELD_OFF)
            {
                InputAction action = asset.FindAction(name);
                if (action == null || !action.enabled)
                    continue;
                action.Disable();
                switchedOff.Add(action);
            }
        }
    }

    /// <summary>
    /// The reel is over: what Take switched off comes back on, where its map is still on (a map the game has switched
    /// off since stays as the game left it). Safe to call again
    /// </summary>
    internal void Give()
    {
        foreach (InputAction action in switchedOff)
        {
            if (action != null && action.actionMap != null && action.actionMap.enabled && !action.enabled)
                action.Enable();
        }
        switchedOff.Clear();
    }
}
