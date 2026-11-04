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

    /// <summary>The rows in the order they show, when the mode shows them (the Mode row comes first)</summary>
    static readonly Rule[] ORDER = { Rule.WaveLength, Rule.Waves, Rule.RoundTimer, Rule.Steals, Rule.GoldenOrder, Rule.Bots, Rule.Speed, Rule.LowGravity };

    // The rules as picked so far: a row the mode hides keeps its value for when another mode shows it
    GameMode mode;
    MatchLength length;
    int waves, speed, round;
    bool steals, golden, bots, lowGravity;
    readonly List<Rule?> rowRules = new List<Rule?>(); // null: the Mode row

    /// <summary>Opens on the rules picked. In a public lobby they're kept for later: the match plays normal</summary>
    public void Open(HouseRules rules, bool inPublicLobby = false)
    {
        publicLobby = inPublicLobby;
        mode = rules.Mode;
        length = rules.Length;
        waves = rules.Waves;
        speed = rules.SpeedPercent;
        round = rules.RoundMinutes;
        steals = rules.Steals;
        golden = rules.GoldenOrder;
        bots = rules.Bots;
        lowGravity = rules.LowGravity;
        Rebuild(0);

        IsOpen = true;
        gameObject.SetActive(true);
        Show();
    }

    /// <summary>The row a rule shows on, or -1 when the mode hides it</summary>
    public int RowOf(Rule rule) => rowRules.IndexOf(rule);

    /// <summary>The Mode row, or -1 while only one mode can be played</summary>
    public int ModeRow => rowRules.IndexOf(null);

    // Makes the rows the mode shows, then selects the row given (or the last one)
    void Rebuild(int row)
    {
        List<OptionSetting> settings = new List<OptionSetting>();
        rowRules.Clear();
        if (GameModes.Playable.Length > 1)
        {
            List<int> modes = new List<int>();
            foreach (GameMode playable in GameModes.Playable)
                modes.Add((int)playable);
            OptionSetting modeRow = OptionSetting.Keyed("rules.mode", modes, choice => GameModes.Key((GameMode)choice), (int)mode);
            modeRow.Changed += value => { mode = (GameMode)value; Changed(); Rebuild(Row); Show(); };
            settings.Add(modeRow);
            rowRules.Add(null);
        }
        foreach (Rule rule in ORDER)
        {
            RuleShow show = ModeRules.For(mode, rule);
            if (show == RuleShow.Hidden)
                continue;
            OptionSetting setting = show == RuleShow.LockedOn ? Locked(rule) : MakeRow(rule);
            setting.Changed += value => { Set(rule, value); Changed(); };
            settings.Add(setting);
            rowRules.Add(rule);
        }
        book = new OptionsBook(new OptionsPage("RULES", settings.ToArray()));
        for (int i = 0; i < Mathf.Min(row, settings.Count - 1); i++)
            book.Move(true);
        BuildRows();
    }

    int Row => book != null ? book.Row : 0;

    static readonly string[] LENGTHS = { "rules.length.short", "rules.length.normal", "rules.length.long" };
    static readonly int[] LENGTH_SECONDS = { 45, 60, 90 };

    OptionSetting MakeRow(Rule rule)
    {
        switch (rule)
        {
            case Rule.WaveLength:
                return Labelled("rules.wavelength", new[] { 0, 1, 2 }, choice => Loc.Get("rules.value.seconds", LENGTH_SECONDS[choice]), (int)length);
            case Rule.Waves:
                return Labelled("rules.waves", HouseRules.WaveChoices, choice => choice.ToString(), waves);
            case Rule.RoundTimer:
                return Labelled("rules.roundtimer", HouseRules.RoundChoices, choice => Loc.Get("rules.summary.round", choice), round);
            case Rule.Speed:
                return Labelled("rules.speed", HouseRules.SpeedChoices, choice => Loc.Get("rules.value.percent", choice), speed);
            case Rule.Steals:
                return OptionSetting.KeyedToggle("rules.steals", "word.off", "word.on", steals ? 1 : 0);
            case Rule.GoldenOrder:
                return OptionSetting.KeyedToggle("rules.golden", "word.off", "word.on", golden ? 1 : 0);
            case Rule.Bots:
                return OptionSetting.KeyedToggle("rules.bots", "word.off", "word.on", bots ? 1 : 0);
            default:
                return OptionSetting.KeyedToggle("rules.lowgravity", "word.off", "word.on", lowGravity ? 1 : 0);
        }
    }

    static OptionSetting Labelled(string labelKey, IEnumerable<int> choices, Func<int, string> labelOf, int value)
    {
        return new OptionSetting(labelKey, choices, labelOf, value) { LabelKey = labelKey };
    }

    // A rule the mode needs on: shows ON (LOCKED) and doesn't step
    static OptionSetting Locked(Rule rule)
    {
        string key = rule == Rule.Steals ? "rules.steals" : rule == Rule.GoldenOrder ? "rules.golden" : "rules.bots";
        return new OptionSetting(key, new[] { 1 }, choice => Loc.Get("rules.locked", Loc.Get("word.on")), 1) { LabelKey = key, Dimmed = () => true };
    }

    void Set(Rule rule, int value)
    {
        switch (rule)
        {
            case Rule.WaveLength: length = (MatchLength)value; break;
            case Rule.Waves: waves = value; break;
            case Rule.RoundTimer: round = value; break;
            case Rule.Speed: speed = value; break;
            case Rule.Steals: steals = value == 1; break;
            case Rule.GoldenOrder: golden = value == 1; break;
            case Rule.Bots: bots = value == 1; break;
            case Rule.LowGravity: lowGravity = value == 1; break;
        }
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

    HouseRules Picked => new HouseRules(length, steals, golden, bots, mode, waves, speed, lowGravity, round);

    void Changed()
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
            bool arrows = i == book.Row && !rules[i].IsDimmed;
            values[i].text = arrows ? "◀  " + rules[i].ValueLabel + "  ▶" : rules[i].ValueLabel;
        }
        note.text = NoteFor(Picked, publicLobby);
    }

    static string NoteFor(HouseRules picked, bool publicLobby)
    {
        if (publicLobby)
            return Loc.Get("rules.note.public");
        List<string> lines = new List<string>();
        if (ModeRules.For(picked.Mode, Rule.GoldenOrder) == RuleShow.Shown && !picked.GoldenOrder)
            lines.Add(Loc.Get("rules.note.nogolden"));
        if (picked.IsExperimental)
            lines.Add(Loc.Get("rules.note.experimental"));
        return string.Join("\n", lines);
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
