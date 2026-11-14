using TMPro;
using UnityEngine;

/// <summary>
/// A player's goal in Kings Crown, Infection and Hot Potato (KEEP THE CROWN, CATCH THEM, PASS IT!...): one small line of
/// text, no box, just under the compass in their own view. It appears when the mode's info box has gone, or when their goal
/// changes, stays GOAL_SECONDS (ModeFeedbackRules) with a quick fade at each end, and is hidden while paused and at
/// match end. The same goal seen again does not bring it back. Each machine works the goal out from the mode's state
/// (ModeWatcher.ViewOf), so online every machine agrees. Made at runtime on the player's HUD canvas: no prefab holds it
/// </summary>
public class GoalTag : MonoBehaviour
{
    internal const float SIZE = 26f;
    internal const float WIDTH = 320f;
    internal const float HEIGHT = 36f;
    /// <summary>The tag's middle, from the middle of the HUD (1920 x 1080): just under the compass</summary>
    internal const float Y = 310f;
    const float OUTLINE = 0.25f;

    OrderHandler player;
    TextMeshProUGUI text;
    ModeRole shown = ModeRole.None;
    float shownAt;

    /// <summary>The time now, in seconds. Tests set it so they can move time by hand</summary>
    internal System.Func<float> Clock = RealTime;

    static float RealTime()
    {
        return Time.unscaledTime;
    }

    /// <summary>The words up now: "" when none are</summary>
    internal string ShownWords { get { return text != null && shown != ModeRole.None ? text.text : ""; } }

    /// <summary>The goal the tag is showing, or None</summary>
    internal ModeRole Shown { get { return shown; } }

    /// <summary>Gives a player their goal tag, on their HUD's canvas. A player without a HUD here (another machine's) gets none</summary>
    internal static GoalTag AddTo(OrderHandler player, NumberHandler hud)
    {
        Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>(true) : null;
        if (player == null || canvas == null)
            return null;

        GoalTag tag = player.gameObject.AddComponent<GoalTag>();
        tag.player = player;
        tag.Build(canvas.rootCanvas.transform);
        return tag;
    }

    /// <summary>Makes the tag's one line of text: white with a black outline, centred, one line, empty and clear</summary>
    void Build(Transform hud)
    {
        text = (TextMeshProUGUI)GameCard.Words(hud, "Goal Tag", SIZE, Color.white);
        text.richText = false;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.outlineWidth = OUTLINE;
        text.outlineColor = Color.black;
        text.raycastTarget = false;
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(WIDTH, HEIGHT);
        rect.anchoredPosition = new Vector2(0f, Y);
        text.text = "";
        text.alpha = 0f;
    }

    void OnDestroy()
    {
        if (text != null)
            Destroy(text.gameObject);
    }

    /// <summary>
    /// The player's goal now. The same goal as already shown does nothing (it does not restart the seconds). A different one
    /// shows its words and starts the seconds from the later of now and notBefore. No goal clears the tag
    /// </summary>
    internal void See(ModeRole goal, float notBefore)
    {
        if (goal == shown)
            return;

        shown = goal;
        string key = ModeFeedbackRules.GoalKey(goal);
        if (text != null)
            text.text = key != null ? Loc.Get(key) : "";
        shownAt = Mathf.Max(Clock(), notBefore);
    }

    /// <summary>How solid the tag is: gone while paused or with no goal, else by how long ago it started</summary>
    internal float Alpha(bool paused)
    {
        if (paused || shown == ModeRole.None)
            return 0f;
        return ModeFeedbackRules.GoalAlpha(Clock() - shownAt);
    }

    void Update()
    {
        if (text == null || player == null)
            return;

        int seat = OrderSync.SeatOf(player);
        GameManager game = GameManager.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;

        // The info box goes SECONDS after the players start driving; DrivingFrom is in game time, the tag's clock is not
        bool driving = BotDriver.InRound(state, Time.time, PlayerInstantiate.DrivingFrom);
        if (Scoreboard.EndsMatch(state) || !driving || seat < 0)
            See(ModeRole.None, 0f);
        else
        {
            float notBefore = Clock() + (PlayerInstantiate.DrivingFrom + ModeInfoBox.SECONDS - Time.time);
            See(ModeFeedbackRules.GoalFor(ModeWatcher.ViewOf(ModeMatch.Current), seat), notBefore);
        }

        text.alpha = Alpha(state == GameState.Paused);
    }
}
