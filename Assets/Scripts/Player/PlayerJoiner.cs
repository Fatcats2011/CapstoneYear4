using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>
/// Joins players: a controller nobody uses joins on any button, a keyboard on any key but the function keys (the
/// developer hotkeys). The game asks PlayerInputManager for each join itself (its joins are set to manual). While the game
/// takes nobody new (Welcomes: mid-match, the title screen once someone's in, a full player select), the device goes to
/// the one player here instead, so whatever they last used is what they play on (playtest, 2026-10-24). Nothing happens
/// while PlayerInputManager's joining is off (ReplacementControllerListener turns it off while a lost controller's player
/// waits for a replacement). See docs/controls.md
/// </summary>
public static class PlayerJoiner
{
    /// <summary>The control scheme a controller joins with</summary>
    public const string GAMEPAD_SCHEME = "Gamepad";

    static Func<bool> welcomes;
    static Action<InputDevice, string> handOver;

    /// <summary>Whether the game joins players now (from PlayerInstantiate's OnEnable to its OnDisable)</summary>
    public static bool Enabled { get; private set; }

    /// <summary>
    /// Starts joining players, and sets PlayerInputManager's own joins to manual
    /// </summary>
    /// <param name="welcomesJoins">Whether the game takes a new player now (null: always)</param>
    /// <param name="handsOver">While the game takes nobody new: a device nobody uses was used (SchemeFor), with its
    /// scheme. The game gives it to its one player here, if there's one</param>
    public static void Enable(Func<bool> welcomesJoins = null, Action<InputDevice, string> handsOver = null)
    {
        welcomes = welcomesJoins;
        handOver = handsOver;
        UseManualJoins();
        if (Enabled)
            return;

        Enabled = true;
        InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;
        InputUser.listenForUnpairedDeviceActivity++;
    }

    /// <summary>Stops joining players</summary>
    public static void Disable()
    {
        if (!Enabled)
            return;

        Enabled = false;
        welcomes = null;
        handOver = null;
        InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
        InputUser.listenForUnpairedDeviceActivity--;
    }

    /// <summary>
    /// Whether the game takes a new player now. Offline: the first at any time, more while players may join (player select)
    /// up to four. Online: this machine's first at any time, more only in player select (a full match is told so there,
    /// by PlayerInstantiate). A player moving seats joins by itself, without this
    /// </summary>
    /// <param name="allowSpawn">PlayerInstantiate's allowPlayerSpawn: players may join (offline)</param>
    /// <param name="onePerMachine">OnlineRules.OnePlayerPerMachine: online, this machine's first player is its only one</param>
    public static bool Welcomes(bool online, bool allowSpawn, int playerCount, int localCount, bool inPlayerSelect,
        bool onePerMachine)
    {
        if (online)
            return (localCount == 0 || inPlayerSelect) && (!onePerMachine || localCount == 0);

        return (allowSpawn || playerCount == 0) && playerCount < Constants.MAX_PLAYERS;
    }

    /// <summary>
    /// PlayerInputManager joins nobody by itself: the scene sets it to join on any button
    /// </summary>
    public static void UseManualJoins()
    {
        PlayerInputManager manager = PlayerInputManager.instance;
        if (manager != null && manager.joinBehavior != PlayerJoinBehavior.JoinPlayersManually)
            manager.joinBehavior = PlayerJoinBehavior.JoinPlayersManually;
    }

    /// <summary>
    /// The control scheme a press joins with, or hands its device over with: a controller's for any of its buttons, the
    /// keyboard's for any key but the function keys (developer hotkeys), else null. The press is read from its event: a
    /// tap can be up again by the time the join runs
    /// </summary>
    public static string SchemeFor(InputControl control, InputEventPtr eventPtr)
    {
        if (!(control is ButtonControl button) || !Pressed(button, eventPtr))
            return null;

        if (control.device is Gamepad)
            return GAMEPAD_SCHEME;

        if (control.device is Keyboard && !(control is KeyControl key && key.keyCode >= Key.F1 && key.keyCode <= Key.F12))
            return KeyboardControls.SCHEME;

        return null;
    }

    static bool Pressed(ButtonControl button, InputEventPtr eventPtr)
    {
        return button.ReadValueFromEvent(eventPtr, out float value) && value >= button.pressPointOrDefault;
    }

    static void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
    {
        PlayerInputManager manager = PlayerInputManager.instance;
        if (manager == null || !manager.joiningEnabled)
            return;

        // Nobody new now: the device may go to the player already here
        if (welcomes != null && !welcomes())
        {
            string handedWith = handOver != null ? SchemeFor(control, eventPtr) : null;
            if (handedWith != null)
                handOver(control.device, handedWith);
            return;
        }

        string scheme = SchemeFor(control, eventPtr);
        if (scheme != null)
            manager.JoinPlayer(-1, -1, scheme, control.device);
    }
}
