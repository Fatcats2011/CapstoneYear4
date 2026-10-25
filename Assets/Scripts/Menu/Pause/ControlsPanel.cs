using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

/// <summary>
/// The Controls panel, in a player's own pause menu, built by code: "CONTROLS — KEYBOARD" or "— CONTROLLER" (the set of
/// the device they're using when it opens), a row per control of that set with its key or button, and RESET TO DEFAULTS.
/// - A on a row waits for the next press on that player's own device ("PRESS A KEY..."), from the next frame (the A that
///   chose the row doesn't count). A press that can't be bound (RebindRules) keeps it waiting; Back (B / circle,
///   Backspace) cancels. One in use swaps.
/// - A on Reset resets the set. Back closes the panel, and closing saves the seat's controls (bindings.cfg).
/// - While waiting, and the frame a press lands, the menu buttons do nothing (Space is the menus' A too).
/// See docs/superpowers/specs/2026-10-23-control-rebinding-design.md
/// </summary>
public class ControlsPanel : MonoBehaviour
{
    /// <summary>The last row's text (in the language spoken)</summary>
    public static string RESET => Loc.Get("controls.reset");
    const string BANGERS = "Fonts & Materials/Bangers SDF"; // in TextMesh Pro's Examples & Extras Resources
    const float TITLE_SIZE = 56f;
    const float ROW_SIZE = 38f;
    const float ROW_HEIGHT = 46f;
    const float WIDTH = 760f;
    static readonly Color SELECTED = new Color(1f, 0.82f, 0.25f);

    // The game's hooks; tests swap them
    readonly RebindListener listener = new RebindListener(); // this panel's own: another player can wait at once
    internal Action<Func<InputDevice, bool>, Action<string>> Listen;
    internal Action StopListening;
    internal Action<int, InputActionMap> Saved = SaveSeat;
    internal Func<int> Frame = CurrentFrame;

    InputActionMap map;
    BindingSet set;
    int seat;
    Func<InputDevice, bool> mine;
    GlyphStyle style;
    List<RebindSlot> slots = new List<RebindSlot>();
    readonly List<TMP_Text> names = new List<TMP_Text>();
    readonly List<TMP_Text> values = new List<TMP_Text>();
    TMP_Text title;
    RectTransform list;
    TMP_FontAsset font;

    int quietFrame = -1;   // the menu buttons do nothing this frame (a press just landed)
    int listenFrom = -1;   // waiting: listening starts from this frame

    public bool IsOpen { get; private set; }
    public bool Waiting { get; private set; }
    public int Row { get; private set; }
    public int RowCount => slots.Count + 1;
    public string Title => title.text;

    public string RowName(int row) => names[row].text;
    public string RowValue(int row) => values[row].text;

    static int CurrentFrame() { return Time.frameCount; }

    static void SaveSeat(int seat, InputActionMap map)
    {
        BindingStore.Current.Keep(seat, map);
        BindingStore.Current.Save();
    }

