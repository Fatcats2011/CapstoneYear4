using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// A player's pause menu. The host's lists Resume · Restart · Main Menu in a local match, and Resume · Main Menu online
/// (the online pause doesn't stop the game, and a restart would need everyone). Restart is built by code beside the
/// drawn entries and asks first: A restarts the match as a rematch does (the tutorial skipped), B goes back to the list
/// </summary>
public class PauseMenu : MonoBehaviour, IMouseMenu
{
    public bool goToMainMenu = false;

    [SerializeField] internal GameObject tint;

    public enum PauseType
    {
        Host,
        Sub
    }
    public PauseType pauseType = PauseType.Sub;

    [Header("Sub Pause")]
    [SerializeField] internal GameObject subPauseGO;

    [Header("Host Pause")]
    [SerializeField] internal GameObject hostPauseGO;
    [SerializeField] internal GameObject selector;
    [SerializeField] internal GameObject[] selectorObjects;
    int selectorPos;

    // What the entries do; tests swap them
    internal Action Restart = RestartMatch;
    internal Action Leave; // null: ReturnToMenu

    const string BANGERS = "Fonts & Materials/Bangers SDF"; // in TextMesh Pro's Examples & Extras Resources
    const float ENTRY_SIZE = 90f;
    const float QUESTION_SIZE = 44f;
    const float PROMPT_HEIGHT = 50f;

    GameObject[] drawnEntries; // Resume and Main Menu, as drawn
    Vector2 mainMenuDrawnAt;
    float entryStep; // between the drawn entries
    TMP_Text restartEntry;
    TMP_Text controlsEntry;
    TMP_Text subControlsEntry;
    GameObject restartQuestion;
    bool confirming;
    ControlsPanel controls;

    /// <summary>Whoever this pause menu belongs to (MenuInteractions), whose controls the panel edits</summary>
    internal PlayerInput Owner;

    /// <summary>The code-built Restart entry (null until the first host pause)</summary>
    internal TMP_Text RestartEntry => restartEntry;

    /// <summary>The code-built Controls entry on the host's pause (null until the first host pause)</summary>
    internal TMP_Text ControlsEntry => controlsEntry;

    /// <summary>The other players' pause: its one entry, Controls (null until the first)</summary>
    internal TMP_Text SubControlsEntry => subControlsEntry;

    /// <summary>"RESTART THE MATCH?" and its A / B prompts (null until the first host pause)</summary>
    internal GameObject RestartQuestion => restartQuestion;

    /// <summary>Whether Restart is asking: A restarts, B goes back to the list</summary>
    internal bool Confirming => confirming;

    /// <summary>The Controls panel, while it's up</summary>
    internal ControlsPanel Controls => controls != null && controls.IsOpen ? controls : null;

    // Restart: a fresh match from its start, the tutorial skipped, as a rematch (SceneFlow)
    static void RestartMatch()
    {
        PlayerInstantiate.Instance.PlayerPlay();
        SceneFlow.Current.Rematch();
    }

