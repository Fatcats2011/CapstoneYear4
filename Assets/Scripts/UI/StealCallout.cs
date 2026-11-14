using TMPro;
using UnityEngine;

/// <summary>
/// A player's callout: one small line in their own view when they steal, are stolen from, bump a rival and take
/// nothing (StealFeedbackRules has the words and the timings), earn money from a delivery, or gain or lose first place
/// (GainFeedbackRules). Two gains close together are one line with the sum, and a lead line waits for a fresh gain line
/// to have had its moment. In Kings Crown, Infection and Hot Potato it also says what happened to them (the crown, an
/// infection, the cursed package, a player out: ModeFeedbackRules has the lines) and, in the timed modes, that the round has
/// 30 seconds left, when the timer's digits pulse (ModeWatcher announces both through ModeFeedback). It sits at the bottom middle of their HUD, just over the
/// horn charge bars, and a new line replaces the old one. With a steal the player's pad shakes and their camera kicks
/// (BallDriving.StealJolt). Each machine shows it for the players it drives, from the same announcement (StealFeedback),
/// so online every machine tells the host's story. Made at runtime on the player's HUD canvas: no prefab holds it
/// </summary>
public class StealCallout : MonoBehaviour
{
    internal const float SIZE = 28f;
    internal const float WIDTH = 300f;
    internal const float HEIGHT = 40f;
    /// <summary>The line's middle, from the middle of the HUD (1920 x 1080): just over the horn charge bars</summary>
    internal const float Y = -400f;
    const float OUTLINE = 0.25f;

    OrderHandler player;
    NumberHandler hud;
    TextMeshProUGUI line;
    float shownAt = -1f;

    /// <summary>The time now, in seconds. Tests set it so they can move time by hand</summary>
    internal System.Func<float> Clock = RealTime;

    // The gain line up, if the last line shown was one: what it says, so a gain heard right after can add to it
    bool gainUp;
    GainReason gainReason;
    int gainAmount;
    string gainStamp = "";

    // A lead line waiting for the gain line to have its moment (one: a newer replaces it)
    bool leadHeld;
    string leadWords;
    CalloutTone leadTone;

    static float RealTime()
    {
        return Time.unscaledTime;
    }

    /// <summary>The words up now: "" when none are</summary>
    internal string ShownWords { get { return line != null && shownAt >= 0f ? line.text : ""; } }

    /// <summary>Gives a player their callout, on their HUD's canvas. A player without a HUD here (another machine's) gets none</summary>
    internal static StealCallout AddTo(OrderHandler player, NumberHandler hud)
    {
        Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>(true) : null;
        if (player == null || canvas == null)
            return null;

        StealCallout callout = player.gameObject.AddComponent<StealCallout>();
        callout.player = player;
        callout.hud = hud;
        callout.Build(canvas.rootCanvas.transform);
        return callout;
    }

    void Build(Transform hud)
    {
        line = (TextMeshProUGUI)GameCard.Words(hud, "Steal Callout", SIZE, Color.white);
        line.richText = false; // a rival's name is drawn as it is, tags and all
        line.overflowMode = TextOverflowModes.Ellipsis;
        line.outlineWidth = OUTLINE;
        line.outlineColor = Color.black;
        RectTransform rect = line.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(WIDTH, HEIGHT);
        rect.anchoredPosition = new Vector2(0f, Y);
        line.text = "";
        line.alpha = 0f;
    }

    void OnEnable()
    {
        StealFeedback.Happened += Hear;
        ScoreFeedback.Gained += HearGain;
        ScoreFeedback.LeadTaken += HearLead;
        ModeFeedback.Said += HearMode;
        ModeFeedback.Warned += HearWarning;
    }

    void OnDisable()
    {
        StealFeedback.Happened -= Hear;
        ScoreFeedback.Gained -= HearGain;
        ScoreFeedback.LeadTaken -= HearLead;
        ModeFeedback.Said -= HearMode;
        ModeFeedback.Warned -= HearWarning;
    }

    void OnDestroy()
    {
        if (line != null)
            Destroy(line.gameObject);
    }

    /// <summary>A bump happened on this machine: this player's line, if they were in it, and a steal's jolt</summary>
    internal void Hear(BumpNotice notice)
    {
        if (player == null)
            return;

        int seat = OrderSync.SeatOf(player);
        bool attacker = seat == notice.Attacker, victim = seat == notice.Victim;
        if (seat < 0 || attacker == victim)
            return;

        CalloutTone tone;
        string key = attacker ? StealFeedbackRules.AttackerKey(notice.Outcome, notice.Paid, out tone)
            : StealFeedbackRules.VictimKey(notice.Outcome, out tone);
        if (key == null)
            return;

        string rival = StealFeedbackRules.Short(Scoreboard.ShownName(attacker ? notice.Victim : notice.Attacker));
        Show(Loc.Get(key, rival, notice.Paid), tone);

        if (notice.Outcome == BumpOutcome.Stolen && player.ball != null)
            player.ball.StealJolt(victim);

        // The thief's steal sound plays as the order changes hands (OrderHandler.TakeOrderFrom): these two are for the
        // player alone
        SoundPool sounds = player.GetComponent<SoundPool>();
        if (sounds != null && (victim || notice.Outcome != BumpOutcome.Stolen))
            sounds.PlayFeedback(victim ? FeedbackSounds.ROBBED : FeedbackSounds.THUD);
    }