    /// <summary>Builds the (hidden) panel under a player's pause menu, in a font (null: Bangers)</summary>
    public static ControlsPanel Build(RectTransform parent, TMP_FontAsset font)
    {
        GameObject root = new GameObject("Controls Panel", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        Image back = root.GetComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.7f);
        back.raycastTarget = false;
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 24, 24);
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fit = root.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ControlsPanel panel = root.AddComponent<ControlsPanel>();
        panel.Listen = panel.listener.Listen;
        panel.StopListening = panel.listener.Stop;
        panel.font = font != null ? font : Resources.Load<TMP_FontAsset>(BANGERS);
        panel.title = Text(root.transform, "Title", panel.font, TITLE_SIZE, TextAlignmentOptions.Center);
        GameObject rows = new GameObject("Rows", typeof(RectTransform));
        rows.transform.SetParent(root.transform, false);
        panel.list = (RectTransform)rows.transform;
        VerticalLayoutGroup rowsLayout = rows.AddComponent<VerticalLayoutGroup>();
        rowsLayout.spacing = 2;
        rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        Loc.Changed += panel.OnLanguage;
        root.SetActive(false);
        return panel;
    }

    /// <summary>
    /// Opens on a player's map for a set (their seat, 1-4, for saving), listening only to their devices (mine), with
    /// button names in their pad's style
    /// </summary>
    public void Open(InputActionMap playerMap, BindingSet bindingSet, int seatNumber, Func<InputDevice, bool> isMine, GlyphStyle buttons = GlyphStyle.Xbox)
    {
        map = playerMap;
        set = bindingSet;
        seat = seatNumber;
        mine = isMine;
        style = bindingSet == BindingSet.Keyboard ? GlyphStyle.Keyboard : buttons;
        slots = RebindRules.SlotsFor(map, set);
        LocalizedText.On(title, set == BindingSet.Keyboard ? "controls.title.keyboard" : "controls.title.controller");
        BuildRows();

        Row = 0;
        Waiting = false;
        quietFrame = Frame(); // the A that opened it
        IsOpen = true;
        gameObject.SetActive(true);
        Show();
    }

    /// <summary>Up / down: the next row, round from the last to the first. Nothing while waiting</summary>
    public void Move(bool down)
    {
        if (!Usable)
            return;
        Row = (Row + (down ? 1 : RowCount - 1)) % RowCount;
        Show();
    }

    /// <summary>A: wait for a press for the row's control, or reset the set</summary>
    public void Confirm()
    {
        if (!Usable)
            return;

        if (Row == slots.Count)
        {
            RebindRules.Reset(map, set);
            Show();
            return;
        }

        Waiting = true;
        listenFrom = Frame() + 1;
        Show();
    }

    /// <summary>B: close the panel (saving). While waiting the press is what cancels, so B here does nothing</summary>
    public void Back()
    {
        if (!Usable)
            return;
        Close();
    }

    /// <summary>Closes the panel, whatever it's doing, and saves the seat's controls</summary>
    public void Close()
    {
        if (!IsOpen)
            return;
        StopWaiting();
        IsOpen = false;
        gameObject.SetActive(false);
        Saved?.Invoke(seat, map);
    }

    bool Usable => IsOpen && !Waiting && Frame() != quietFrame;

    void Update()
    {
        Tick();
    }

    /// <summary>A frame went by: a wait starts listening from the frame after the A that began it</summary>
    internal void Tick()
    {
        if (IsOpen && Waiting && listenFrom >= 0 && Frame() >= listenFrom)
        {
            listenFrom = -1;
            Listen?.Invoke(mine, Heard);
        }
    }

    // The press the wait was for
    void Heard(string path)
    {
        if (!IsOpen || !Waiting)
            return;

        if (path == CancelPath)
        {
            StopWaiting();
        }
        else if (RebindRules.CanBind(map, set, path))
        {
            RebindRules.Bind(map, set, slots[Row].Name, path);
            StopWaiting();
        }
        else
        {
            Listen?.Invoke(mine, Heard); // refused: still waiting
            return;
        }
        quietFrame = Frame();
        Show();
    }

    string CancelPath => set == BindingSet.Keyboard ? "<Keyboard>/backspace" : "<Gamepad>/buttonEast";

    void StopWaiting()
    {
        if (Waiting)
            StopListening?.Invoke();
        Waiting = false;
        listenFrom = -1;
    }

    void BuildRows()
    {
        foreach (TMP_Text name in names)
        {
            if (Application.isPlaying)
                Destroy(name.transform.parent.gameObject);
            else
                DestroyImmediate(name.transform.parent.gameObject);
        }
        names.Clear();
        values.Clear();

        for (int i = 0; i <= slots.Count; i++)
        {
            GameObject row = new GameObject("Row", typeof(RectTransform));
            row.transform.SetParent(list, false);
            LayoutElement height = row.AddComponent<LayoutElement>();
            height.preferredHeight = ROW_HEIGHT;
            height.preferredWidth = WIDTH;
            TMP_Text name = Text(row.transform, "Name", font, ROW_SIZE, TextAlignmentOptions.Left);
            TMP_Text value = Text(row.transform, "Value", font, ROW_SIZE, TextAlignmentOptions.Right);
            Stretch(name.rectTransform);
            Stretch(value.rectTransform);

            names.Add(name);
            values.Add(value);
        }
    }

    void Show()
    {
        for (int i = 0; i < names.Count; i++)
        {
            bool selected = i == Row;
            names[i].color = values[i].color = selected ? SELECTED : Color.white;
            names[i].text = i < slots.Count ? RebindRules.Shown(slots[i]) : RESET;
            if (i == slots.Count)
                values[i].text = "";
            else if (selected && Waiting)
                values[i].text = Loc.Get(set == BindingSet.Keyboard ? "controls.waiting.key" : "controls.waiting.button");
            else
                values[i].text = RebindRules.Display(RebindRules.PathOf(map, slots[i]), style);
        }
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    // The language changed with the panel up
    void OnLanguage()
    {
        if (IsOpen)
            Show();
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguage;
        StopWaiting();
    }
}

/// <summary>
/// Hears the next fresh button or key press on a player's own devices, as the generic path the Player map binds
/// ("&lt;Keyboard&gt;/k", "&lt;Gamepad&gt;/buttonNorth"), for one Controls panel (each panel has its own, so two players
/// can wait at once). A button already down when an event arrives isn't a press: the A that chose the row, still held,
/// doesn't bind itself (onAnyButtonPress reports every button above its press point, held or not)
/// </summary>
public class RebindListener
{
    IDisposable listening;
    Func<InputDevice, bool> mine;
    Action<string> heard;

    public void Listen(Func<InputDevice, bool> isMine, Action<string> onHeard)
    {
        Stop();
        mine = isMine;
        heard = onHeard;
        listening = InputSystem.onAnyButtonPress.Call(OnPress);
    }

    public void Stop()
    {
        listening?.Dispose();
        listening = null;
        heard = null;
    }

    void OnPress(InputControl control)
    {
        if (heard == null || control == null || (mine != null && !mine(control.device)))
            return;
        // The event hasn't reached the device's state yet: a button pressed there already was held, not pressed now
        if (control is ButtonControl button && button.isPressed)
            return;
        string path = GenericPath(control);
        if (path == null)
            return;
        Action<string> callback = heard;
        Stop();
        callback(path);
    }

    /// <summary>A control as the Player map binds it: by kind of device, not this one</summary>
    public static string GenericPath(InputControl control)
    {
        string device = control.device is Keyboard ? "<Keyboard>" : control.device is Gamepad ? "<Gamepad>" : null;
        if (device == null || control.path.Length <= control.device.path.Length)
            return null;
        return device + control.path.Substring(control.device.path.Length);
    }
}
