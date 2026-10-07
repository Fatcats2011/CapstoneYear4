using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Used for the cardboard cutout in the tutorial. A player who boosts into it steals its order, and the barrier behind it
/// opens. Online the host hands out the order: a client's own player opens its barrier at once and asks the host
/// (TutorialSync), and every machine opens a cutout once its order is taken. See docs/online.md
/// </summary>
public class CutoutHandler : MonoBehaviour
{
    [Tooltip("Model of the cutout for spinning and whatnot.")]
    [SerializeField] private Transform cutoutModel;

    [Tooltip("Order the cutout will be holding.")]
    [SerializeField] private Order order;

    [Tooltip("Position the order will be.")]
    [SerializeField] private Transform orderPos;

    [Tooltip("GO to block the player from advancing until they've stolen the order.")]
    [SerializeField] private GameObject barrier;

    [Tooltip("Time it takes for the barrier to dissolve.")]
    [SerializeField] private float dissolveTime = 0.5f;

    private Dissolver barrierDissolver;
    private BoxCollider barrierCollider;
    //private BoxCollider cutoutMeshCollider;

    private bool hasStolen = false;

    // The cutout in each seat's lane this match (CutoutManager sets them up)
    private static readonly CutoutHandler[] bySeat = new CutoutHandler[Constants.MAX_PLAYERS];

    /// <summary>The seat whose lane this cutout is in (-1 until it's set up)</summary>
    public int Seat { get; private set; } = -1;

    /// <summary>Whether it's been stolen from: its barrier is down</summary>
    public bool IsOpen { get { return hasStolen; } }

    /// <summary>
    /// The cutout in a seat's lane this match, or null when there's none
    /// </summary>
    public static CutoutHandler InSeat(int seat)
    {
        if (seat < 0 || seat >= bySeat.Length || bySeat[seat] == null)
            return null;

        return bySeat[seat];
    }

    /// <summary>
    /// Sets the cutout up for the player in a seat: it holds its order, and its barrier blocks the lane
    /// </summary>
    public void InitCutout(int seat)
    {
        Seat = seat;
        if (seat >= 0 && seat < bySeat.Length)
            bySeat[seat] = this;

        order.CardboardHold();
        order.transform.position = orderPos.position;

        barrierDissolver = barrier.GetComponent<Dissolver>();
        barrierCollider = barrier.GetComponent<BoxCollider>();
        //cutoutMeshCollider = cutoutModel.GetComponent<BoxCollider>();

        barrierCollider.enabled = true;
    }

    private void Update()
    {
        // Online, the host decides who steals: once its decision reaches this machine, the cutout opens here too
        if (Seat >= 0 && !hasStolen && order.PlayerHolding != null)
            Open(order.PlayerHolding);
    }

    /// <summary>
    /// The player steals the cutout's order, and the cutout opens. Nothing once it's open. Online, only the host decides
    /// (OrderSync): a client asks it
    /// </summary>
    public void StealFor(OrderHandler player)
    {
        if (hasStolen || player == null)
            return;

        order.StealActive = false;
        order.InitOrder(false);
        player.AddOrder(order);
        Open(player);
    }

    // The steal shows: the barrier dissolves, the cutout spins, and the thief hears it
    private void Open(OrderHandler player)
    {
        hasStolen = true;
        barrierCollider.enabled = false;
        barrierDissolver.DissolveOut(dissolveTime);
        SpinCutout(1f);

        SoundPool sounds = player != null ? player.GetComponent<SoundPool>() : null;
        if (sounds != null)
            sounds.PlayOrderTheft();
    }

    /// <summary>
    /// Gives a spin animation with DOTween to simulate being stolen from by a player.
    /// </summary>
    private void SpinCutout(float tweenTime)
    {
        //cutoutMeshCollider.enabled = false;
        Tween spinning = cutoutModel.DORotate(new Vector3(cutoutModel.rotation.x, 360, cutoutModel.rotation.z), tweenTime, RotateMode.LocalAxisAdd);
        spinning.SetEase(Ease.OutBack); //an easing function which dictates a steep climb, slight overshoot, then gradual correction
        //spinning.onComplete += () => cutoutMeshCollider.enabled = true;
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasStolen)
            return;

        if (!FindScooter(other, out OrderHandler player, out BallDriving playerBall))
            return;

        // Another machine's scooter: its own machine sees its boost and asks the host
        if (RemoteAvatar.IsRemote(player) || !playerBall.Boosting)
            return;

        if (OrderSync.MayChange)
        {
            StealFor(player);
        }
        else
        {
            // Online client: the barrier opens here at once (no bumping into it while the host answers), and the
            // host hands out the order
            Open(player);
            TutorialSync.AskCutout(Seat);
        }
    }

    // What each collider that has touched the cutout belongs to: a scooter's order handler and ball driving, or neither
    private struct Scooter
    {
        public OrderHandler Player;
        public BallDriving Ball;
        public bool IsScooter; // it has a ball driving (the cutout only reacts to those)
    }

    private static readonly Dictionary<Collider, Scooter> scooters = new Dictionary<Collider, Scooter>();

    /// <summary>
    /// Finds the scooter a collider belongs to (its parent's order handler and ball driving). It looks each collider up
    /// once: one that isn't a scooter's is remembered as such
    /// </summary>
    private static bool FindScooter(Collider other, out OrderHandler player, out BallDriving playerBall)
    {
        // A known scooter whose parts are gone is looked up again
        if (scooters.TryGetValue(other, out Scooter known) && (!known.IsScooter || known.Ball != null))
        {
            player = known.Player;
            playerBall = known.Ball;
            return known.IsScooter;
        }

        // Scenes come and go: don't hold on to the colliders of old ones
        if (scooters.Count > 256)
            scooters.Clear();

        Transform parent = other.transform.parent;
        known = new Scooter();
        if (parent != null)
        {
            known.Player = parent.GetComponentInChildren<OrderHandler>();
            known.Ball = parent.GetComponentInChildren<BallDriving>();
        }

        known.IsScooter = known.Ball != null;
        scooters[other] = known;
        player = known.Player;
        playerBall = known.Ball;
        return known.IsScooter;
    }
}
