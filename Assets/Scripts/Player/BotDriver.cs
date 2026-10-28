using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A CPU driver for one scooter (bots phase G1): it drives as a pad would (IDriveInput), towards what its personality
/// (Kind) scores best — a free order, or a rival to steal from, boosting into them when close — then to where its order
/// goes (BotTactics), along a path on the Scooter NavMesh (straight at it without one), boosting when lined up on a far
/// corner and backing out when stuck. The courier swerves from boosting rivals while it carries something.
/// Set it as the scooter's BallDriving.DriveInput. Only where the rules run: a client's scooters follow the host's.
/// Developers: F7 hands Player 1's scooter to a bot and back (BotAutopilot)
/// </summary>
public class BotDriver : MonoBehaviour, IDriveInput
{
    const float BOOST_HOLD = 0.2f;    // how long it holds boost, in seconds
    const float BOOST_EVERY = 2.5f;   // the least time between boosts
    const string AGENT = "Scooter";   // the NavMesh agent type the scenes bake for scooters (EDITOR-TODO.md, Bots)
    const float REPATH_EVERY = 0.5f;  // how often it asks for a fresh path, in seconds
    const float SNAP = 10f;           // how far off the NavMesh the scooter or its target may be, in metres
    const float RETARGET = 2f;        // a target this far from the path's end (metres) asks for a new path at once
    const int MAX_CORNERS = 64;

    BallDriving driving;
    OrderHandler handler;
    readonly BotStuck stuck = new BotStuck();
    readonly BotChase chase = new BotChase();
    readonly List<BotOption> options = new List<BotOption>();
    readonly List<Component> targets = new List<Component>();     // each option's order, or its victim's OrderHandler
    readonly List<Transform> victimBalls = new List<Transform>(); // a steal's victim's scooter (null for an order)
    readonly List<Vector3> threats = new List<Vector3>();
    Component chosen;                 // the order or victim it's going for, else null: deliver, or nothing to do
    Transform victimBall;
    int victimSeat = -1;
    float chaseLimit;
    float sinceChoice = REPATH_EVERY;
    readonly List<Vector3> corners = new List<Vector3>();
    readonly Vector3[] cornerBuffer = new Vector3[MAX_CORNERS];
    NavMeshPath path;
    int corner;                       // which of corners it's steering at
    float sincePath = REPATH_EVERY;
    Vector3 pathTarget;
    // Looked up once. Agent type IDs can be any int, negative too, so whether one was found is kept apart
    static bool agentLooked, hasAgent;
    static int agentType;

    float steer, accelerate, brake;
    float boostHeld = -1f;

    /// <summary>Whether it boosts at all (tests, and a bot level that never does)</summary>
    internal bool UsesBoost = true;
    float sinceBoost = BOOST_EVERY;

    /// <summary>Online bots: it drives only while a round is on (not in the tutorial, a cutscene or the results)</summary>
    internal bool OnlyInRound;

    public event Action<bool> BoostButton;
#pragma warning disable 67 // a bot neither drifts nor emotes (yet): there for IDriveInput
    public event Action<bool> DriftButton;
    public event Action<Vector2> EmotePad;
#pragma warning restore 67

    float IDriveInput.Steer => steer;
    float IDriveInput.Accelerate => accelerate;
    float IDriveInput.Brake => brake;
    float IDriveInput.CameraX => 0f;
    float IDriveInput.CameraY => 0f;
    bool IDriveInput.LookBehind => false;

    /// <summary>Its personality (online bots: by seat, OnlineGame). The courier, the mildest, until told</summary>
    public BotKind Kind { get; set; } = BotKind.Courier;

    /// <summary>Who it's chasing to steal from now, else null (tests)</summary>
    public OrderHandler Victim { get; private set; }

    /// <summary>Where it's heading now (tests, gizmos); null with nothing to do</summary>
    public Vector3? Target { get; private set; }

    /// <summary>What it steers at: the next corner of its path to Target, or Target itself without a path</summary>
    public Vector3 Aim { get; private set; }

    /// <summary>Whether it has a path on the NavMesh to Target, rather than driving straight at it</summary>
    public bool HasPath => corners.Count > 0;

    /// <summary>Its path, for tests and the log: complete or partial, the corners, which one it's steering at</summary>
    internal string PathReport => HasPath ? path.status + " " + corners.Count + " corners, at " + corner + " " + Aim.ToString("0") : "no path";

    /// <summary>Takes over a scooter</summary>
    public void Drive(BallDriving scooter)
    {
        driving = scooter;
        handler = scooter.GetComponentInParent<OrderHandler>();
        if (handler == null)
            handler = scooter.transform.root.GetComponentInChildren<OrderHandler>();
        scooter.DriveInput = this;
    }

    /// <summary>Hands the scooter back to its pad</summary>
    public void Release()
    {
        if (driving != null && ReferenceEquals(driving.DriveInput, this))
            driving.DriveInput = null;
        driving = null;
    }

    void OnDestroy()
    {
        Release();
    }

