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
    const float ROAD_REACH = 40f;     // how far it looks for a road when it has no path, in metres
    const float WATER_LOOK = 1f;      // how far ahead it watches for a road ending in water, in seconds of travel
    const float WALL_LOOK = 0.35f;    // and in a wall, where a mistake only costs a bump
    const float WATER_SPEED = 12f;    // what it slows to near each (m/s)
    const float WALL_SPEED = 20f;
    const float SHORE_CLEAR = 8f;     // a spot to run to this far from a road's edge is clear of whatever's past it, in metres
    const float CORNER_CLOSE = 6f;    // this near where it's running to, it looks again at once, in metres
    static readonly float[] FLEE_TURNS = { 0f, 45f, -45f, 90f, -90f, 135f, -135f }; // degrees off straight away
    const float DROP_NEAR = 3f, DROP_FAR = 8f; // how far past the edge it looks for water, in metres
    const float DROP_ABOVE = 1.5f, DROP_DEPTH = 3f; // ground counts from this far above the road's level to this far below
    const float EDGE_SNAP = 3f;       // this close to the NavMesh it counts as on it, in metres
    const float EDGE_MIN_SPEED = 8f;  // slower than this (m/s) it can't run off
    const float EDGE_BACK_IN = 8f;    // how far along and in from an edge it heads, in metres
    const int GIVE_UP_AFTER = 2;      // stuck this many times running, it chooses again
    const float ROAM_NEAR = 15f;      // with nothing to do, a rival this close is close enough
    const float OVERRUN = 15f;        // road ending this far past its target counts as an edge to stop short of, in metres
    const float TARGET_TURN = 120f;   // it brakes for such a target as for a corner this sharp
    const float ORDER_BARRED = 20f;   // how long it leaves an order alone after failing to reach it, in seconds
    const float PARTIAL_GIVE_UP = 10f; // a path that stops short of its order, this close to the stop (metres): it can't be got at
    const float DODGE_MOST = 6f;      // the most the courier's swerve moves its aim, in metres (more leaves the road)

    BallDriving driving;
    Rigidbody ballBody;
    readonly Dictionary<Order, float> barredOrders = new Dictionary<Order, float>(); // until when
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
    int chosenWorth;                  // what it chose was worth, and how far away it was (BotStats)
    float chosenDistance;
    int heldBefore;
    bool inRound;
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

    /// <summary>How this round has gone (online bots log it as the round ends)</summary>
    internal BotStats Stats { get; private set; } = new BotStats();

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
        FeatSync.Sank -= OnFell;
        FeatSync.Sank += OnFell;
    }

    /// <summary>Hands the scooter back to its pad</summary>
    public void Release()
    {
        if (driving != null && ReferenceEquals(driving.DriveInput, this))
            driving.DriveInput = null;
        driving = null;
        FeatSync.Sank -= OnFell;
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
            if (inRound)
            {
                // The round is over: how it played, for the log
                Debug.Log(Stats.Line(Kind, OrderSync.SeatOf(handler)));
                Stats = new BotStats();
                inRound = false;
            }
            Forget();
            Target = null;
            corners.Clear();
            steer = accelerate = brake = 0f;
            ReleaseBoost(Time.deltaTime);
            return;
        }
        // Steam's features reset FeatSync as they start: it listens again each round
        if (!inRound)
        {
            FeatSync.Sank -= OnFell;
            FeatSync.Sank += OnFell;
        }
        inRound = true;

        // Something new in its hold: a steal when it was chasing, else a pickup (before choosing again forgets which)
        int held = handler != null ? handler.HeldCount : 0;
        if (held > heldBefore)
        {
            if (Victim != null)
                Stats.Stole();
            else
                Stats.PickedUp(chosenWorth, chosenDistance);
        }
        heldBefore = held;

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
        // A corner counts as reached from a few metres off, and paths hug the road's edge. If the straight line on to the
        // next one leaves the road (off a bridge, over the water), it goes on to the one it nearly reached first
        if (corner > 1 && ScooterAgent(out int roadAgent) && NavMesh.SamplePosition(position, out NavMeshHit onRoad, EDGE_SNAP, Filter(roadAgent))
            && NavMesh.Raycast(onRoad.position, corners[corner], out NavMeshHit _, Filter(roadAgent)))
            corner--;
        // A complete path's last corner is the target pulled onto the NavMesh: the order itself is the better aim. A
        // partial path ends where the NavMesh does, nearest the target, so that's where it goes
        // A rival or a place to run to isn't: straight at it can mean straight over the water
        bool last = corner >= 0 && corner == corners.Count - 1 && path.status == NavMeshPathStatus.PathComplete
            && victimBall == null && ModeMatch.Current == null;
        Aim = corner < 0 || last ? Target.Value : corners[corner];
        if (profile.Dodges && handler != null && handler.HeldCount > 0)
            Aim += Vector3.ClampMagnitude(BotTactics.Dodge(position, Aim, BoostingRivals()) - Aim, DODGE_MOST);

        // Off the road with no path (thrown clear, a respawn): back to the nearest road before anything else
        // A path stopping short of its order, and it's nearly at the stop: over water, or off the road
        // The golden order is all there is to go for in its round: straight at it from there
        if (chosen is Order partly && corners.Count > 0 && path.status == NavMeshPathStatus.PathPartial
            && Flat(corners[corners.Count - 1] - position) < PARTIAL_GIVE_UP)
        {
            if (partly.Value == Constants.OrderValue.Golden)
                Aim = Target.Value;
            else
                GiveUp();
        }

        Vector3 velocity = Velocity();
        float speed = new Vector2(velocity.x, velocity.z).magnitude;
        if (corner < 0 && ToRoad(position, out Vector3 road))
            Aim = road;

        // About to leave the road (water, a wall): off the pedals and turning back on to it
        bool edgeAhead = EdgeAhead(position, velocity, speed, out Vector3 awayFromEdge, out bool water);
        if (edgeAhead)
            Aim = awayFromEdge;

        Vector3 toAim = Aim - position;
        float bearing = BotBrain.Bearing(driving.transform.forward, toAim);
        if (stuck.Tick(position, dt))
        {
            // Backing out: reverse with the wheel the other way
            steer = -BotBrain.Steer(driving.transform.forward, toAim);
            accelerate = 0f;
            brake = 1f;
            // Still stuck after backing out: what it went for can't be got at from here, for now
            if (stuck.Repeats >= GIVE_UP_AFTER)
                GiveUp();
        }
        else
        {
            steer = BotBrain.Steer(driving.transform.forward, toAim);
            BotBrain.Pedals(speed, edgeAhead ? (water ? WATER_SPEED : WALL_SPEED) : CornerLimit(position), bearing, out accelerate, out brake);
        }

        // Chasing: a boost into the victim when close and lined up, whenever it can (the game's boost rules still apply)
        bool hit = false;
        if (victimBall != null)
        {
            Vector3 toVictim = victimBall.position - position;
            hit = BotTactics.BoostToHit(BotBrain.Bearing(driving.transform.forward, toVictim), new Vector2(toVictim.x, toVictim.z).magnitude, driving.Boosting);
        }
        sinceBoost += dt;
        if (UsesBoost && boostHeld < 0f && !stuck.Reversing && !(edgeAhead && water)
            && (hit || (sinceBoost >= profile.BoostEvery && BotBrain.Boosts(bearing, StraightAhead(position, toAim)))))
        {
            boostHeld = 0f;
            sinceBoost = 0f;
            if (hit)
                Stats.Tried();
            BoostButton?.Invoke(true);
        }
        ReleaseBoost(dt);
    }

    Vector3 Velocity()
    {
        if (ballBody == null && driving.Sphere != null)
            ballBody = driving.Sphere.GetComponent<Rigidbody>();
        return ballBody != null ? ballBody.velocity : Vector3.zero;
    }

    NavMeshQueryFilter Filter(int agent) => new NavMeshQueryFilter { agentTypeID = agent, areaMask = NavMesh.AllAreas };

    // The nearest road when it's off the NavMesh, within reach of a thrown scooter. False on the road, or with none near
    bool ToRoad(Vector3 position, out Vector3 road)
    {
        road = position;
        if (!ScooterAgent(out int agent) || !NavMesh.SamplePosition(position, out NavMeshHit near, ROAD_REACH, Filter(agent)))
            return false;
        road = near.position;
        return near.distance > EDGE_SNAP; // on the road already: it keeps its aim
    }

    // Whether where it's going soon leaves the NavMesh: then a point back in from the edge, on its way to what it aims at.
    // Water is watched for from far off; a wall only costs a bump, so it keeps its speed until close
    bool EdgeAhead(Vector3 position, Vector3 velocity, float speed, out Vector3 away, out bool water)
    {
        away = position;
        water = false;
        if (speed < EDGE_MIN_SPEED || !ScooterAgent(out int agent))
            return false;
        NavMeshQueryFilter filter = Filter(agent);
        if (!NavMesh.SamplePosition(position, out NavMeshHit here, EDGE_SNAP, filter))
            return false;

        Vector3 ahead = here.position + new Vector3(velocity.x, 0f, velocity.z) * WATER_LOOK;
        if (!NavMesh.Raycast(here.position, ahead, out NavMeshHit edge, filter))
            return false;
        water = WaterPast(edge);
        if (!water && edge.distance > speed * WALL_LOOK)
            return false;

        // Already steering clear of it: the path handles this
        Vector3 toAim = Aim - position;
        toAim.y = 0f;
        if (Vector3.Dot(toAim, edge.normal) > 0f && Vector3.Dot(toAim.normalized, new Vector3(velocity.x, 0f, velocity.z).normalized) < 0.7f)
            return false;

        // Along the edge towards the aim, and in from it
        Vector3 along = Vector3.Cross(edge.normal, Vector3.up);
        if (Vector3.Dot(along, toAim) < 0f)
            along = -along;
        away = edge.position + edge.normal * EDGE_BACK_IN + along * EDGE_BACK_IN;
        return true;
    }

    // Whether the road's edge here gives on to water or a drop: no ground to drive on just past it, by the scooter's own
    // idea of ground (BallDriving.GroundCheck). A wall stands on ground, so it isn't one
    static bool WaterPast(NavMeshHit edge)
    {
        return NoGround(edge.position - edge.normal * DROP_NEAR) || NoGround(edge.position - edge.normal * DROP_FAR);
    }

    static bool NoGround(Vector3 spot)
    {
        const int GROUND = (1 << 0) | (1 << 9) | (1 << 29);
        return !Physics.Raycast(spot + Vector3.up * DROP_ABOVE, Vector3.down, out RaycastHit _, DROP_ABOVE + DROP_DEPTH, GROUND, QueryTriggerInteraction.Ignore);
    }

    // How fast it should go for the next corner of its path
    float CornerLimit(Vector3 position)
    {
        if (corner < 0 || corner + 1 >= corners.Count)
            return TargetLimit(position);
        Vector3 toCorner = corners[corner] - position;
        Vector3 afterwards = corners[corner + 1] - corners[corner];
        return BotBrain.SpeedFor(new Vector2(toCorner.x, toCorner.z).magnitude, BotBrain.Bearing(toCorner, afterwards));
    }

    // How fast it should go for its target: flat out, unless the road ends just past it (an order on a river bank), where
    // it comes in slowly enough to stop rather than sailing over it into the water
    float TargetLimit(Vector3 position)
    {
        if (Target == null || !ScooterAgent(out int agent))
            return float.PositiveInfinity;
        Vector3 toTarget = Target.Value - position;
        toTarget.y = 0f;
        float distance = toTarget.magnitude;
        if (distance < 0.5f)
            return float.PositiveInfinity;
        NavMeshQueryFilter filter = Filter(agent);
        if (!NavMesh.SamplePosition(Target.Value, out NavMeshHit at, SNAP, filter))
            return float.PositiveInfinity;
        Vector3 past = at.position + toTarget / distance * OVERRUN;
        return NavMesh.Raycast(at.position, past, out NavMeshHit _, filter) ? BotBrain.SpeedFor(distance, TARGET_TURN) : float.PositiveInfinity;
    }

    // It fell going for something: that's out of bounds for a while, or it tries the same way again and again
    void OnFell(int seat)
    {
        if (handler != null && seat == OrderSync.SeatOf(handler))
            GiveUp();
    }

    // What it went for can't be got at from here, for now
    void GiveUp()
    {
        if (chosen is Order order)
        {
            if (order.Value != Constants.OrderValue.Golden)
                barredOrders[order] = Time.time + ORDER_BARRED;
        }
        else if (victimSeat >= 0)
            chase.Bar(victimSeat);
        Forget();
    }

    bool Barred(Order order) => barredOrders.TryGetValue(order, out float until) && Time.time < until;

    // How far it can run straight on from here: to the aim, on past a corner gentle enough to keep going, and never beyond
    // where the NavMesh ends the way it's facing
    float StraightAhead(Vector3 position, Vector3 toAim)
    {
        float straight = new Vector2(toAim.x, toAim.z).magnitude;
        if (corner >= 0 && corner + 1 < corners.Count)
        {
            Vector3 afterwards = corners[corner + 1] - corners[corner];
            if (Mathf.Abs(BotBrain.Bearing(toAim, afterwards)) <= BotBrain.GENTLE_TURN)
                straight += new Vector2(afterwards.x, afterwards.z).magnitude;
        }
        if (!ScooterAgent(out int agent))
            return straight;
        NavMeshQueryFilter filter = Filter(agent);
        if (!NavMesh.SamplePosition(position, out NavMeshHit here, EDGE_SNAP, filter))
            return 0f;
        Vector3 facing = driving.transform.forward;
        facing.y = 0f;
        Vector3 boostEnd = here.position + facing.normalized * BotBrain.BOOST_DISTANCE;
        if (NavMesh.Raycast(here.position, boostEnd, out NavMeshHit edge, filter))
            straight = Mathf.Min(straight, Vector3.Distance(here.position, edge.position));
        return straight;
    }

    static bool InRound()
    {
        GameManager game = GameManager.Instance;
        return game != null && InRound(game.MainState, Time.time, PlayerInstantiate.DrivingFrom);
    }

    /// <summary>
    /// Whether a bot drives: in the round's states, and once the players' 3, 2, 1 has handed them their controls (the
    /// golden round's start). Bots set off before it ended (playtest, 2026-10-31)
    /// </summary>
    internal static bool InRound(GameState state, float now, float drivingFrom)
    {
        return (state == GameState.Begin || state == GameState.MainLoop || state == GameState.FinalPackage) && now >= drivingFrom;
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
        // Near it first; a target further off the road (a door set back, an order on a verge) still gets the nearest road to it
        if (!NavMesh.SamplePosition(from, out NavMeshHit start, ROAD_REACH, filter)
            || (!NavMesh.SamplePosition(to, out NavMeshHit end, SNAP, filter) && !NavMesh.SamplePosition(to, out end, ROAD_REACH, filter)))
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
        // Kings Crown, Infection, Hot Potato: no orders, the mode's own goal (BotModeTactics)
        if (ModeMatch.Current != null)
            return ModeTarget(position);

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
        return Roam(position);
    }

    // Nothing to go for (every order taken, none due yet): it keeps moving, towards the nearest rival, so it's in the thick
    // of it when the next order appears rather than parked at the last drop-off
    Vector3? Roam(Vector3 position)
    {
        PlayerRoster roster = PlayerInstantiate.Instance != null ? PlayerInstantiate.Instance.Roster : null;
        if (roster == null || handler == null)
            return null;
        Vector3? nearest = null;
        float best = float.MaxValue;
        foreach (PlayerSlot slot in roster.Players)
        {
            BallDriving ball = slot.Player != null ? slot.Player.GetComponentInChildren<BallDriving>(true) : null;
            if (ball == null || ball == driving)
                continue;
            float distance = Flat(ball.transform.position - position);
            if (distance > ROAM_NEAR && distance < best)
            {
                best = distance;
                nearest = ball.transform.position;
            }
        }
        return nearest;
    }

    // A timed mode's goal: a seat to chase and boost into, or a place to drive to
    Vector3? ModeTarget(Vector3 position)
    {
        chosen = null;
        Victim = null;
        ModeGoal goal = BotModeTactics.Choose(ModeMatch.Current, OrderSync.SeatOf(handler), position, SeatBallPosition);
        victimSeat = goal.Chase;
        victimBall = SeatBall(goal.Chase);
        if (victimBall != null)
            return victimBall.position;
        return goal.Place != null ? RoadTowards(position, goal.Place.Value) : goal.Place;
    }

    Vector3 fleeTo;
    float nextFleeLook;
    bool fleeing;

    // Running from someone picks a spot without looking, and straight away from a chaser ends at the coast: the road nearest
    // it, or, when that's beside the water, the nearest way round that isn't. Crown spots on the road come out as they went in
    Vector3 RoadTowards(Vector3 position, Vector3 place)
    {
        if (!ScooterAgent(out int agent))
            return place;
        if (fleeing && Time.time < nextFleeLook && (fleeTo - position).sqrMagnitude > CORNER_CLOSE * CORNER_CLOSE)
            return fleeTo;
        nextFleeLook = Time.time + REPATH_EVERY;
        fleeing = true;
        NavMeshQueryFilter filter = Filter(agent);
        Vector3 away = place - position;
        Vector3 first = place;
        bool found = false;
        foreach (float turn in FLEE_TURNS)
        {
            Vector3 spot = position + Quaternion.Euler(0f, turn, 0f) * away;
            if (!NavMesh.SamplePosition(spot, out NavMeshHit road, turn == 0f ? ROAD_REACH : SNAP, filter))
                continue;
            if (!found)
            {
                first = road.position;
                found = true;
            }
            if (!NavMesh.FindClosestEdge(road.position, out NavMeshHit edge, filter) || edge.distance >= SHORE_CLEAR || !WaterPast(edge))
                return fleeTo = road.position;
        }
        return fleeTo = first;
    }

    static Transform SeatBall(int seat)
    {
        OrderHandler other = OrderSync.HandlerIn(seat);
        BallDriving ball = other != null ? other.GetComponent<BallDriving>() : null;
        return ball != null ? ball.transform : null;
    }

    static Vector3? SeatBallPosition(int seat)
    {
        Transform ball = SeatBall(seat);
        return ball != null ? ball.position : (Vector3?)null;
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
                if (!FreeToTake(order) || Barred(order))
                    continue;
                options.Add(new BotOption
                {
                    Kind = BotOptionKind.Order,
                    Worth = order.Worth,
                    Golden = order.Value == Constants.OrderValue.Golden || order.IsLode,
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
                    Golden = best.Value == Constants.OrderValue.Golden || best.IsLode,
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
            StealsOn = HouseRules.Playing.StealsOn
        };
        int pick = BotTactics.Choose(situation, options, chosen != null ? targets.IndexOf(chosen) : -1);
        chosen = pick >= 0 ? targets[pick] : null;
        victimBall = pick >= 0 ? victimBalls[pick] : null;
        chosenWorth = pick >= 0 ? options[pick].Worth : 0;
        chosenDistance = pick >= 0 ? options[pick].Distance : 0f;
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
