using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A player's ghost emoting in a match, from what this machine already sees, so every machine shows every ghost's emotes
/// with nothing sent: a steal (StealFeedback), a delivery (ScoreFeedback), a boost (BallDriving.OnBoostStart), a hard bump
/// (FeatSync.Bumped, offline and on the host), and, polled each frame, time in the air, the golden order, rivals close
/// behind or a carrier ahead, and standing still. Two come as one-shots online (OnlineCues): the player's d-pad emotes
/// (Pad, ShowPad) and, on clients, a hard bump (ShowDizzy). PlayerEmoteRules decides when; GhostEmotes plays it
/// (priorities and cooldowns). Cosmetic only: nothing here changes play. Sits on the player's top object, put there as the
/// player is seated (Attach: PlayerInstantiate).
/// Outside the match, in player select: Materialise once the player has joined (Joined, a frame later, so the ghost is
/// in place), WarmUp or KnuckleCrack as they ready up (Readied), HatShowOff or MirrorCheck as their hat or colour changes
/// (Restyled), and, polled, Impatient while they're ready and someone else isn't. At the match's 3, 2, 1 every ghost
/// RevBounces twice (Countdown). The results' emotes are PodiumEmotes'.
/// It tells the ghost's emotes what the player carries (IGhostCarry), so a parcel move holds the order's package
/// </summary>
public class PlayerEmotes : MonoBehaviour, IGhostCarry
{
    /// <summary>Seconds over which a ghost's measured velocity settles (remote poses arrive in steps)</summary>
    const float VELOCITY_SMOOTHING = 0.15f;
    /// <summary>Faster than this between two frames is a teleport (a respawn, the podium): no velocity from it</summary>
    const float TELEPORT_SPEED = 60f;

    /// <summary>Every ghost's emotes that is switched on: the others each one looks at</summary>
    static readonly List<PlayerEmotes> all = new List<PlayerEmotes>();

    /// <summary>Whether a match is being driven now. Tests set it: there is no GameManager in them</summary>
    internal static Func<bool> MatchOn;

    /// <summary>Whether the players are in player select now. Tests set it</summary>
    internal static Func<bool> LobbyOn;

    /// <summary>
    /// Whether a seat's player is ready in player select: null for an empty seat or a CPU driver (who never holds the
    /// start up). Tests set it; otherwise PlayerInstantiate says
    /// </summary>
    internal static Func<int, bool?> SeatReady;

    OrderHandler handler;
    BallDriving ball;
    GhostEmotes emotes;
    BallDriving hooked;   // the ball whose boosts it hears
    bool listening;

    float airFor;
    float airLeft;              // the ball's height on the ground, last grounded frame (where it took off from)
    float airRise;              // the most it has risen above that, this time in the air
    bool wheeed;                // Wheee asked for, this time in the air
    float stillFor;
    bool wasGolden;
    float sinceTreasure;
    bool treasureOwed;          // a TreasureHold asked for was turned away (something more important played): again until it plays
    float sinceSideEye = PlayerEmoteRules.SIDE_EYE_EVERY;

    Vector3 lastPosition;
    bool hasLast;

    // The lobby and the countdown
    int materialiseIn = -1;     // frames until it Materialises (-1: not waiting to)
    float waitedFor;            // seconds ready while someone else isn't
    float revAgainIn = -1f;     // seconds until the countdown's second RevBounce (-1: none to come)
    GhostAction? lastReady;     // the last ready-up emote picked (they take turns)
    GhostAction? lastStyle;     // the last hat or colour emote picked

    /// <summary>Picks between two actions ("X or Y"); tests seed it</summary>
    internal System.Random Random = new System.Random();

    /// <summary>The seat it plays as, when set (tests); otherwise looked up in the roster</summary>
    internal int SeatOverride = -1;

    /// <summary>The emote last asked for, whether or not it played (tests)</summary>
    internal GhostAction? Asked { get; private set; }

    /// <summary>How many emotes have been asked for (tests)</summary>
    internal int AskCount { get; private set; }

    static readonly int ACTIONS = Enum.GetValues(typeof(GhostAction)).Length;
    readonly int[] askedEach = new int[ACTIONS];

