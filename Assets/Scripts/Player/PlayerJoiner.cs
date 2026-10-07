using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>
/// Joins players: a controller nobody uses joins on any button, a keyboard only on Space or Enter. The game asks
/// PlayerInputManager for each join itself (its joins are set to manual), so another key makes no player at all, where
/// Unity used to make one for any key and the game destroyed it. A key that doesn't join is reported (PlayerInstantiate
/// says how to join). Joins wait while PlayerInputManager's joining is off (ReplacementControllerListener turns it off
/// while a lost controller's player waits for a replacement). See docs/controls.md
/// </summary>
public static class PlayerJoiner
{
    /// <summary>The control scheme a controller joins with</summary>
    public const string GAMEPAD_SCHEME = "Gamepad";

    static Action<InputDevice> turnedAway;

    /// <summary>Whether the game joins players now (from PlayerInstantiate's OnEnable to its OnDisable)</summary>
    public static bool Enabled { get; private set; }

    /// <summary>
    /// Starts joining players, and sets PlayerInputManager's own joins to manual
    /// </summary>
    /// <param name="onTurnedAway">A key that doesn't join was pressed on a keyboard nobody uses</param>
    public static void Enable(Action<InputDevice> onTurnedAway)
    {
        turnedAway = onTurnedAway;
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
        turnedAway = null;
        InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
        InputUser.listenForUnpairedDeviceActivity--;
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
    /// The control scheme a press joins with: a controller's for any of its buttons, the keyboard's for Space or Enter,
    /// else null. The press is read from its event: a tap can be up again by the time the join runs
    /// </summary>
    public static string SchemeFor(InputControl control, InputEventPtr eventPtr)
    {
        if (!(control is ButtonControl button) || !Pressed(button, eventPtr))
            return null;

        if (control.device is Gamepad)
            return GAMEPAD_SCHEME;

        if (control.device is Keyboard keyboard
            && (control == keyboard.spaceKey || control == keyboard.enterKey || control == keyboard.numpadEnterKey))
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

        string scheme = SchemeFor(control, eventPtr);
        if (scheme != null)
        {
            manager.JoinPlayer(-1, -1, scheme, control.device);
            return;
        }

        // Another key on a keyboard nobody uses: the game says how to join
        if (control.device is Keyboard && control is ButtonControl key && Pressed(key, eventPtr))
            turnedAway?.Invoke(control.device);
    }
}
