using System;
using UnityEngine;

/// <summary>
/// The boost-charge ring on the ground under a scooter (shader DoA/Boost Ring), so a player can't miss when the boost is
/// back. PhaseIndicator drives it every frame from the same gauge as the horns (Show), so the two always agree: it fills
/// round in the horns' colours, dashed while the recharge is slowed (a package), and glows and pulses when full, with
/// the boost button in front of it for its own player only (their world layer). Spending the boost bursts it outward
/// (Burst, on BallDriving.OnBoostStart). A boost left full for a while offers the BoostReady hint once a full spell.
/// It sits on the ground under BallDriving.ScooterNormal, not under the wheelie or lean, and fades out in the air.
/// Another machine's scooter shows none (Hide, from RemoteAvatar). Every timing is in BoostRingRules.
/// Built into PlayerAvatar.prefab by DoA/Setup/Boost Ring (BoostRingSetup)
/// </summary>
public class BoostRing : MonoBehaviour
{
    static readonly int FillId = Shader.PropertyToID("_Fill");
    static readonly int PulseId = Shader.PropertyToID("_Pulse");
    static readonly int DashedId = Shader.PropertyToID("_Dashed");
    static readonly int FadeId = Shader.PropertyToID("_Fade");
    static readonly int ColorLowId = Shader.PropertyToID("_ColorLow");
    static readonly int ColorFullId = Shader.PropertyToID("_ColorFull");
    static readonly int BurstScaleId = Shader.PropertyToID("_BurstScale");
    static readonly int BurstAlphaId = Shader.PropertyToID("_BurstAlpha");
    static readonly int RingRadiusId = Shader.PropertyToID("_RingRadius");
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");

    /// <summary>How much wider the quad is than the ring, leaving room for the burst (BoostRingRules grows it to 1.6)</summary>
    const float QUAD_SPAN = 1.75f;
    /// <summary>How long a just-filled ring flashes, with the horns</summary>
    const float FILL_FLASH_SECONDS = 0.25f;

    [Tooltip("The flat quad drawing the ring (DoA/Boost Ring)")]
    [SerializeField] internal Renderer ring;
    [Tooltip("The boost button prompt: a world-space canvas, shown to its own player only")]
    [SerializeField] internal GameObject prompt;
    [SerializeField] internal BallDriving control;

    [Header("Size and place")]
    [Tooltip("The ring's radius, in metres")]
    [SerializeField] float radius = 1.2f;
    [Tooltip("The band's width, as a share of the radius")]
    [Range(0.02f, 0.5f)]
    [SerializeField] float thickness = 0.12f;
    [Tooltip("How far above the ground the ring floats, so the ground doesn't flicker through it")]
    [SerializeField] float lift = 0.05f;
    [Tooltip("How far below the ground-check point (BallDriving.ScooterNormal) the ground is")]
    [SerializeField] float groundBelowCheck = 0.11f;
    [Tooltip("How fast the ring fades out in the air and back on landing, per second")]
    [SerializeField] float fadeSpeed = 6f;

    [Header("Prompt")]
    [Tooltip("How far in front of the ring's edge the prompt stands")]
    [SerializeField] float promptAhead = 0.3f;
    [SerializeField] float promptHeight = 0.4f;
    [Tooltip("How far the prompt leans back, towards the camera behind and above")]
    [SerializeField] float promptTilt = 35f;

    [Header("Colours (the horns' own replace these at start: PhaseIndicator)")]
    [SerializeField] Gradient charging = DefaultCharging();
    [SerializeField] Color full = new Color(0.27482426f, 0.07075471f, 1f, 1f);

    /// <summary>The seconds since the last frame: tests set it so they needn't run frames</summary>
    internal Func<float> DeltaTime = () => Time.deltaTime;

    /// <summary>How long the boost has sat full and unspent</summary>
    internal float TimeFull { get; private set; }

    MaterialPropertyBlock block;
    PlayerCameraResizer view;
    bool ready;
    bool listening;
    bool hidden;
    bool hintOffered;
    bool isBot; // looked up as each full spell starts (a developer's autopilot can come and go: BotAutopilot)
    bool fromOutsideAMatch = true; // the last state seen was outside a match (the lobby, a cutscene, the results)
    float burstAge = BoostRingRules.BURST_SECONDS;
    float fade = 1f;