    /// <summary>How many times an action has been asked for (tests)</summary>
    internal int AskedFor(GhostAction action)
    {
        return askedEach[(int)action];
    }

    /// <summary>The ghost's flat velocity, smoothed, in m/s</summary>
    internal Vector3 Velocity { get; private set; }

    /// <summary>Where the ghost's ball is</summary>
    internal Vector3 Position { get; private set; }

    /// <summary>The ghost's emotes (null when its model has no Animator)</summary>
    internal GhostEmotes Emotes => emotes;

    internal OrderHandler Handler => handler;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForPlay()
    {
        all.Clear();
        MatchOn = null;
        LobbyOn = null;
        SeatReady = null;
    }

    /// <summary>
    /// Gives a player's top object its emotes, once (calling it again only finds its parts again). Null when there's no
    /// object or no OrderHandler under it
    /// </summary>
    public static PlayerEmotes Attach(GameObject avatarRoot)
    {
        if (avatarRoot == null || avatarRoot.GetComponentInChildren<OrderHandler>(true) == null)
            return null;

        PlayerEmotes player = avatarRoot.GetComponent<PlayerEmotes>();
        if (player == null)
            player = avatarRoot.AddComponent<PlayerEmotes>();
        player.Bind();
        return player;
    }

    /// <summary>Finds its OrderHandler, ball and Animator, and the ghost's emotes on that Animator</summary>
    internal void Bind()
    {
        handler = GetComponentInChildren<OrderHandler>(true);
        ball = handler != null && handler.ball != null ? handler.ball : GetComponentInChildren<BallDriving>(true);
        Animator animator = handler != null ? handler.PlayerAnimator : null;
        if (animator == null && ball != null)
            animator = ball.playerAnimator;
        emotes = GhostEmotes.On(animator);
        if (emotes != null)
            emotes.Carry = this;
        if (handler != null)
            Position = BallPosition();
        if (listening)
            HookBall();
    }

    /// <summary>The package of an order the player carries now (the first picked up), null when none</summary>
    public Transform Parcel()
    {
        if (handler == null)
            return null;
        Transform mesh = MeshOf(handler.order2);
        return mesh != null ? mesh : MeshOf(handler.order1);
    }

    /// <summary>Whether the player still carries the order this package belongs to</summary>
    public bool Carries(Transform mesh)
    {
        return handler != null && mesh != null && (MeshOf(handler.order1) == mesh || MeshOf(handler.order2) == mesh);
    }

    // An order's package, when this player holds it and it shows
    Transform MeshOf(Order order)
    {
        if (order == null || order.PlayerHolding != handler || !order.IsActive || order.orderMeshObject == null)
            return null;
        return order.orderMeshObject.transform;
    }

    internal void OnEnable()
    {
        if (!all.Contains(this))
            all.Add(this);
        if (listening)
            return;
        listening = true;
        StealFeedback.Happened += HearSteal;
        ScoreFeedback.Gained += HearGain;
        FeatSync.Bumped += HearBump;
        HookBall();
    }

    internal void OnDisable()
    {
        all.Remove(this);
        if (!listening)
            return;
        listening = false;
        StealFeedback.Happened -= HearSteal;
        ScoreFeedback.Gained -= HearGain;
        FeatSync.Bumped -= HearBump;
        UnhookBall();
        Forget();
    }

    void HookBall()
    {
        if (hooked == ball)
            return;
        UnhookBall();
        hooked = ball;
        if (hooked != null)
            hooked.OnBoostStart += HearBoost;
    }

    void UnhookBall()
    {
        if (hooked != null)
            hooked.OnBoostStart -= HearBoost;
        hooked = null;
    }

    /// <summary>The seat it plays as (-1 for none)</summary>
    internal int Seat
    {
        get
        {
            if (SeatOverride >= 0)
                return SeatOverride;
            return handler != null ? OrderSync.SeatOf(handler) : -1;
        }
    }

    static bool MatchIsOn()
    {
        if (MatchOn != null)
            return MatchOn();
        GameManager game = GameManager.Instance;
        if (game == null)
            return false;
        GameState state = game.MainState;
        return state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
    }

    static bool LobbyIsOn()
    {
        if (LobbyOn != null)
            return LobbyOn();
        GameManager game = GameManager.Instance;
        return game != null && game.MainState == GameState.PlayerSelect;
    }

