using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The lobby's word on what's about to be played, for everyone in it: a card hanging at the top of player select and
/// Dispatch, "TONIGHT'S SHIFT" over the mode's name, with a gold stamp slapped on for each modifier changed from normal.
/// Online, a client reads the host's rules (OnlineMatch.Rules), which the host shares as it changes them. Made at launch,
/// no scene holds it
/// </summary>
public class ModeLobbyCard : MonoBehaviour
{
    internal const int SORTING_ORDER = 850; // over the lobbies, under MATCH RULES
    const float BELOW_TOP = 14f;
    const float STAMP_SIZE = 26f;
    static readonly float[] STAMP_TILTS = { -3f, 2f, -1.5f, 3f, -2.5f, 1.5f, -2f, 2.5f };

    static ModeLobbyCard instance;

    Canvas canvas;
    TMP_Text heading, modeName;
    RectTransform stamps;
    readonly List<GameObject> shownStamps = new List<GameObject>();
    readonly List<string> shownModifiers = new List<string>();
    int shownRules = int.MinValue;
    string shownLanguage;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject(nameof(ModeLobbyCard), typeof(RectTransform));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<ModeLobbyCard>();
    }

    void Awake()
    {
        canvas = GameCard.Canvas(gameObject, SORTING_ORDER);
        canvas.enabled = false;
        RectTransform box = GameCard.Box(transform, "Lobby Card", out RectTransform content);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -BELOW_TOP);
        box.localRotation = Quaternion.Euler(0f, 0f, 1.2f);
        heading = GameCard.Words(content, "Heading", 24f, Color.white);
        modeName = GameCard.Words(content, "Mode", 58f, GameCard.GOLD);

        stamps = new GameObject("Stamps", typeof(RectTransform)).GetComponent<RectTransform>();
        stamps.SetParent(content, false);
        HorizontalLayoutGroup row = stamps.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 10f;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = true;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
    }

    // The rules everyone in this lobby will play: the host's
    static HouseRules Rules()
    {
        OnlineGame online = OnlineGame.Playing;
        if (GameAuthority.IsOnline && GameAuthority.Role != NetworkRole.Host && online != null && online.SharedRules.HasValue)
            return HouseRules.Unpack(online.SharedRules.Value);
        return HouseRules.Current.ForMatch(OrderManager.PublicLobby());
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        bool up = game != null && game.MainState == GameState.PlayerSelect && RulesPanel.Showing == null;
        if (canvas.enabled != up)
            canvas.enabled = up;
        if (!up)
            return;

        HouseRules rules = Rules();
        int packed = rules.Pack();
        if (packed == shownRules && shownLanguage == Loc.Language)
            return;
        shownRules = packed;
        shownLanguage = Loc.Language;

        heading.text = Loc.Get("lobby.tonight");
        modeName.text = Loc.Get(GameModes.Key(rules.Mode));
        ShowStamps(rules.Modifiers);
    }

    void ShowStamps(List<string> modifiers)
    {
        foreach (GameObject stamp in shownStamps)
            Destroy(stamp);
        shownStamps.Clear();
        shownModifiers.Clear();
        shownModifiers.AddRange(modifiers);
        stamps.gameObject.SetActive(modifiers.Count > 0);
        for (int i = 0; i < modifiers.Count; i++)
            shownStamps.Add(Stamp(modifiers[i], STAMP_TILTS[i % STAMP_TILTS.Length]));
    }

    /// <summary>The modifiers stamped on the card now (tests)</summary>
    internal IReadOnlyList<string> Stamped => shownModifiers;

    GameObject Stamp(string words, float tilt)
    {
        Image back = new GameObject("Stamp " + words, typeof(RectTransform)).AddComponent<Image>();
        back.rectTransform.SetParent(stamps, false);
        back.sprite = GameCard.Rounded();
        back.type = Image.Type.Sliced;
        back.pixelsPerUnitMultiplier = 3f;
        back.color = GameCard.GOLD;
        back.raycastTarget = false;
        back.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        HorizontalLayoutGroup pad = back.gameObject.AddComponent<HorizontalLayoutGroup>();
        pad.padding = new RectOffset(12, 12, 3, 3);
        pad.childControlWidth = pad.childControlHeight = true;
        pad.childForceExpandWidth = pad.childForceExpandHeight = false;
        GameCard.Words(back.rectTransform, "Words", STAMP_SIZE, GameCard.FRAME).text = words;
        return back.gameObject;
    }
}
