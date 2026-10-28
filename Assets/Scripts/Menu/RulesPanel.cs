using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MATCH RULES, over player select (local) or Dispatch (the online host): Start opens it for the player who works the
/// menus. Up / down pick a rule, left / right change it, A or B close it. Each change is the host's rules at once
/// (HouseRules.Current) and saved in settings.cfg (Kept). Built by code on its own canvas, above the menus.
/// See docs/superpowers/specs/2026-10-24-match-rules-design.md
/// </summary>
public class RulesPanel : MonoBehaviour
{
    const string BANGERS = "Fonts & Materials/Bangers SDF"; // in TextMesh Pro's Examples & Extras Resources
    const float TITLE_SIZE = 64f;
    const float ROW_SIZE = 44f;
    const float NOTE_SIZE = 28f;
    const float ROW_WIDTH = 720f;
    const float ROW_HEIGHT = 56f;
    internal const int SORTING_ORDER = 900; // above the menus, below ControllerPrompts' hints
    static readonly Color SELECTED = new Color(1f, 0.82f, 0.25f);
    bool publicLobby;

    static RulesPanel instance;

    /// <summary>Where a change is kept: the settings file (OptionsMenu); tests swap it</summary>
    internal Action<HouseRules> Kept = KeepInSettings;

    OptionsBook book;
    readonly List<TMP_Text> names = new List<TMP_Text>();
    readonly List<TMP_Text> values = new List<TMP_Text>();
    TMP_Text note;
    TMP_FontAsset font;
    RectTransform list;

    public bool IsOpen { get; private set; }
    public string ValueOf(int row) => book.Current.Settings[row].ValueLabel;
    public string Note => note.text;
    public string Title => title.text;
    public string NameOf(int row) => names[row].text;
    TMP_Text title;

    /// <summary>The game's panel while it's up, else null (never makes one)</summary>
    public static RulesPanel Showing => instance != null && instance.IsOpen ? instance : null;

    /// <summary>The game's panel, made the first time it's needed, on a canvas of its own</summary>
    public static RulesPanel Instance
    {
        get
        {
            if (instance == null)
            {
                GameObject canvasObject = new GameObject("Match Rules", typeof(Canvas), typeof(CanvasScaler));
                DontDestroyOnLoad(canvasObject);
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = SORTING_ORDER;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                instance = Build((RectTransform)canvasObject.transform);
            }
            return instance;
        }
    }

    static void KeepInSettings(HouseRules rules)
    {
        if (OptionsMenu.Instance != null)
            OptionsMenu.Instance.SetRules(rules);
    }

    /// <summary>Builds the (hidden) panel in the middle of a canvas</summary>
    public static RulesPanel Build(RectTransform canvas)
    {
        GameObject root = new GameObject("Rules Panel", typeof(RectTransform), typeof(Image));
        RectTransform rect = (RectTransform)root.transform;
        rect.SetParent(canvas, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        Image back = root.GetComponent<Image>();
        back.color = new Color(0f, 0f, 0f, 0.8f);
        back.raycastTarget = false;
        VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 28, 28);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fit = root.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RulesPanel panel = root.AddComponent<RulesPanel>();
        panel.font = Resources.Load<TMP_FontAsset>(BANGERS);
        panel.title = Text(root.transform, "Title", panel.font, TITLE_SIZE, TextAlignmentOptions.Center);
        LocalizedText.On(panel.title, "rules.title");
        GameObject rows = new GameObject("Rules", typeof(RectTransform));
        rows.transform.SetParent(root.transform, false);
        panel.list = (RectTransform)rows.transform;
        VerticalLayoutGroup rowsLayout = rows.AddComponent<VerticalLayoutGroup>();
        rowsLayout.childControlWidth = rowsLayout.childControlHeight = true;
        rowsLayout.childForceExpandWidth = true;
        rowsLayout.childForceExpandHeight = false;
        panel.note = Text(root.transform, "Note", panel.font, NOTE_SIZE, TextAlignmentOptions.Center);
        panel.note.enableWordWrapping = true;
        panel.note.gameObject.AddComponent<LayoutElement>().preferredWidth = ROW_WIDTH;
        LocalizedText.On(Text(root.transform, "Close", panel.font, NOTE_SIZE, TextAlignmentOptions.Center), "rules.done");
        Loc.Changed += panel.OnLanguage;
        root.SetActive(false);
        return panel;
    }