    static Gradient DefaultCharging()
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(0.2264151f, 0.2264151f, 0.2264151f), 0f), new GradientColorKey(new Color(0.78382945f, 0.09019607f, 1f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return gradient;
    }

    void Start()
    {
        Ready();
    }

    void OnDestroy()
    {
        if (listening && control != null)
            control.OnBoostStart -= Burst;
    }

    /// <summary>Finds its parts and sizes the ring once: Show may come before Start, and tests never run it</summary>
    void Ready()
    {
        if (ready)
            return;
        ready = true;

        if (control == null)
            control = GetComponentInParent<BallDriving>();
        if (control != null && !listening)
        {
            control.OnBoostStart += Burst;
            listening = true;
        }

        block = new MaterialPropertyBlock();
        if (ring != null)
        {
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // the quad lies flat, facing up, its +V forward
            ring.transform.localScale = Vector3.one * (radius * 2f * QUAD_SPAN);
        }
        if (prompt != null)
        {
            prompt.transform.localPosition = new Vector3(0f, promptHeight, radius + promptAhead);
            prompt.transform.localRotation = Quaternion.Euler(promptTilt, 0f, 0f);
        }
    }

    /// <summary>The horns' colours: the charging gradient and the ready colour, so the ring matches them</summary>
    public void SetColours(Gradient chargingColours, Color fullColour)
    {
        if (chargingColours != null)
            charging = chargingColours;
        full = fullColour;
    }

    /// <summary>
    /// The gauge this frame (fill 0 to 1; slowed: a slower recharge, a package). Called every frame, not only when the
    /// gauge moves, so the time it has sat full keeps counting
    /// </summary>
    public void Show(float fill, bool slowed)
    {
        if (hidden)
            return;
        Ready();

        float dt = DeltaTime();
        bool inMatch = FollowMatch();
        // A boost under way drains the gauge from full: its first frame still reads full, and must not glow or prompt
        bool isFull = BoostRingRules.Full(fill) && (control == null || !control.Boosting);
        if (isFull)
        {
            if (TimeFull <= 0f)
                isBot = control != null && control.GetComponent<BotDriver>() != null;
            TimeFull += dt;
        }
        else
        {
            TimeFull = 0f;
            hintOffered = false;
        }
        burstAge = Mathf.Min(burstAge + dt, BoostRingRules.BURST_SECONDS); // held at the end: no burst
        bool grounded = control == null || control.Grounded;
        fade = Mathf.MoveTowards(fade, grounded ? 1f : 0f, fadeSpeed * dt);

        Draw(fill, slowed, isFull);

        ShowPrompt();
        OfferHint(inMatch);

        // Never in the menus: in the lobby its button prompt sat inside the scooter on Dispatch's stand (playtest,
        // 2026-11-20). Everything above still runs there, its clock included
        bool shows = !InMenus(StateNow());
        if (ring != null && ring.enabled != shows)
            ring.enabled = shows;
        if (!shows && prompt != null && prompt.activeSelf)
            prompt.SetActive(false);
    }

    /// <summary>
    /// Whether this is a match, where the idle hint may be offered. Coming into one from outside (the lobby, a cutscene,
    /// the results), a ring left full there starts its time full again: else a rematch would offer the hint on its first
    /// frame, and online the prompt's delay would already be over. A pause or the golden cutscene isn't leaving it
    /// </summary>
    bool FollowMatch()
    {
        GameState? state = StateNow();
        bool inMatch = state == null || state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
        if (inMatch)
        {
            if (fromOutsideAMatch)
            {
                TimeFull = 0f;
                hintOffered = false;
            }
            fromOutsideAMatch = false;
        }
        else if (state != GameState.Paused && state != GameState.GoldenCutscene)
            fromOutsideAMatch = true;
        return inMatch;
    }

    /// <summary>The title, options, credits, player select or Dispatch: no ring there</summary>
    internal static bool InMenus(GameState? state)
    {
        return state == GameState.Menu || state == GameState.Options || state == GameState.Credits || state == GameState.PlayerSelect;
    }

    /// <summary>The game state now, as the hints see it (tests set Hints.State); none without a game manager</summary>
    static GameState? StateNow()
    {
        if (Hints.State != null)
            return Hints.State();
        GameManager game = GameManager.Instance;
        return game != null ? game.MainState : (GameState?)null;
    }

    void Draw(float fill, bool slowed, bool isFull)
    {
        if (ring == null)
            return;

        float flash = isFull && TimeFull < FILL_FLASH_SECONDS ? 1f - TimeFull / FILL_FLASH_SECONDS : 0f;
        float ringRadius = 0.5f / QUAD_SPAN;

        ring.GetPropertyBlock(block);
        block.SetFloat(FillId, fill);
        block.SetFloat(PulseId, Mathf.Max(BoostRingRules.Pulse(TimeFull), flash));
        block.SetFloat(DashedId, BoostRingRules.Slowed(slowed, fill) ? 1f : 0f);
        block.SetFloat(FadeId, fade);
        block.SetColor(ColorLowId, charging.Evaluate(fill));
        block.SetColor(ColorFullId, full);
        block.SetFloat(BurstScaleId, BoostRingRules.BurstScale(burstAge));
        block.SetFloat(BurstAlphaId, BoostRingRules.BurstAlpha(burstAge));
        block.SetFloat(RingRadiusId, ringRadius);
        block.SetFloat(ThicknessId, ringRadius * thickness);
        ring.SetPropertyBlock(block);
    }

    void ShowPrompt()
    {
        if (prompt == null)
            return;

        bool show = BoostRingRules.ShowPrompt(BoostPrompts.WithPeopleOnline(), BoostPrompts.OnlineMatches, isBot, TimeFull)
            && OnlyItsPlayerSees();
        if (prompt.activeSelf != show)
            prompt.SetActive(show);
    }

    /// <summary>
    /// Puts the prompt on its player's world layer, which only their camera draws. False while their view isn't set up
    /// yet, and for a scooter with no view (no PlayerCameraResizer above it: the Player - Cinemachine root holds a local
    /// player's): every camera would see it there
    /// </summary>
    bool OnlyItsPlayerSees()
    {
        if (view == null)
            view = GetComponentInParent<PlayerCameraResizer>(true);
        if (view == null)
            return false;

        int layer = view.playerWorldLayer;
        if (layer == 0)
            return false;
        if (prompt.layer != layer)
        {
            foreach (Transform part in prompt.GetComponentsInChildren<Transform>(true))
                part.gameObject.layer = layer;
        }
        return true;
    }

    /// <summary>A boost left full: the BoostReady hint, once a full spell, in a match (not the lobby: Hints would spend it there)</summary>
    void OfferHint(bool inMatch)
    {
        if (hintOffered || isBot || !inMatch || !BoostRingRules.OfferIdleHint(TimeFull))
            return;
        hintOffered = true;
        Hints.Offer(HintKind.BoostReady, OrderSync.SeatOf(this));
    }

    /// <summary>The boost was spent: the ring flares out and fades, and starts filling from empty</summary>
    public void Burst()
    {
        TimeFull = 0f;
        hintOffered = false;
        burstAge = 0f;
    }

    /// <summary>No ring here at all (another machine's scooter: its charge isn't known here)</summary>
    public void Hide()
    {
        hidden = true;
        if (ring != null)
            ring.enabled = false;
        if (prompt != null)
            prompt.SetActive(false);
        enabled = false;
    }

    // Follows the ground under the scooter, after the scooter has moved this frame
    void LateUpdate()
    {
        if (hidden || control == null || control.ScooterNormal == null)
            return;

        Transform ground = control.ScooterNormal;
        Vector3 up = ground.up;
        Vector3 forward = Vector3.ProjectOnPlane(control.transform.forward, up);
        if (forward.sqrMagnitude < 1e-6f)
            forward = Vector3.ProjectOnPlane(ground.forward, up);
        transform.SetPositionAndRotation(ground.position + up * (lift - groundBelowCheck), Quaternion.LookRotation(forward, up));
    }
}
