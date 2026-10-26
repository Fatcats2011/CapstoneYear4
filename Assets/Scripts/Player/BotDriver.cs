using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// A CPU driver for one scooter (bots phase G1): it drives as a pad would (IDriveInput), towards the nearest order free
/// to take, then to where its order goes (BotBrain), along a path on the Scooter NavMesh (straight at it without one),
/// boosting when lined up on a far corner and backing out when stuck.
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
    readonly List<Vector3> spots = new List<Vector3>();
    readonly List<Order> free = new List<Order>();
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

        Vector3 position = driving.transform.position;
        Target = PickTarget(position);
        float dt = Time.deltaTime;

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

        sinceBoost += dt;
        if (UsesBoost && boostHeld < 0f && sinceBoost >= BOOST_EVERY && !stuck.Reversing
            && BotBrain.Boosts(bearing, new Vector2(toAim.x, toAim.z).magnitude))
        {
            boostHeld = 0f;
            sinceBoost = 0f;
            BoostButton?.Invoke(true);
        }
        ReleaseBoost(dt);
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

    // Where its order goes, else the nearest order nobody has
    Vector3? PickTarget(Vector3 position)
    {
        Order held = handler != null ? handler.GetBestOrder() : null;
        if (held != null && held.DropoffPoint != null)
            return held.DropoffPoint.position;

        OrderManager orders = OrderManager.Instance;
        if (orders == null)
            return null;
        spots.Clear();
        free.Clear();
        foreach (Order order in orders.ActiveOrders)
        {
            if (order != null && order.IsActive && order.CanPickup && order.PlayerHolding == null)
            {
                free.Add(order);
                spots.Add(order.transform.position);
            }
        }
        int nearest = BotBrain.Nearest(position, spots);
        return nearest >= 0 ? spots[nearest] : (Vector3?)null;
    }
}
