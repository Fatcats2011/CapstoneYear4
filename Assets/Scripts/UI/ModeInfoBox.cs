using TMPro;
using UnityEngine;

/// <summary>
/// What to do in this mode, in the game's own box over the top of the screen, as the players get their controls in any
/// mode but Standard (which has its tutorial). It goes after SECONDS, on every machine. Made at launch, no scene holds it
/// </summary>
public class ModeInfoBox : MonoBehaviour
{
    /// <summary>How long it's up, its fades included</summary>
    internal const float SECONDS = 5f;
    const float FADE = 0.3f;
    const float WIDTH = 620f;
    const float BELOW_TOP = 130f; // under the compass
    internal const int SORTING_ORDER = 60; // over the scoreboard, under the menus

    static ModeInfoBox instance;

    Canvas canvas;
    CanvasGroup group;
    RectTransform box;
    TMP_Text title, words;
    bool shownThisMatch;
    float shownAt = -1f;
    float lastOutOfMatch; // the 3, 2, 1 that counts began after this: an older one's end says nothing

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (instance != null)
            return;
        GameObject holder = new GameObject(nameof(ModeInfoBox), typeof(RectTransform));
        DontDestroyOnLoad(holder);
        instance = holder.AddComponent<ModeInfoBox>();
    }

    void Awake()
    {
        canvas = GameCard.Canvas(gameObject, SORTING_ORDER);
        canvas.enabled = false;
        group = gameObject.AddComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        box = GameCard.Box(transform, "Info Box", out RectTransform content);
        box.anchorMin = box.anchorMax = new Vector2(0.5f, 1f);
        box.pivot = new Vector2(0.5f, 1f);
        box.anchoredPosition = new Vector2(0f, -BELOW_TOP);
        box.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        title = GameCard.Words(content, "Mode", 42f, GameCard.GOLD);
        words = GameCard.Words(content, "Instructions", 27f, Color.white, WIDTH);
    }

    /// <summary>Whether a mode has instructions to show (Standard teaches with its tutorial)</summary>
    internal static bool HasInstructions(GameMode mode) => mode != GameMode.Standard;

    /// <summary>How solid the box is at a time since it came up: fades in, holds, fades out, gone by SECONDS</summary>
    internal static float AlphaAt(float time)
    {
        if (time < 0f || time >= SECONDS)
            return 0f;
        return Mathf.Clamp01(Mathf.Min(time, SECONDS - time) / FADE);
    }

    // Whether the players are driving: the round's states, once their 3, 2, 1 has ended
    static bool Driving(GameState state)
    {
        return BotDriver.InRound(state, Time.time, PlayerInstantiate.DrivingFrom);
    }

    void Update()
    {
        GameManager game = GameManager.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;
        if (Scoreboard.EndsMatch(state))
        {
            shownThisMatch = false;
            shownAt = -1f;
            lastOutOfMatch = Time.time;
            canvas.enabled = false;
            return;
        }

        if (!shownThisMatch && PlayerInstantiate.DrivingFrom > lastOutOfMatch && Driving(state))
        {
            shownThisMatch = true;
            GameMode mode = HouseRules.Playing.Mode;
            if (HasInstructions(mode))
            {
                title.text = Loc.Get(GameModes.Key(mode));
                words.text = Loc.Get(GameModes.InstructionKey(mode));
                shownAt = Time.unscaledTime;
            }
        }

        // Paused, the box waits: its seconds are for reading
        if (shownAt >= 0f && state == GameState.Paused)
            shownAt += Time.unscaledDeltaTime;

        float alpha = shownAt < 0f ? 0f : AlphaAt(Time.unscaledTime - shownAt);
        bool up = alpha > 0f && state != GameState.Paused;
        if (canvas.enabled != up)
            canvas.enabled = up;
        group.alpha = alpha;
    }
}