    void Update()
    {
        if (driving == null)
            return;

        if (OnlyInRound && !InRound())
        {
            Forget();
            Target = null;
            corners.Clear();
            steer = accelerate = brake = 0f;
            ReleaseBoost(Time.deltaTime);
            return;
        }

        Vector3 position = driving.transform.position;
        float dt = Time.deltaTime;
        Target = PickTarget(position, dt);
        BotProfile profile = BotPersonality.For(Kind);

        if (Target == null)
        {
            corners.Clear();
            steer = accelerate = brake = 0f;
            ReleaseBoost(dt);
            return;
        }

        sincePath += dt;
        if (sincePath >= REPATH_EVERY || (Target.Value - pathTarget).sqrMagnitude > RETARGET * RETARGET)
        {
            sincePath = 0f;
            pathTarget = Target.Value;
            FindPath(position, Target.Value);
        }
        corner = BotBrain.NextCorner(position, corners, corner);
        // A complete path's last corner is the target pulled onto the NavMesh: the order itself is the better aim. A
        // partial path ends where the NavMesh does, nearest the target, so that's where it goes
        bool last = corner >= 0 && corner == corners.Count - 1 && path.status == NavMeshPathStatus.PathComplete;
        Aim = corner < 0 || last ? Target.Value : corners[corner];
        if (profile.Dodges && handler != null && handler.HeldCount > 0)
            Aim = BotTactics.Dodge(position, Aim, BoostingRivals());

        Vector3 toAim = Aim - position;
        float bearing = BotBrain.Bearing(driving.transform.forward, toAim);
        if (stuck.Tick(position, dt))
        {
            // Backing out: reverse with the wheel the other way
            steer = -BotBrain.Steer(driving.transform.forward, toAim);
            accelerate = 0f;
            brake = 1f;
        }
        else
        {
            steer = BotBrain.Steer(driving.transform.forward, toAim);
            accelerate = BotBrain.Throttle(bearing);
            brake = 0f;
        }

        // Chasing: a boost into the victim when close and lined up, whenever it can (the game's boost rules still apply)
        bool hit = false;
        if (victimBall != null)
        {
            Vector3 toVictim = victimBall.position - position;
            hit = BotTactics.BoostToHit(BotBrain.Bearing(driving.transform.forward, toVictim), new Vector2(toVictim.x, toVictim.z).magnitude);
        }
        sinceBoost += dt;
        if (UsesBoost && boostHeld < 0f && !stuck.Reversing
            && (hit || (sinceBoost >= profile.BoostEvery && BotBrain.Boosts(bearing, new Vector2(toAim.x, toAim.z).magnitude))))
        {
            boostHeld = 0f;
            sinceBoost = 0f;
            BoostButton?.Invoke(true);
        }
        ReleaseBoost(dt);
    }

    static bool InRound()
    {
        GameManager game = GameManager.Instance;
        if (game == null)
            return false;
        GameState state = game.MainState;
        return state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage;
    }

    void ReleaseBoost(float dt)
    {
        if (boostHeld < 0f)
            return;
        boostHeld += dt;
        if (boostHeld >= BOOST_HOLD)
        {
            boostHeld = -1f;
            BoostButton?.Invoke(false);
        }
    }

