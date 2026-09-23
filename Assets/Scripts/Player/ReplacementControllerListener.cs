using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

/// <summary>
/// While a player's controller is missing, catches button presses on controllers nobody is using, so any controller
/// can take that player's place. New players can't join meanwhile, otherwise the press would add a new player
/// instead (and with four players nobody could join at all).
/// </summary>
public class ReplacementControllerListener
{
    readonly Action<Gamepad> onGamepadPressed;
    readonly Action<InputDevice> onOtherDevicePressed;

    public bool IsListening { get; private set; }

    public ReplacementControllerListener(Action<Gamepad> onGamepadPressed, Action<InputDevice> onOtherDevicePressed)
    {
        this.onGamepadPressed = onGamepadPressed;
        this.onOtherDevicePressed = onOtherDevicePressed;
    }

    ///<summary>
    /// Starts or stops listening; asking for the current state again does nothing
    ///</summary>
    public void SetListening(bool listen)
    {
        if (listen == IsListening)
            return;

        IsListening = listen;

        if (listen)
        {
            InputUser.onUnpairedDeviceUsed += OnUnpairedDeviceUsed;
            InputUser.listenForUnpairedDeviceActivity++;
        }
        else
        {
            InputUser.onUnpairedDeviceUsed -= OnUnpairedDeviceUsed;
            InputUser.listenForUnpairedDeviceActivity--;
        }

        // The game always allows joining (PlayerInstantiate decides who may spawn), so stopping turns it back on
        PlayerInputManager joinManager = PlayerInputManager.instance;
        if (joinManager != null)
        {
            if (listen)
                joinManager.DisableJoining();
            else
                joinManager.EnableJoining();
        }
    }

    private void OnUnpairedDeviceUsed(InputControl control, InputEventPtr eventPtr)
    {
        // Only button presses count, not stick movement
        if (!(control is ButtonControl))
            return;

        if (control.device is Gamepad gamepad)
            onGamepadPressed(gamepad);
        else
            onOtherDevicePressed(control.device);
    }
}