    /// <summary>Puts a line up, in place of any before it</summary>
    internal void Show(string words, CalloutTone tone)
    {
        if (line == null)
            return;

        line.text = words;
        line.color = tone == CalloutTone.Thief ? GameCard.GOLD : tone == CalloutTone.Victim ? ModeIcons.RED : Color.white;
        shownAt = Clock();
        gainUp = false;
    }

    /// <summary>
    /// This player earned money on a delivery: a gold line with how much and why. Money heard within MERGE_SECONDS of the
    /// last gain line is added to it (the same reason and stamp keep their line; mixed ones read as a plain delivery).
    /// Another player's gain is not this player's line
    /// </summary>
    internal void HearGain(int seat, GainReason reason, int amount, string stamp)
    {
        if (player == null || seat < 0 || seat != OrderSync.SeatOf(player))
            return;

        stamp = stamp ?? "";
        float now = Clock();
        if (gainUp && shownAt >= 0f && now - shownAt < GainFeedbackRules.MERGE_SECONDS)
        {
            amount += gainAmount;
            if (reason != gainReason || stamp != gainStamp)
            {
                reason = GainReason.Delivered;
                stamp = "";
            }
        }

        Show(Loc.Get(GainFeedbackRules.Key(reason, stamp), amount, stamp), CalloutTone.Thief);
        gainUp = true;
        gainReason = reason;
        gainAmount = amount;
        gainStamp = stamp;
    }

    /// <summary>
    /// First place changed hands: the new leader reads that they took the lead (gold), the old leader reads who did (red),
    /// nobody else reads anything. A line younger than GAIN_HOLD is not cut short: the lead line waits for it (Tick)
    /// </summary>
    internal void HearLead(int newLeader, int oldLeader)
    {
        if (player == null)
            return;

        int seat = OrderSync.SeatOf(player);
        if (seat < 0 || newLeader == oldLeader)
            return;

        string words;
        CalloutTone tone;
        if (seat == newLeader)
        {
            words = Loc.Get("lead.you");
            tone = CalloutTone.Thief;
        }
        else if (seat == oldLeader)
        {
            words = Loc.Get("lead.other", StealFeedbackRules.Short(Scoreboard.ShownName(newLeader)));
            tone = CalloutTone.Victim;
        }
        else
            return;

        if (LineIsFresh())
        {
            leadHeld = true;
            leadWords = words;
            leadTone = tone;
            return;
        }

        leadHeld = false;
        Show(words, tone);
    }

    /// <summary>
    /// A mode line was said: this player's line if it is addressed to their seat, or to everyone but the player it names. It
    /// shows at once, in its tone, with the other player's short name in it. Having the cursed package also plays its sound
    /// </summary>
    internal void HearMode(ModeLine said)
    {
        if (player == null)
            return;

        int seat = OrderSync.SeatOf(player);
        if (seat < 0)
            return;
        bool mine = said.Seat == ModeLine.EVERYONE_ELSE ? seat != said.Other : seat == said.Seat;
        if (!mine)
            return;

        string other = said.Other >= 0 ? StealFeedbackRules.Short(Scoreboard.ShownName(said.Other)) : "";
        Show(Loc.Get(ModeFeedbackRules.Key(said.Kind), other), ModeFeedbackRules.Tone(said.Kind));

        if (said.Kind == ModeLineKind.PotatoYours)
            PlaySound(FeedbackSounds.CURSED);
    }

    static int warningSoundFrame = -1; // the frame the 30-second sound last played on this machine

    /// <summary>The round has 30 seconds left: a plain line, the warning sound, and a pulse of the timer's digits</summary>
    internal void HearWarning()
    {
        Show(Loc.Get("mode.line.thirty"), CalloutTone.Plain);
        // Every player on this machine hears the warning at the same moment: one of them plays it, so split screen
        // doesn't stack it four times over
        if (Time.frameCount != warningSoundFrame)
        {
            warningSoundFrame = Time.frameCount;
            PlaySound(FeedbackSounds.WARNING);
        }
        if (hud != null)
            hud.PulseTimer();
    }

    // A sound only this player hears. No sound pool, or no file for the sound, is no sound
    void PlaySound(string name)
    {
        SoundPool sounds = player != null ? player.GetComponent<SoundPool>() : null;
        if (sounds != null)
            sounds.PlayFeedback(name);
    }

    /// <summary>Whether a line is up that has not yet had GAIN_HOLD: a gain's, a steal's or another lead's. None is cut short</summary>
    bool LineIsFresh()
    {
        return shownAt >= 0f && Clock() - shownAt < GainFeedbackRules.GAIN_HOLD;
    }

    /// <summary>Puts up a lead line that was waiting, once no line is still fresh. Update calls it every frame</summary>
    internal void Tick()
    {
        if (!leadHeld || LineIsFresh())
            return;

        leadHeld = false;
        Show(leadWords, leadTone);
    }

    void Update()
    {
        if (line == null)
            return;

        GameManager game = GameManager.Instance;
        GameState state = game != null ? game.MainState : GameState.Default;
        if (Scoreboard.EndsMatch(state))
            leadHeld = false;
        Tick();
        if (shownAt < 0f)
            return;

        float alpha = StealFeedbackRules.CalloutAlpha(Clock() - shownAt);
        if (alpha <= 0f || Scoreboard.EndsMatch(state))
        {
            shownAt = -1f;
            gainUp = false;
            line.text = "";
            alpha = 0f;
        }
        // Paused, the menus have the screen
        line.alpha = state == GameState.Paused ? 0f : alpha;
    }
}
