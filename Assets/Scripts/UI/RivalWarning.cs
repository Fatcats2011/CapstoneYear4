using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A carrier's rival warning: one small red chevron at the edge of their own view, pointing outward, where a rival who
/// could steal from them is. Only a rival out of view gets a mark (left or right edge, or behind, low in the view). Steady
/// when the rival is near, pulsing when they are boosting straight at the carrier, with a short rumble as it starts
/// (BallDriving.WarnJolt). RivalWarningRules has every number. Each machine works it out from what it already has
/// (positions, who is boosting, held orders), so nothing goes over the network; another machine's scooter gets none, but
/// counts as a rival here. Made at runtime on the player's HUD canvas: no prefab holds it
/// </summary>
public class RivalWarning : MonoBehaviour
{
    internal const float SIZE = 40f;
    /// <summary>Where the mark sits, from the middle of the HUD (1920 x 1080)</summary>
    internal static readonly Vector2 LEFT = new Vector2(-900f, 0f), RIGHT = new Vector2(900f, 0f), BEHIND = new Vector2(0f, -340f);
    internal static readonly Color RED = new Color(1f, 0.25f, 0.2f);

    const int TEXTURE = 64;
    const float REFRESH_SECONDS = 1f;

    OrderHandler player;
    Image mark;
    ThreatLevel shown = ThreatLevel.None;
    float shownSince;
    float rumbledAt;
    int rumbles;

    // Update's own working: who is playing (refreshed now and then), the camera, and each rival's distance last frame
    readonly List<OrderHandler> everyone = new List<OrderHandler>();
    readonly Dictionary<OrderHandler, float> lastDistances = new Dictionary<OrderHandler, float>();
    Transform viewCamera;
    float nextRefresh;

    static Sprite chevron;

    /// <summary>The time now, in seconds. Tests set it so they can move time by hand</summary>
    internal System.Func<float> Clock = RealTime;

    /// <summary>Whether a match is being played. Tests set it: there is no GameManager in them</summary>
    internal static System.Func<bool> MatchOn;

    static float RealTime()
    {
        return Time.unscaledTime;
    }

    /// <summary>The level of the warning up now: None when there is no mark</summary>
    internal ThreatLevel ShownLevel { get { return shown; } }

    /// <summary>The mark's anchored position</summary>
    internal Vector2 ShownAt { get { return mark != null ? mark.rectTransform.anchoredPosition : Vector2.zero; } }

    /// <summary>How solid the mark is, 0 to 1</summary>
    internal float ShownAlpha { get { return mark != null ? mark.color.a : 0f; } }

    /// <summary>How many times the warning has asked for the rumble (tests)</summary>
    internal int Rumbles { get { return rumbles; } }

    /// <summary>Gives a player their rival warning, on their HUD's canvas. A player without a HUD here (another machine's) gets none</summary>
    internal static RivalWarning AddTo(OrderHandler player, NumberHandler hud)
    {
        Canvas canvas = hud != null ? hud.GetComponentInParent<Canvas>(true) : null;
        if (player == null || canvas == null)
            return null;

        RivalWarning warning = player.gameObject.AddComponent<RivalWarning>();
        warning.player = player;
        warning.Build(canvas.rootCanvas.transform);
        return warning;
    }

    // One red chevron, clear, small, not hit by the mouse
    void Build(Transform hud)
    {
        GameObject made = new GameObject("Rival Warning", typeof(RectTransform), typeof(Image));
        made.transform.SetParent(hud, false);
        mark = made.GetComponent<Image>();
        mark.sprite = Chevron();
        mark.color = new Color(RED.r, RED.g, RED.b, 0f);
        mark.raycastTarget = false;
        RectTransform rect = mark.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(SIZE, SIZE);
        rect.anchoredPosition = BEHIND;
    }

    void OnDestroy()
    {
        if (mark != null)
            Destroy(mark.gameObject);
    }

