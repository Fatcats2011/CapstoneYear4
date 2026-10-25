using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Switch;
using UnityEngine.UI;

/// <summary>
/// Which family of button art a prompt shows
/// </summary>
public enum GlyphStyle
{
    Xbox,
    PlayStation,
    Nintendo,
    Keyboard,
}

/// <summary>
/// Picks button art by device: DualShock 4 and DualSense show PlayStation buttons, a Switch Pro controller Nintendo
/// ones, a keyboard (or mouse) keys, and anything else Xbox. A style with no art yet falls back to the Xbox sprite, so
/// a prompt looks as it always did until its art exists. Note: with Steam Input on, Steam hands the game a virtual
/// Xbox pad, so every controller shows Xbox buttons.
/// </summary>
public static class GlyphRules
{
    public static GlyphStyle StyleFor(InputDevice device)
    {
        switch (device)
        {
            case DualShockGamepad _:
                return GlyphStyle.PlayStation;
            case SwitchProControllerHID _:
                return GlyphStyle.Nintendo;
            case Keyboard _:
            case Mouse _:
                return GlyphStyle.Keyboard;
            default:
                return GlyphStyle.Xbox;
        }
    }

    public static Sprite Pick(GlyphStyle style, Sprite xbox, Sprite playStation, Sprite nintendo, Sprite keyboard)
    {
        Sprite chosen = style == GlyphStyle.PlayStation ? playStation
            : style == GlyphStyle.Nintendo ? nintendo
            : style == GlyphStyle.Keyboard ? keyboard
            : xbox;
        return chosen != null ? chosen : xbox;
    }
}

/// <summary>
/// The device shared prompts follow (the title screen, menus): the last one that drove an action, gamepads and
/// keyboard alike
/// </summary>
public static class LastDevice
{
    static bool listening;

    public static InputDevice Current { get; private set; }
    public static event Action<InputDevice> Changed;

    /// <summary>
    /// Starts following action presses (idempotent)
    /// </summary>
    public static void Listen()
    {
        if (listening)
            return;
        listening = true;
        InputSystem.onActionChange += OnActionChange;
    }

    static void OnActionChange(object action, InputActionChange change)
    {
        if (change == InputActionChange.ActionPerformed && action is InputAction performed)
            Use(performed.activeControl?.device);
    }

    /// <summary>
    /// Makes a device the current one (also used by tests)
    /// </summary>
    public static void Use(InputDevice device)
    {
        if (device == null || device == Current)
            return;
        Current = device;
        Changed?.Invoke(device);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Current = null;
        Changed = null;
        if (listening)
            InputSystem.onActionChange -= OnActionChange;
        listening = false;
    }
}

/// <summary>
/// A button prompt's Image that swaps its sprite to match the controller: inside a player's hierarchy (a PlayerInput
/// above it) it follows that player's device, otherwise the last device used. Leave a style's sprite empty until its
/// art exists; the Image keeps the Xbox sprite meanwhile.
/// </summary>
[RequireComponent(typeof(Image))]
public class ButtonGlyph : MonoBehaviour
{
    [Tooltip("The Xbox button (what the Image shows today); also the fallback for a style with no art")]
    [SerializeField] internal Sprite xbox;
    [SerializeField] internal Sprite playStation;
    [SerializeField] internal Sprite nintendo;
    [SerializeField] internal Sprite keyboard;

    Image image;
    PlayerInput player;

    public GlyphStyle Shown { get; private set; }

    Image Image
    {
        get
        {
            if (image == null)
            {
                image = GetComponent<Image>();
                if (xbox == null)
                    xbox = image.sprite;
            }
            return image;
        }
    }

    void OnEnable()
    {
        player = GetComponentInParent<PlayerInput>();
        if (player != null)
        {
            player.onControlsChanged += OnControlsChanged;
            Show(player.devices.Count > 0 ? player.devices[0] : null);
        }
        else
        {
            LastDevice.Listen();
            LastDevice.Changed += Show;
            Show(LastDevice.Current);
        }
    }

    void OnDisable()
    {
        if (player != null)
            player.onControlsChanged -= OnControlsChanged;
        LastDevice.Changed -= Show;
    }

    void OnControlsChanged(PlayerInput changed)
    {
        Show(changed.devices.Count > 0 ? changed.devices[0] : null);
    }

    internal void Show(InputDevice device)
    {
        Image shownOn = Image;
        Shown = GlyphRules.StyleFor(device);
        shownOn.sprite = GlyphRules.Pick(Shown, xbox, playStation, nintendo, keyboard);

        // A prompt laid out by its button's width (a LayoutElement): a wide key pushes the word along, a round button
        // leaves no gap
        LayoutElement size = GetComponent<LayoutElement>();
        if (size != null && size.preferredHeight > 0 && shownOn.sprite != null && shownOn.sprite.rect.height > 0)
            size.preferredWidth = size.preferredHeight * shownOn.sprite.rect.width / shownOn.sprite.rect.height;
    }
}