    // A path on the Scooter NavMesh from here to the target, into corners (left empty without one)
    void FindPath(Vector3 from, Vector3 to)
    {
        corners.Clear();
        corner = 1;
        if (!ScooterAgent(out int agent))
            return;
        NavMeshQueryFilter filter = new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, SNAP, filter) || !NavMesh.SamplePosition(to, out NavMeshHit end, SNAP, filter))
            return;
        if (path == null)
            path = new NavMeshPath();
        // A partial path still gets it closer (a target across water, or off the mesh)
        if (!NavMesh.CalculatePath(start.position, end.position, filter, path) || path.status == NavMeshPathStatus.PathInvalid)
            return;
        int count = path.GetCornersNonAlloc(cornerBuffer);
        for (int i = 0; i < count; i++)
            corners.Add(cornerBuffer[i]);
    }

    static bool ScooterAgent(out int id)
    {
        if (!agentLooked)
        {
            agentLooked = true;
            for (int i = 0; i < NavMesh.GetSettingsCount() && !hasAgent; i++)
            {
                agentType = NavMesh.GetSettingsByIndex(i).agentTypeID;
                hasAgent = NavMesh.GetSettingsNameFromID(agentType) == AGENT;
            }
            if (!hasAgent)
                Debug.LogWarning("Bots: no NavMesh agent type called " + AGENT + ", so bots drive straight at their targets");
        }
        id = agentType;
        return hasAgent;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 1; i < corners.Count; i++)
            Gizmos.DrawLine(corners[i - 1], corners[i]);
    }

    // What it goes for, chosen afresh every REPATH_EVERY and at once when that's gone (taken, delivered, dropped) or a
    // chase runs out: a free order, a victim's scooter, else where its best held order goes
    Vector3? PickTarget(Vector3 position, float dt)
    {
        chase.Advance(dt);
        sinceChoice += dt;
        // ReferenceEquals: a destroyed order or player is gone too (Unity's == calls it null)
        if (sinceChoice >= REPATH_EVERY || (!ReferenceEquals(chosen, null) && !StillThere(chosen)))
            Choose(position);
        if (chase.Tick(victimSeat, chaseLimit, dt))
            Choose(position); // the victim is barred for a while now

        if (chosen is Order order)
            return order.transform.position;
        if (victimBall != null)
            return victimBall.position;
        Order held = handler != null ? handler.GetBestOrder() : null;
        if (held != null && held.DropoffPoint != null)
            return held.DropoffPoint.position;
        return null;
    }

    // Whether what it went for is still there to have
    static bool StillThere(Component target)
    {
        if (target == null)
            return false;
        if (target is Order order)
            return FreeToTake(order);
        return ((OrderHandler)target).GetBestOrder() != null;
    }

    static bool FreeToTake(Order order)
    {
        return order != null && order.IsActive && order.CanPickup && order.PlayerHolding == null;
    }

    // Scores every free order and every rival carrying one (BotTactics) and takes the best
    void Choose(Vector3 position)
    {
        sinceChoice = 0f;
        options.Clear();
        targets.Clear();
        victimBalls.Clear();

        int mySeat = OrderSync.SeatOf(handler);
        int myScore = handler != null ? handler.Score : 0;
        int leader = myScore;
        PlayerRoster roster = PlayerInstantiate.Instance != null ? PlayerInstantiate.Instance.Roster : null;
        if (roster != null)
        {
            foreach (PlayerSlot slot in roster.Players)
            {
                OrderHandler other = slot.Player != null ? slot.Player.GetComponentInChildren<OrderHandler>(true) : null;
                if (other != null)
                    leader = Mathf.Max(leader, other.Score);
            }
        }

        OrderManager orders = OrderManager.Instance;
        if (orders != null)
        {
            foreach (Order order in orders.ActiveOrders)
            {
                if (!FreeToTake(order))
                    continue;
                options.Add(new BotOption
                {
                    Kind = BotOptionKind.Order,
                    Worth = order.Worth,
                    Golden = order.Value == Constants.OrderValue.Golden,
                    Distance = Flat(order.transform.position - position)
                });
                targets.Add(order);
                victimBalls.Add(null);
            }
        }

        if (roster != null)
        {
            foreach (PlayerSlot slot in roster.Players)
            {
                if (slot.Index == mySeat || slot.Player == null || chase.Barred(slot.Index))
                    continue;
                OrderHandler other = slot.Player.GetComponentInChildren<OrderHandler>(true);
                BallDriving ball = slot.Player.GetComponentInChildren<BallDriving>(true);
                Order best = other != null ? other.GetBestOrder() : null;
                if (best == null || ball == null || other == handler)
                    continue;
                options.Add(new BotOption
                {
                    Kind = BotOptionKind.Steal,
                    Worth = best.Worth,
                    Golden = best.Value == Constants.OrderValue.Golden,
                    Distance = Flat(ball.transform.position - position),
                    VictimLeads = other.Score >= leader,
                    VictimBoosting = other.IsBoosting
                });
                targets.Add(other);
                victimBalls.Add(ball.transform);
            }
        }

        BotSituation situation = new BotSituation
        {
            Profile = BotPersonality.For(Kind),
            MyScore = myScore,
            LeaderScore = leader,
            Held = handler != null ? handler.HeldCount : 0,
            StealsOn = HouseRules.Playing.Steals
        };
        int pick = BotTactics.Choose(situation, options, chosen != null ? targets.IndexOf(chosen) : -1);
        chosen = pick >= 0 ? targets[pick] : null;
        victimBall = pick >= 0 ? victimBalls[pick] : null;
        Victim = chosen as OrderHandler;
        victimSeat = Victim != null ? OrderSync.SeatOf(Victim) : -1;
        chaseLimit = BotTactics.ChaseSeconds(situation);
    }

    // Nothing chosen, nobody chased (out of the round)
    void Forget()
    {
        chosen = null;
        victimBall = null;
        Victim = null;
        victimSeat = -1;
        sinceChoice = REPATH_EVERY;
    }

    // Where the rivals boosting now are (the courier swerves from them)
    List<Vector3> BoostingRivals()
    {
        threats.Clear();
        if (PlayerInstantiate.Instance == null)
            return threats;
        foreach (PlayerSlot slot in PlayerInstantiate.Instance.Roster.Players)
        {
            if (slot.Player == null)
                continue;
            OrderHandler other = slot.Player.GetComponentInChildren<OrderHandler>(true);
            BallDriving ball = slot.Player.GetComponentInChildren<BallDriving>(true);
            if (other != null && other != handler && ball != null && other.IsBoosting)
                threats.Add(ball.transform.position);
        }
        return threats;
    }

    static float Flat(Vector3 v)
    {
        return new Vector2(v.x, v.z).magnitude;
    }
}
