using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Skipping a cutscene (CutsceneManager opens it with each cutscene). A second in (SkipHold.SHOW_AFTER) the prompt
/// "HOLD A TO SKIP n/m" shows; each player on this machine holds A for half a second to vote (a fresh press: an A
/// held from the loading screen doesn't count). The rules are SkipVote's.
/// - Locally, and on the host online, it decides: once everyone still in has voted, the cutscene ends (Skip, once).
///   The host shares the count with every machine.
/// - An online client sends its players' votes to the host and shows the host's count; the host's next state ends the
///   cutscene there.
/// </summary>
public class CutsceneSkip : MonoBehaviour
{
    const string CONFIRM_ACTION = "UI/ReadyUp"; // A / cross / Space in the UI map
    const float PROMPT_HEIGHT = 56f;
    const float LABEL_SIZE = 44f;
    const string BANGERS = "Fonts & Materials/Bangers SDF"; // in TextMesh Pro's Examples & Extras Resources

    // The game's hooks, swapped by tests
    internal Func<int, bool> Pressed = LocalPressed;
    internal Action Skip = SkipCutscene;
    internal Func<int, bool> SeatStillThere = InRoster;
    internal Func<IEnumerable<int>> LocalSeats = RosterLocalSeats;

    /// <summary>Where the prompt is built (the cutscene canvas); none: no prompt (tests)</summary>
    internal Transform PromptParent;

    SkipVote vote;
    bool decides;
    bool open;
    float elapsed;
    readonly Dictionary<int, SkipHold> holds = new Dictionary<int, SkipHold>();
    SkipBoard board;
    bool shared;
    GameObject prompt;
    TMP_Text count;

    static bool LocalPressed(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        PlayerSlot slot = players != null && seat >= 0 && seat < Constants.MAX_PLAYERS ? players.Roster[seat] : null;
        InputAction confirm = slot != null && slot.IsLocal ? slot.Input.actions.FindAction(CONFIRM_ACTION) : null;
        return confirm != null && confirm.IsPressed();
    }

    static void SkipCutscene()
    {
        if (CutsceneManager.Instance != null)
            CutsceneManager.Instance.Skip();
    }

    static bool InRoster(int seat)
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        return players == null || (seat >= 0 && seat < Constants.MAX_PLAYERS && players.Roster[seat] != null);
    }

    static IEnumerable<int> RosterLocalSeats()
    {
        PlayerInstantiate players = PlayerInstantiate.Instance;
        return players == null ? Enumerable.Empty<int>() : players.Roster.LocalPlayers.Select(p => p.Index).ToArray();
    }

    /// <summary>A cutscene starts: a fresh vote for these seats. decides is whether this machine decides (GameAuthority)</summary>
    public void Open(IEnumerable<int> seats, bool decides)
    {
        Close();
        this.decides = decides;
        vote = new SkipVote(seats);
        board = SkipBoard.From(vote);
        shared = false;
        elapsed = 0;
        holds.Clear();
        foreach (int seat in LocalSeats())
            holds[seat] = new SkipHold();
        open = true;

        if (decides)
            SkipSync.VoteArrived += OnVote;
        else
            SkipSync.BoardArrived += OnBoard;
        ShowPrompt();
    }

    /// <summary>The cutscene is over (skipped, or it ended by itself): the prompt goes and presses do nothing</summary>
    public void Close()
    {
        open = false;
        SkipSync.VoteArrived -= OnVote;
        SkipSync.BoardArrived -= OnBoard;
        if (prompt != null)
            prompt.SetActive(false);
    }

    void Update()
    {
        if (open)
            Tick(Time.unscaledDeltaTime);
    }

    internal void Tick(float deltaTime)
    {
        if (!open)
            return;

        elapsed += deltaTime;
        bool shown = elapsed >= SkipHold.SHOW_AFTER;
        foreach (KeyValuePair<int, SkipHold> hold in holds)
        {
            if (!hold.Value.Tick(Pressed(hold.Key), deltaTime, shown))
                continue;
            if (decides)
                vote.Vote(hold.Key);
            else
                SkipSync.SendVote(hold.Key);
        }

        if (decides)
        {
            for (int seat = 0; seat < Constants.MAX_PLAYERS; seat++)
            {
                if (vote.IsIn(seat) && !SeatStillThere(seat))
                    vote.Leave(seat);
            }

            SkipBoard next = SkipBoard.From(vote);
            if (!shared || !next.Equals(board))
            {
                board = next;
                shared = true;
                SkipSync.ShareBoard(board);
            }

            if (vote.Everyone)
            {
                Close();
                Skip?.Invoke();
                return;
            }
        }
        ShowPrompt();
    }

    // Host: another machine's player voted (OnlineCutsceneSkip checked it's their own seat)
    void OnVote(int seat)
    {
        if (open)
            vote.Vote(seat);
    }

    // Client: the host's count
    void OnBoard(SkipBoard arrived)
    {
        board = arrived;
        ShowPrompt();
    }

    void ShowPrompt()
    {
        if (PromptParent == null)
            return;
        if (prompt == null)
            BuildPrompt();

        bool shown = open && elapsed >= SkipHold.SHOW_AFTER;
        prompt.SetActive(shown);
        if (shown)
            count.text = Loc.Get("cutscene.toskip", board.Voted, board.Total);
    }

    // Bottom right of the cutscene: HOLD [A] TO SKIP n/m, in the game's font
    void BuildPrompt()
    {
        prompt = new GameObject("Skip Prompt", typeof(RectTransform));
        prompt.transform.SetParent(PromptParent, false);
        RectTransform rect = (RectTransform)prompt.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-40f, 40f);
        HorizontalLayoutGroup layout = prompt.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12;
        layout.childAlignment = TextAnchor.MiddleRight;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        ContentSizeFitter fit = prompt.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        TMP_FontAsset font = FindFont();
        LocalizedText.On(Label("HOLD", font), "cutscene.hold");
        ButtonArtSet art = ButtonArtSet.Results();
        if (art != null && art.confirmXbox != null)
        {
            GameObject button = new GameObject("Button", typeof(RectTransform), typeof(Image));
            button.transform.SetParent(prompt.transform, false);
            Image image = button.GetComponent<Image>();
            image.sprite = art.confirmXbox;
            image.preserveAspect = true;
            LayoutElement size = button.AddComponent<LayoutElement>();
            size.preferredHeight = size.preferredWidth = PROMPT_HEIGHT;
            ButtonGlyph glyph = button.AddComponent<ButtonGlyph>();
            glyph.xbox = art.confirmXbox;
            glyph.playStation = art.confirmPlayStation;
            glyph.keyboard = art.confirmKeyboard;
            glyph.Show(LastDevice.Current);
        }
        else
            Label("A", font);
        count = Label("TO SKIP", font);
    }

    // The game's font (Bangers), else the cutscene's own text's
    TMP_FontAsset FindFont()
    {
        TMP_FontAsset bangers = Resources.Load<TMP_FontAsset>(BANGERS);
        if (bangers != null)
            return bangers;
        TMP_Text existing = PromptParent.GetComponentInChildren<TMP_Text>(true);
        return existing != null ? existing.font : null;
    }

    TMP_Text Label(string text, TMP_FontAsset font)
    {
        GameObject go = new GameObject(text, typeof(RectTransform));
        go.transform.SetParent(prompt.transform, false);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        if (font != null)
            label.font = font;
        label.text = text;
        label.fontSize = LABEL_SIZE;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    void OnDestroy()
    {
        Close();
    }
}