    static bool? IsSeatReady(int seat)
    {
        if (SeatReady != null)
            return SeatReady(seat);
        PlayerInstantiate players = PlayerInstantiate.Instance;
        if (players == null || seat < 0 || seat >= Constants.MAX_PLAYERS)
            return null;
        PlayerSlot slot = players.Roster[seat];
        if (slot == null || slot.IsBot)
            return null;
        return players.IsReady(seat);
    }

    /// <summary>The emotes on a player's top object, if it has them</summary>
    public static PlayerEmotes Of(GameObject avatarRoot)
    {
        return avatarRoot != null ? avatarRoot.GetComponent<PlayerEmotes>() : null;
    }

    // ---- The lobby and the countdown

    /// <summary>
    /// The player has just joined (PlayerInstantiate.AddPlayerReference, AddRemotePlayer): it Materialises a frame later,
    /// once its ghost is in place, or as it reaches player select when it joined before (the first player, in the main
    /// menu). Never in a match
    /// </summary>
    public void Joined()
    {
        materialiseIn = 1;
    }

    /// <summary>The player readied up in player select (PlayerInstantiate.ReadyUp, SetRemoteReady): WarmUp or KnuckleCrack</summary>
    public void Readied()
    {
        if (!LobbyIsOn())
            return;
        lastReady = PlayerEmoteRules.Readied(lastReady, Random);
        Ask(lastReady.Value);
    }

    /// <summary>
    /// The player's hat or colour changed in player select (CustomizationSelector, OnlineGame for another machine's):
    /// HatShowOff or MirrorCheck
    /// </summary>
    public void Restyled()
    {
        if (!LobbyIsOn())
            return;
        lastStyle = PlayerEmoteRules.Restyled(lastStyle, Random);
        Ask(lastStyle.Value);
    }