    // The entries for this pause: Resume, Restart (a local match only), Controls, Main Menu, a step apart
    void ArrangeEntries()
    {
        if (selectorObjects == null || selectorObjects.Length < 2)
            return;
        if (drawnEntries == null)
        {
            drawnEntries = (GameObject[])selectorObjects.Clone();
            RectTransform drawnMenu = (RectTransform)drawnEntries[drawnEntries.Length - 1].transform;
            mainMenuDrawnAt = drawnMenu.anchoredPosition;
            entryStep = ((RectTransform)drawnEntries[0].transform).anchoredPosition.y - mainMenuDrawnAt.y;
            TMP_FontAsset font = Resources.Load<TMP_FontAsset>(BANGERS);
            restartEntry = Entry(drawnMenu, "Restart", "pause.restart", font);
            controlsEntry = Entry(drawnMenu, "Controls", "pause.controls", font);
            BuildQuestion(drawnMenu.parent, drawnMenu, font);
        }

        GameObject resume = drawnEntries[0];
        GameObject mainMenu = drawnEntries[drawnEntries.Length - 1];
        bool local = !GameAuthority.IsOnline;
        restartEntry.gameObject.SetActive(local);
        selectorObjects = local
            ? new[] { resume, restartEntry.gameObject, controlsEntry.gameObject, mainMenu }
            : new[] { resume, controlsEntry.gameObject, mainMenu };
        for (int i = 1; i < selectorObjects.Length; i++)
        {
            RectTransform rect = (RectTransform)selectorObjects[i].transform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, mainMenuDrawnAt.y - (i - 1) * entryStep);
        }
        RectTransform question = (RectTransform)restartQuestion.transform;
        question.anchoredPosition = new Vector2(0f, mainMenuDrawnAt.y - (selectorObjects.Length - 1) * entryStep - entryStep);
    }

    // An entry built by code, its left edge lined up with the drawn Main Menu's
    static TMP_Text Entry(RectTransform drawnMenu, string name, string words, TMP_FontAsset font)
    {
        TMP_Text entry = Text(drawnMenu.parent, name, font, ENTRY_SIZE);
        RectTransform rect = entry.rectTransform;
        rect.anchorMin = drawnMenu.anchorMin;
        rect.anchorMax = drawnMenu.anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(600f, ENTRY_SIZE * 1.2f);
        float left = drawnMenu.anchoredPosition.x - drawnMenu.rect.width * drawnMenu.pivot.x * drawnMenu.localScale.x; // the drawn art is a scaled image
        rect.anchoredPosition = new Vector2(left, drawnMenu.anchoredPosition.y);
        LocalizedText.On(entry, words);
        entry.alignment = TextAlignmentOptions.Left;
        return entry;
    }

    // "RESTART THE MATCH?" and A YES  B NO, under the list
    void BuildQuestion(Transform parent, RectTransform drawnMenu, TMP_FontAsset font)
    {
        restartQuestion = new GameObject("Restart Question", typeof(RectTransform));
        restartQuestion.transform.SetParent(parent, false);
        RectTransform question = (RectTransform)restartQuestion.transform;
        question.anchorMin = question.anchorMax = drawnMenu.anchorMin;
        VerticalLayoutGroup lines = restartQuestion.AddComponent<VerticalLayoutGroup>();
        lines.childAlignment = TextAnchor.MiddleCenter;
        lines.spacing = 8;
        lines.childControlWidth = lines.childControlHeight = true;
        lines.childForceExpandWidth = lines.childForceExpandHeight = false;
        ContentSizeFitter fit = restartQuestion.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        LocalizedText.On(Text(restartQuestion.transform, "Question", font, QUESTION_SIZE), "pause.restart.question");
        GameObject prompts = new GameObject("Prompts", typeof(RectTransform));
        prompts.transform.SetParent(restartQuestion.transform, false);
        HorizontalLayoutGroup row = prompts.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 14;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        ButtonArtSet art = ButtonArtSet.Results();
        Glyph(prompts.transform, art != null ? art.confirmXbox : null, art != null ? art.confirmPlayStation : null, art != null ? art.confirmKeyboard : null, "A");
        LocalizedText.On(Text(prompts.transform, "Yes", font, QUESTION_SIZE), "pause.yes").GetComponent<TMP_Text>().margin = new Vector4(0f, 0f, 40f, 0f);
        Glyph(prompts.transform, art != null ? art.backXbox : null, art != null ? art.backPlayStation : null, art != null ? art.backKeyboard : null, "B");
        LocalizedText.On(Text(prompts.transform, "No", font, QUESTION_SIZE), "pause.no");
        restartQuestion.SetActive(false);
    }

    // The other players' pause: PAUSED, and CONTROLS, its one entry (A opens it)
    void ArrangeSubEntries()
    {
        if (subControlsEntry != null || subPauseGO == null)
            return;
        TMP_FontAsset font = Resources.Load<TMP_FontAsset>(BANGERS);
        TMP_Text paused = Text(subPauseGO.transform, "Paused", font, ENTRY_SIZE);
        LocalizedText.On(paused, "word.paused");
        paused.rectTransform.anchoredPosition = new Vector2(0f, 320f);
        paused.rectTransform.sizeDelta = new Vector2(800f, ENTRY_SIZE * 1.2f);
        subControlsEntry = Text(subPauseGO.transform, "Controls", font, ENTRY_SIZE);
        LocalizedText.On(subControlsEntry, "pause.controls");
        subControlsEntry.color = new Color(1f, 0.82f, 0.25f);
        subControlsEntry.rectTransform.anchoredPosition = new Vector2(0f, 150f);
        subControlsEntry.rectTransform.sizeDelta = new Vector2(800f, ENTRY_SIZE * 1.2f);
    }

    /// <summary>
    /// Opens the Controls panel for a player: the set of the device they're using (keyboard, else controller), only
    /// their own devices heard
    /// </summary>
    public void OpenControls(PlayerInput player)
    {
        if (player == null || PlayerInstantiate.Instance == null)
            return;
        if (controls == null)
            controls = ControlsPanel.Build((RectTransform)transform, Resources.Load<TMP_FontAsset>(BANGERS));

        BindingSet set = player.currentControlScheme == KeyboardControls.SCHEME ? BindingSet.Keyboard : BindingSet.Controller;
        Gamepad pad = null;
        foreach (InputDevice device in player.devices)
            pad = pad ?? device as Gamepad;
        int seat = PlayerInstantiate.Instance.Roster.IndexOf(player) + 1;
        controls.Open(player.actions.FindActionMap(BindingStore.PLAYER_MAP), set, seat, device => Owns(player, device),
            GlyphRules.StyleFor(pad));
        hostPauseGO.SetActive(false);
        subPauseGO.SetActive(false);
        if (connections != null)
            connections.gameObject.SetActive(false);
    }

    /// <summary>B with the panel up: its own Back; once it closes, the pause list shows again</summary>
    public void ControlsBack()
    {
        if (Controls == null)
            return;
        controls.Back();
        if (!controls.IsOpen)
            ShowList();
    }

    /// <summary>Start with the panel up (not waiting): closes it, back to the list</summary>
    public void CloseControls()
    {
        if (Controls == null || controls.Waiting)
            return;
        controls.Close();
        ShowList();
    }

    static bool Owns(PlayerInput player, InputDevice device)
    {
        foreach (InputDevice own in player.devices)
        {
            if (own == device)
                return true;
        }
        return false;
    }

    // Online: every seat's player and their connection, beside the list (OnlinePings measures, PingSync carries)
    const float CONNECTIONS_SIZE = 40f;
    TMP_Text connections;

    /// <summary>The online connection list (null until the first online pause)</summary>
    internal TMP_Text Connections => connections;

    void ShowConnections(bool online)
    {
        PingSync.Arrived -= OnPings;
        if (!online)
        {
            if (connections != null)
                connections.gameObject.SetActive(false);
            return;
        }

        if (connections == null)
        {
            connections = Text(transform, "Connections", Resources.Load<TMP_FontAsset>(BANGERS), CONNECTIONS_SIZE);
            connections.alignment = TextAlignmentOptions.TopLeft;
            connections.richText = true;
            RectTransform rect = connections.rectTransform;
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(560f, 300f);
            rect.anchoredPosition = new Vector2(330f, 230f);
        }
        connections.gameObject.SetActive(true);
        PingSync.Arrived += OnPings;
        OnPings(PingSync.Last);
    }

    // PingSync is static: a pause menu gone with its player never keeps listening
    void OnDestroy()
    {
        PingSync.Arrived -= OnPings;
    }

    void OnPings(PingBoard board)
    {
        if (connections == null)
            return;
        System.Text.StringBuilder lines = new System.Text.StringBuilder();
        for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
        {
            string name = PingSync.Names[seat];
            if (string.IsNullOrEmpty(name) && board.Of(seat) < 0)
                continue;
            lines.Append("P").Append(seat + 1).Append("  ").Append(NotMarkup(PlayerNames.Shown(name, seat)).ToUpperInvariant())
                .Append("   ").Append(PingRules.Line(board.Of(seat))).Append('\n');
        }
        connections.text = lines.ToString();
    }

    // A Steam name is not markup: its angle brackets become round ones, so "<size=400%>" can't fill the screen
    internal static string NotMarkup(string name)
    {
        return (name ?? "").Replace('<', '(').Replace('>', ')');
    }

    void ShowList()
    {
        if (connections != null && GameAuthority.IsOnline)
            connections.gameObject.SetActive(true);
        hostPauseGO.SetActive(pauseType == PauseType.Host);
        subPauseGO.SetActive(pauseType == PauseType.Sub);
    }

    static void Glyph(Transform parent, Sprite xbox, Sprite playStation, Sprite keyboard, string fallback)
    {
        if (xbox == null)
        {
            Text(parent, fallback, null, QUESTION_SIZE).text = fallback;
            return;
        }
        GameObject button = new GameObject("Button", typeof(RectTransform), typeof(Image));
        button.transform.SetParent(parent, false);
        Image image = button.GetComponent<Image>();
        image.sprite = xbox;
        image.preserveAspect = true;
        LayoutElement size = button.AddComponent<LayoutElement>();
        size.preferredHeight = size.preferredWidth = PROMPT_HEIGHT;
        ButtonGlyph glyph = button.AddComponent<ButtonGlyph>();
        glyph.xbox = xbox;
        glyph.playStation = playStation;
        glyph.keyboard = keyboard;
        glyph.Show(LastDevice.Current);
    }

    static TMP_Text Text(Transform parent, string name, TMP_FontAsset font, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }

    void Ask(bool asking)
    {
        confirming = asking;
        if (restartQuestion != null)
            restartQuestion.SetActive(asking);
    }

    /// <summary>B on the host's pause: while Restart asks, back to the list</summary>
    public void Back()
    {
        if (confirming)
            Ask(false);
    }

    private void OnEnable()
    {
        GameManager.Instance.OnSwapPlayerSelect += ResetMenu;
        GameManager.Instance.OnNewMatch += ResetMenu; // a restart or rematch skips player select
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapPlayerSelect -= ResetMenu;
        GameManager.Instance.OnNewMatch -= ResetMenu;
    }

    public void OnPause(PauseType pauseType)
    {
        tint.SetActive(true);
        this.pauseType = pauseType;

        if (pauseType == PauseType.Host)
            ArrangeEntries();
        else
            ArrangeSubEntries();
        ShowConnections(GameAuthority.IsOnline);
        Ask(false);
        selectorPos = 0;
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[0].transform.position.y, selector.transform.position.z);

        switch (pauseType)
        {
            case PauseType.Host:
                hostPauseGO.SetActive(true);
                subPauseGO.SetActive(false);
                return;
            case PauseType.Sub:
                hostPauseGO.SetActive(false);
                subPauseGO.SetActive(true);
                return;
            default: return;
        }

    }

    public void OnPlay()
    {
        hostPauseGO.SetActive(false);
        subPauseGO.SetActive(false);

        tint.SetActive(false);
        Ask(false);
        if (controls != null)
            controls.Close(); // saves: someone resumed with the panel up
        ShowConnections(false);

        if (SoundManager.Instance != null)
            SoundManager.Instance.ChangeSnapshot("gameplay");
    }

    // The mouse (MouseMenus), on the list of whoever paused: hover picks an entry, a click does what A does, a right
    // click what B does (cancels Restart's question, or leaves the Controls panel). Not inside the Controls panel
    IReadOnlyList<RectTransform> IMouseMenu.MouseRows
    {
        get
        {
            List<RectTransform> rows = new List<RectTransform>();
            if (Controls == null && !confirming && selectorObjects != null)
                foreach (GameObject entry in selectorObjects)
                    rows.Add(entry != null ? entry.transform as RectTransform : null);
            return rows;
        }
    }

    int IMouseMenu.MouseSelected => selectorPos;
    bool IMouseMenu.MouseRowsSpanTheScreen => false;
    // The pause draws inside its player's split view: its hit areas go through that view's camera, not the menu UI
    // overlay camera's full-screen rect
    Camera IMouseMenu.MouseCamera => Owner != null ? Owner.camera : null;

    void IMouseMenu.MouseSelect(int row)
    {
        selectorPos = row;
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    void IMouseMenu.MouseClick(int row, Vector2 point, Camera camera)
    {
        ConfirmMenu();
    }

    void IMouseMenu.MouseBack()
    {
        if (Controls != null)
            ControlsBack();
        else
            Back();
    }

    public void ScrollMenu(bool direction)
    {
        // While Restart asks, only A and B do anything
        if (confirming)
            return;

        // Positive Scroll
        if (direction)
        {
            if (selectorPos == selectorObjects.Length - 1)
            {
                selectorPos = 0;
            }
            else
            {
                selectorPos = selectorPos + 1;
            }
        }
        // Negative Scroll
        else
        {
            if (selectorPos == 0)
            {
                selectorPos = selectorObjects.Length - 1;
            }
            else
            {
                selectorPos = selectorPos - 1;
            }
        }

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    public void ConfirmMenu()
    {
        if (goToMainMenu == true)
            return;

        GameObject chosen = selectorObjects[selectorPos];

        // Restart asks first: A again restarts
        if (restartEntry != null && chosen == restartEntry.gameObject && !confirming)
        {
            Ask(true);
            return;
        }

        // Controls opens the panel over the list
        if (controlsEntry != null && chosen == controlsEntry.gameObject)
        {
            OpenControls(Owner);
            return;
        }

        goToMainMenu = true;

        if (selectorPos == 0) // Resume
        {
            PlayerInstantiate.Instance.PlayerPlay();
            ResetMenu();
        }
        else if (restartEntry != null && chosen == restartEntry.gameObject)
        {
            Ask(false);
            Restart();
        }
        else // Main Menu, the last
        {
            (Leave ?? ReturnToMenu)();
        }
    }

    internal void ReturnToMenu()
    {
        SoundManager.Instance.ChangeSnapshot("gameplay");
        PlayerInstantiate.Instance.PlayerPlay();
        SceneFlow.Current.ReturnToMenu();
        selectorPos = 0;
    }

    public void ResetMenu()
    {
        goToMainMenu = false;
    }
}
