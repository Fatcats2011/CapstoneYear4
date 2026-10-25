using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A CPU driver for one scooter (bots phase G1): it drives as a pad would (IDriveInput), towards the nearest order free
/// to take, then to where its order goes (BotBrain), boosting when lined up on a far target and backing out when stuck.
/// Set it as the scooter's BallDriving.DriveInput. Only where the rules run: a client's scooters follow the host's.
/// Developers: F7 hands Player 1's scooter to a bot and back (BotAutopilot)
/// </summary>
public class BotDriver : MonoBehaviour, IDriveInput
{
    const float BOOST_HOLD = 0.2f;    // how long it holds boost, in seconds
    const float BOOST_EVERY = 2.5f;   // the least time between boosts

    BallDriving driving;
    OrderHandler handler;
    readonly BotStuck stuck = new BotStuck();
    readonly List<Vector3> spots = new List<Vector3>();
    readonly List<Order> free = new List<Order>();

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
            steer = accelerate = brake = 0f;
            ReleaseBoost(dt);
            return;
        }

        Vector3 toTarget = Target.Value - position;
        float bearing = BotBrain.Bearing(driving.transform.forward, toTarget);
        if (stuck.Tick(position, dt))
        {
            // Backing out: reverse with the wheel the other way
            steer = -BotBrain.Steer(driving.transform.forward, toTarget);
            accelerate = 0f;
            brake = 1f;
        }
        else
        {
            steer = BotBrain.Steer(driving.transform.forward, toTarget);
            accelerate = BotBrain.Throttle(bearing);
            brake = 0f;
        }

        sinceBoost += dt;
        if (UsesBoost && boostHeld < 0f && sinceBoost >= BOOST_EVERY && !stuck.Reversing
            && BotBrain.Boosts(bearing, new Vector2(toTarget.x, toTarget.z).magnitude))
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