    /// <summary>Opens on the rules picked. In a public lobby they're kept for later: the match plays normal</summary>
    public void Open(HouseRules rules, bool inPublicLobby = false)
    {
        publicLobby = inPublicLobby;
        string[] lengths = { "rules.length.short", "rules.length.normal", "rules.length.long" };
        OptionSetting length = OptionSetting.Keyed("rules.length", new[] { 0, 1, 2 }, choice => lengths[choice], (int)rules.Length);
        OptionSetting steals = OptionSetting.KeyedToggle("rules.steals", "word.off", "word.on", rules.Steals ? 1 : 0);
        OptionSetting golden = OptionSetting.KeyedToggle("rules.golden", "word.off", "word.on", rules.GoldenOrder ? 1 : 0);
        OptionSetting bots = OptionSetting.KeyedToggle("rules.bots", "word.off", "word.on", rules.Bots ? 1 : 0);
        foreach (OptionSetting setting in new[] { length, steals, golden, bots })
            setting.Changed += OnChanged;
        book = new OptionsBook(new OptionsPage("RULES", length, steals, golden, bots));
        BuildRows();

        IsOpen = true;
        gameObject.SetActive(true);
        Show();
    }

    /// <summary>Up / down: the next rule, round from the last to the first</summary>
    public void Move(bool down)
    {
        if (!IsOpen)
            return;
        book.Move(down);
        Show();
    }

    /// <summary>Left / right: the rule's previous or next choice</summary>
    public void Step(bool right)
    {
        if (IsOpen && book.Step(right))
            Show();
    }

    public void Close()
    {
        IsOpen = false;
        gameObject.SetActive(false);
    }

    HouseRules Picked
    {
        get
        {
            IReadOnlyList<OptionSetting> rules = book.Current.Settings;
            return new HouseRules((MatchLength)rules[0].Value, rules[1].Value == 1, rules[2].Value == 1, rules[3].Value == 1);
        }
    }

    void OnChanged(int value)
    {
        HouseRules.Current = Picked;
        Kept?.Invoke(HouseRules.Current);
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

        foreach (OptionSetting setting in book.Current.Settings)
        {
            GameObject row = new GameObject(setting.Label, typeof(RectTransform));
            row.transform.SetParent(list, false);
            LayoutElement size = row.AddComponent<LayoutElement>();
            size.preferredWidth = ROW_WIDTH;
            size.preferredHeight = ROW_HEIGHT;
            TMP_Text name = Text(row.transform, "Name", font, ROW_SIZE, TextAlignmentOptions.Left);
            TMP_Text value = Text(row.transform, "Value", font, ROW_SIZE, TextAlignmentOptions.Right);
            Stretch(name.rectTransform);
            Stretch(value.rectTransform);
            name.text = setting.Label;
            names.Add(name);
            values.Add(value);
        }
    }

    // The language changed with the panel up
    void OnLanguage()
    {
        if (book != null)
            Show();
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguage;
    }

    void Show()
    {
        IReadOnlyList<OptionSetting> rules = book.Current.Settings;
        for (int i = 0; i < rules.Count; i++)
            names[i].text = rules[i].Label;
        for (int i = 0; i < rules.Count; i++)
        {
            names[i].color = values[i].color = i == book.Row ? SELECTED : Color.white;
            values[i].text = i == book.Row ? "◀  " + rules[i].ValueLabel + "  ▶" : rules[i].ValueLabel;
        }
        note.text = publicLobby ? Loc.Get("rules.note.public") : Picked.GoldenOrder ? "" : Loc.Get("rules.note.nogolden");
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
}