    /// <summary>
    /// The match's 3, 2, 1 began (PlayerInstantiate, on every machine): every ghost RevBounces, and again REV_AGAIN later
    /// (or as the first ends, when it runs longer)
    /// </summary>
    public static void Countdown()
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null)
                all[i].Rev();
        }
    }

    internal void Rev()
    {
        Ask(GhostAction.RevBounce);
        revAgainIn = PlayerEmoteRules.REV_AGAIN;
    }

    /// <summary>
    /// A frame outside the match: the Materialise waiting for its frame, the countdown's second RevBounce, and in player
    /// select, Impatient while ready with someone else not
    /// </summary>
    void TickLobby(float dt, bool matchOn)
    {
        if (revAgainIn >= 0f)
        {
            revAgainIn = Mathf.Max(0f, revAgainIn - dt);
            // A longer first one (some versions outlast REV_AGAIN) ends first: the second would be turned away
            bool firstPlaying = emotes != null && emotes.Playing != null && emotes.Playing.Action == GhostAction.RevBounce;
            if (revAgainIn <= 0f && !firstPlaying)
            {
                revAgainIn = -1f;
                Ask(GhostAction.RevBounce);
            }
        }

        bool lobby = !matchOn && LobbyIsOn();
        if (matchOn)
            materialiseIn = -1;
        else if (materialiseIn > 0)
            materialiseIn--;
        else if (materialiseIn == 0 && lobby)
        {
            materialiseIn = -1;
            Ask(GhostAction.Materialise);
        }

        float waitedBefore = waitedFor;
        waitedFor = lobby && Waiting() ? waitedFor + dt : 0f;
        if (PlayerEmoteRules.ImpatientDue(waitedBefore, waitedFor))
            Ask(GhostAction.Impatient);
    }

    // Ready, with another player who isn't
    bool Waiting()
    {
        int seat = Seat;
        if (seat < 0 || IsSeatReady(seat) != true)
            return false;
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            if (i != seat && IsSeatReady(i) == false)
                return true;
        }
        return false;
    }

    /// <summary>Asks for an emote. False only when the ghost's emotes turned it away (true without any: nothing to wait for)</summary>
    bool Ask(GhostAction action)
    {
        Asked = action;
        AskCount++;
        askedEach[(int)action]++;
        return emotes == null || emotes.Play(action);
    }

    /// <summary>A bump on this machine: Cackle for this seat's steal, FistShake for being robbed</summary>
    internal void HearSteal(BumpNotice notice)
    {
        if (!MatchIsOn())
            return;
        GhostAction? action = PlayerEmoteRules.ForSteal(Seat, notice.Attacker, notice.Victim, notice.Outcome);
        if (action.HasValue)
            Ask(action.Value);
    }

    /// <summary>Money came in on this machine: this seat's delivery is a DeliveryFlourish</summary>
    internal void HearGain(int seat, GainReason reason, int amount, string stamp)
    {
        if (!MatchIsOn())
            return;
        GhostAction? action = PlayerEmoteRules.ForGain(Seat, seat, reason);
        if (action.HasValue)
            Ask(action.Value);
    }

    /// <summary>This ghost's scooter boosted</summary>
    internal void HearBoost()
    {
        if (MatchIsOn())
            Ask(GhostAction.HoldYourHat);
    }

    /// <summary>
    /// This machine's player pressed a d-pad emote (EmoteHandler; 0 up, 1 right, 2 down, 3 left): its ghost plays the
    /// matching action (PlayerEmoteRules.ForPad). Online the other machines play it too: it goes out as a one-shot
    /// (CueSync, as SoundPool's do), and they show it on this ghost (OnlineCues.Show, ShowPad)
    /// </summary>
    public void Pad(int index)
    {
        if (!ShowPad(index))
            return;

        int seat = Seat;
        if (GameAuthority.IsOnline && seat >= 0 && !RemoteAvatar.IsRemote(handler))
            CueSync.Play(seat, ScooterCue.Emote(index));
    }

    /// <summary>
    /// A d-pad emote on this ghost, from this machine's player (Pad) or another machine's (OnlineCues.Show). False for an
    /// index out of range
    /// </summary>
    public bool ShowPad(int index)
    {
        GhostAction? action = PlayerEmoteRules.ForPad(index);
        if (!action.HasValue)
            return false;
        Ask(action.Value);
        return true;
    }

    /// <summary>
    /// Online, on a client: the host says this seat was bumped hard (CueKind.Dizzy, OnlineCues.Show). The host and offline
    /// hear the bump itself (HearBump)
    /// </summary>
    public void ShowDizzy()
    {
        Ask(GhostAction.SeeingStars);
    }

    /// <summary>A bump (offline and on the host; clients get CueKind.Dizzy instead): this seat hit hard is SeeingStars</summary>
    internal void HearBump(int bumper, int victim, float strength)
    {
        if (!MatchIsOn() || victim < 0 || victim != Seat || !PlayerEmoteRules.HardBump(strength))
            return;
        Ask(GhostAction.SeeingStars);
    }

    void Update()
    {
        Tick(Time.deltaTime);
    }

    /// <summary>
    /// A frame: where it is and how fast it goes, then, in a match, its time in the air, the golden order, standing still,
    /// and the others around it. Outside a match it forgets its timers
    /// </summary>
    internal void Tick(float dt)
    {
        if (handler == null)
            return;

        Measure(dt);
        bool matchOn = MatchIsOn();
        TickLobby(dt, matchOn);
        if (!matchOn || Respawning(this))
        {
            Forget();
            return;
        }

        // In the air
        airFor = PlayerEmoteRules.AirFor(airFor, ball == null || ball.Grounded, dt);
        if (airFor <= 0f)
        {
            airLeft = Position.y;
            airRise = 0f;
            wheeed = false;
        }
        else
        {
            airRise = PlayerEmoteRules.Rise(airRise, Position.y, airLeft);
        }
        if (PlayerEmoteRules.Wheee(airFor, airRise, wheeed))
        {
            wheeed = true;
            Ask(GhostAction.Wheee);
        }

        // The golden order
        bool golden = handler.HoldsGoldenOrder;
        sinceTreasure += dt;
        if (!golden)
            treasureOwed = false;
        // One turned away (a steal's Cackle, a boost's HoldYourHat as it's picked up) plays as soon as it can
        bool owedNow = treasureOwed && (emotes == null || emotes.CanPlay(GhostAction.TreasureHold));
        if (PlayerEmoteRules.TreasureHold(wasGolden, golden, sinceTreasure) || owedNow)
        {
            sinceTreasure = 0f;
            treasureOwed = !Ask(GhostAction.TreasureHold);
        }
        wasGolden = golden;

        // The others
        sinceSideEye += dt;
        GhostAction? look = LookAround();
        if (look.HasValue)
        {
            if (look.Value == GhostAction.SideEye)
                sinceSideEye = 0f;
            Ask(look.Value);
        }

        // Standing still
        float stillBefore = stillFor;
        stillFor = PlayerEmoteRules.StillFor(stillFor, Velocity.magnitude, dt);
        if (PlayerEmoteRules.Bored(stillBefore, stillFor))
            Ask(GhostAction.Bored);
    }

    /// <summary>Its timers back to the start (outside a match, respawning, switched off)</summary>
    void Forget()
    {
        airFor = 0f;
        airLeft = Position.y;
        airRise = 0f;
        wheeed = false;
        stillFor = 0f;
        wasGolden = false;
        sinceTreasure = 0f;
        treasureOwed = false;
        sinceSideEye = PlayerEmoteRules.SIDE_EYE_EVERY;
    }

    /// <summary>Where the ball is now, and its flat velocity, smoothed. A teleport gives none</summary>
    void Measure(float dt)
    {
        Vector3 now = BallPosition();
        if (hasLast && dt > 0f)
        {
            Vector3 raw = (now - lastPosition) / dt;
            raw.y = 0f;
            if (raw.magnitude > TELEPORT_SPEED)
                Velocity = Vector3.zero;
            else
                Velocity = Vector3.Lerp(Velocity, raw, 1f - Mathf.Exp(-dt / VELOCITY_SMOOTHING));
        }
        lastPosition = now;
        hasLast = true;
        Position = now;
    }

    // As RivalWarning: the ball's position, or the handler's without one
    Vector3 BallPosition()
    {
        return ball != null && ball.Sphere != null ? ball.Sphere.transform.position : handler.transform.position;
    }

    Vector3 Forward()
    {
        return ball != null ? ball.transform.forward : handler.transform.forward;
    }

    /// <summary>
    /// The others: carrying, the worst rival behind (JumpScare over SideEye); holding nothing, a carrier ahead it is
    /// closing on (Scheme). Rivals count as RivalWarningRules.Counts has it (steals on, room to take, nobody respawning)
    /// </summary>
    GhostAction? LookAround()
    {
        bool stealsOn = HouseRules.Playing.StealsOn;
        int held = handler.HeldCount;
        bool carrying = held > 0;
        Vector3 forward = Forward();
        GhostAction? worst = null;

        for (int i = 0; i < all.Count; i++)
        {
            PlayerEmotes other = all[i];
            if (other == this || other == null || other.handler == null || !other.hasLast)
                continue;

            Vector3 toOther = other.Position - Position;
            toOther.y = 0f;
            float distance = toOther.magnitude;
            // The gap shrinking, whoever does the moving (it shrinks as fast seen from either side)
            bool closing = PlayerEmoteRules.Closing(PlayerEmoteRules.ClosingSpeed(toOther, Velocity, other.Velocity));
            bool respawning = Respawning(other);
            int otherHeld = other.handler.HeldCount;

            if (carrying)
            {
                if (!RivalWarningRules.Counts(true, stealsOn, true, otherHeld < 2, respawning))
                    continue;
                worst = PlayerEmoteRules.Worse(worst,
                    PlayerEmoteRules.CarrierLook(distance, PlayerEmoteRules.Behind(forward, toOther), closing, sinceSideEye));
            }
            else
            {
                if (!RivalWarningRules.Counts(true, stealsOn, otherHeld > 0, held < 2, respawning))
                    continue;
                if (PlayerEmoteRules.Scheme(false, true, distance, PlayerEmoteRules.Ahead(forward, toOther), closing))
                    return GhostAction.Scheme;
            }
        }
        return worst;
    }

    // As RivalWarning: respawning, or (another machine's) its rider hidden
    static bool Respawning(PlayerEmotes player)
    {
        BallDriving ball = player.ball;
        Respawn respawn = ball != null && ball.Sphere != null ? ball.Sphere.GetComponent<Respawn>() : null;
        return respawn != null && (respawn.IsRespawning || respawn.RiderHidden);
    }
}