    // A chevron pointing up, white, drawn once from a distance function (as Colourblind.Draw does)
    static Sprite Chevron()
    {
        if (chevron != null)
            return chevron;

        Color[] pixels = new Color[TEXTURE * TEXTURE];
        for (int y = 0; y < TEXTURE; y++)
        {
            for (int x = 0; x < TEXTURE; x++)
            {
                float u = (x + 0.5f) / TEXTURE * 2f - 1f;
                float v = (y + 0.5f) / TEXTURE * 2f - 1f;
                float alpha = Mathf.Clamp01(-ChevronDistance(u, v) * TEXTURE / 2f + 0.5f);
                pixels[y * TEXTURE + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        Texture2D texture = new Texture2D(TEXTURE, TEXTURE, TextureFormat.RGBA32, false) { name = "Rival Warning Chevron", wrapMode = TextureWrapMode.Clamp };
        texture.SetPixels(pixels);
        texture.Apply();
        chevron = Sprite.Create(texture, new Rect(0, 0, TEXTURE, TEXTURE), new Vector2(0.5f, 0.5f), TEXTURE);
        return chevron;
    }

    /// <summary>How far a point (-1 to 1 each way, up is +v) is inside the chevron: negative inside, 0 on its edge. An up arrow's head: two thick arms meeting at the top</summary>
    internal static float ChevronDistance(float u, float v)
    {
        float arms = Mathf.Abs(v + Mathf.Abs(u) - 0.35f) * 0.7071f - 0.17f;
        return Mathf.Max(arms, Mathf.Abs(u) - 0.8f);
    }

    /// <summary>
    /// Shows a threat: the mark at the edge it is on, turned outward, at the solidity its level has. No level or no side
    /// (a rival in view) hides it. Coming up as charging from any other level asks for the rumble once
    /// </summary>
    internal void See(Threat threat)
    {
        ThreatLevel level = threat.Side == ThreatSide.None ? ThreatLevel.None : threat.Level;
        if (level != shown)
        {
            shownSince = Clock();
            // A rival's distance wobbles from frame to frame, so charging can blink off and on: that is one charge
            if (level == ThreatLevel.Charging && (rumbles == 0 || shownSince - rumbledAt >= RivalWarningRules.RUMBLE_GAP_SECONDS))
            {
                rumbledAt = shownSince;
                rumbles++;
                if (player != null && player.ball != null)
                    player.ball.WarnJolt();
            }
            shown = level;
        }

        if (mark == null)
            return;

        Color colour = mark.color;
        colour.a = RivalWarningRules.Alpha(shown, Clock() - shownSince, Comfort.ReduceMotion);
        mark.color = colour;
        if (shown == ThreatLevel.None)
            return;

        RectTransform rect = mark.rectTransform;
        switch (threat.Side)
        {
            case ThreatSide.Left:
                rect.anchoredPosition = LEFT;
                rect.localEulerAngles = new Vector3(0f, 0f, 90f);
                break;
            case ThreatSide.Right:
                rect.anchoredPosition = RIGHT;
                rect.localEulerAngles = new Vector3(0f, 0f, -90f);
                break;
            default:
                rect.anchoredPosition = BEHIND;
                rect.localEulerAngles = new Vector3(0f, 0f, 180f);
                break;
        }
    }

    /// <summary>
    /// The worst threat to a carrier now, out of everyone: for each rival (not the carrier, not nobody) remembers the
    /// distance for next time, then judges it. A carrier holding nothing has none
    /// </summary>
    /// <param name="camera">The carrier's view: where it looks decides the side. None means no side</param>
    /// <param name="lastDistances">Each rival's distance last time: closing is nearer than that. Updated here</param>
    internal static Threat Look(OrderHandler carrier, Transform camera, IEnumerable<OrderHandler> everyone, Dictionary<OrderHandler, float> lastDistances)
    {
        Threat worst = Threat.Nothing;
        if (carrier == null || everyone == null)
            return worst;

        bool playing = MatchOn != null ? MatchOn() : MatchIsOn();
        bool stealsOn = HouseRules.Playing.StealsOn || TutorialIsOn();
        bool carrierHolds = carrier.HeldCount > 0;
        Vector3 carrierAt = BallPosition(carrier);
        bool carrierRespawning = Respawning(carrier);
        Vector3 forward = camera != null ? camera.forward : Vector3.zero;

        foreach (OrderHandler rival in everyone)
        {
            if (rival == null || rival == carrier)
                continue;

            Vector3 toRival = BallPosition(rival) - carrierAt;
            float distance = toRival.magnitude;
            bool closing = lastDistances != null && lastDistances.TryGetValue(rival, out float last) && distance < last;
            if (lastDistances != null)
                lastDistances[rival] = distance;

            bool counts = RivalWarningRules.Counts(playing, stealsOn, carrierHolds, rival.HeldCount < 2, carrierRespawning || Respawning(rival));
            worst = RivalWarningRules.Worse(worst, Judge(counts, distance, toRival, forward, rival.IsBoosting, closing));
        }
        return worst;
    }

    /// <summary>The threat one rival is, from plain values: none when they don't count or are too far, else their level and side</summary>
    internal static Threat Judge(bool counts, float distance, Vector3 toRival, Vector3 cameraForward, bool rivalBoosting, bool closing)
    {
        if (!counts)
            return Threat.Nothing;

        ThreatLevel level = RivalWarningRules.Level(distance, rivalBoosting, closing);
        if (level == ThreatLevel.None)
            return Threat.Nothing;
        return new Threat(level, RivalWarningRules.Side(toRival, cameraForward), distance);
    }

    static bool MatchIsOn()
    {
        GameManager game = GameManager.Instance;
        return game != null && (game.MainState == GameState.Begin || game.MainState == GameState.Tutorial);
    }

    // As OrderHandler.JudgeBump: steals are on in the rules, and the tutorial teaches them anyway
    static bool TutorialIsOn()
    {
        GameManager game = GameManager.Instance;
        return game != null && game.MainState == GameState.Tutorial;
    }

    // Where a scooter's ball is, as OnlineSteals measures players. A scooter without one is at the origin
    static Vector3 BallPosition(OrderHandler player)
    {
        BallDriving ball = player.ball;
        return ball != null && ball.Sphere != null ? ball.Sphere.transform.position : player.transform.position;
    }

    // Respawning: its own for the whole respawn, another machine's while its rider is hidden (as OnlineSteals)
    static bool Respawning(OrderHandler player)
    {
        BallDriving ball = player.ball;
        Respawn respawn = ball != null && ball.Sphere != null ? ball.Sphere.GetComponent<Respawn>() : null;
        return respawn != null && (respawn.IsRespawning || respawn.RiderHidden);
    }

    void Update()
    {
        if (mark == null || player == null)
            return;

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + REFRESH_SECONDS;
            Refresh();
        }
        See(Look(player, viewCamera, everyone, lastDistances));
    }

    // Who is in the match, and this player's camera
    void Refresh()
    {
        everyone.Clear();
        PlayerInstantiate instantiate = PlayerInstantiate.Instance;
        if (instantiate == null)
            return;

        foreach (PlayerSlot slot in instantiate.Roster.Players)
        {
            OrderHandler handler = slot.Player != null ? slot.Player.GetComponentInChildren<OrderHandler>(true) : null;
            if (handler != null)
                everyone.Add(handler);
            if (handler == player && slot.Input != null)
            {
                PlayerCameraResizer resizer = slot.Input.GetComponent<PlayerCameraResizer>();
                viewCamera = resizer != null && resizer.PlayerReferenceCamera != null ? resizer.PlayerReferenceCamera.transform : null;
            }
        }
    }
}
