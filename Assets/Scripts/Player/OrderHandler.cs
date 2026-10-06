using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using UnityEngine;

/// <summary>
/// This class is meant to be attached to every player and will handle picking up, dropping, stealing, and delivering orders in the game, as well as player score.
/// </summary>
public class OrderHandler : MonoBehaviour
{
    private int score; // score of the player
    public int Score { get { return score; } set { score = value; } }
    private int placement = 0;
    public int Placement { get { return placement; }set { placement = value; } }
    [SerializeField] NumberHandler numberHandler;    

    private Order order1 = null; // first order the player is holding
    private Order order2 = null; // second order the player is holding

    [SerializeField] DrivingIndicators drivingIndicators; // Reference to driving indicator script on player

    private bool canTakeOrder;
    public bool CanTakeOrder { get { return canTakeOrder; } }

    [Tooltip("Positions the orders will snap to on the back of the scooter.")]
    [SerializeField] private Transform order1Position;
    [SerializeField] private Transform order2Position;
    [Tooltip("Reference to the ball for event subscription.")]
    [SerializeField] private BallDriving ball;
    private TutorialHandler tutHandler;
    public bool IsBoosting { get { return ball.Boosting; } }
    private bool hasGoldenOrder;
    public bool HasGoldenOrder { get { return hasGoldenOrder; } set { hasGoldenOrder = value; } }
    private bool hasOrder = false;
    public bool HasOrder => hasOrder;

    private SoundPool soundPool;

    public delegate void GotHitDelegate();
    public event GotHitDelegate GotHit;

    public delegate void ClashDelegate(OrderHandler other);
    public event ClashDelegate Clash;

    // if the player is in another player's collider but nobody's boosting
    private OrderHandler playerTouching = null;
    public OrderHandler PlayerTouching { get { return playerTouching; } set {  playerTouching = value; } }

    private CompanyInformation companyInfo;
    public CompanyInformation CompanyInfo { get {  return companyInfo; } set {  companyInfo = value; } }

    [SerializeField] Animator playerAnimator;
    public Animator PlayerAnimator { get { return playerAnimator; } } // the scooter's animator (the results screen plays placements on it)

    private ISceneFlow sceneFlow; // the scene flow this handler listens to while enabled

    private void Start()
    {
        /*score = 0; // init score to 0
        numberHandler.UpdateScoreUI(score.ToString());*/

        ScoreManager.Instance.AddOrderHandler(this);
        ball = transform.parent.GetComponentInChildren<BallDriving>();
        soundPool = GetComponent<SoundPool>();
        tutHandler = GetComponent<TutorialHandler>();

        SetDrivingIndicators();
    }

    private void OnEnable()
    {
        ball.OnBoostStart += AttemptSteal;
        sceneFlow = SceneFlow.Current;
        sceneFlow.OnReturnToMenu += ResetHandler;
        GameManager.Instance.OnSwapAnything += UpdateScore;
        GameManager.Instance.OnSwapStartingCutscene += InitHandler;
    }

    private void OnDisable()
    {
        ball.OnBoostStart -= AttemptSteal;
        sceneFlow.OnReturnToMenu -= ResetHandler;
        GameManager.Instance.OnSwapAnything -= UpdateScore;
        GameManager.Instance.OnSwapStartingCutscene -= InitHandler;
    }

    private void Update()
    {
        if (DevTools.GetKeyDown(KeyCode.T))
            DropEverything(order1Position.position, order2Position.position);

        canTakeOrder = order1 == null || order2 == null;
    }

    /// <summary>
    /// This method checks if the player can carry an order and adds it to their bike if they can.
    /// </summary>
    /// <param name="inOrder">Order the player is trying to pick up</param>
    public void AddOrder(Order inOrder)
    {
        // Online, the host decides pickups: a client shows the host's
        if (!OrderSync.MayChange)
            return;

        if (inOrder == order1 || inOrder == order2)
            return;

        // A client replaying the host's pickup takes it: the host already checked the cooldown
        if (OrderSync.Showing || inOrder.CanPickup || inOrder.PlayerDropped != this)
        {
            // will add order if it fits, elsewise will not do anything
            if (order1 == null || order2 == null)
            {
                using OrderSync.Scope change = OrderSync.Change(OrderChange.Pickup(inOrder.Key, OrderSync.SeatOf(this)));

                soundPool.PlayOrderPickup();
                inOrder.Pickup(this);
                hasOrder = true;
                
                // Attach to order 2 position
                if (order2 == null)
                {
                    order2 = inOrder;
                    order2.transform.parent = order2Position;
                    order2.SetMeshPosition(order2Position.position);
                }
                // Attach to order 1 position
                else
                {
                    order1 = inOrder;
                    order1.transform.parent = order1Position;
                    order1.SetMeshPosition(order1Position.position);
                }
            }

            SetDrivingIndicators();
        }
        if (tutHandler != null && tutHandler.HasLearnt)
            ball.SetBoostModifier(hasOrder);
    }

    /// <summary>
    /// This method is called when the player delivers an order to the dropoff location. If the player has the right order it will drop it off and award the player.
    /// </summary>
    /// <param name="rightOrder">The correct order of the dropoff spot</param>
    public void DeliverOrder(Order rightOrder)
    {
        // Online, the host decides deliveries: a client shows the host's
        if (!OrderSync.MayChange)
            return;

        string key = rightOrder.Value == Constants.OrderValue.Golden ? "final_dropoff" : "dropoff";
        if (order1 == rightOrder)
        {
            using OrderSync.Scope change = OrderSync.Change(OrderChange.Deliver(rightOrder.Key, OrderSync.SeatOf(this)));

            soundPool.PlayOrderDropoff(key);
            // Online, only the host changes scores, and counts deliveries for achievements: a client shows the host's
            if (GameAuthority.IsAuthority)
            {
                score += (int)order1.Value;
                FeatSync.Deliver(OrderSync.SeatOf(this), rightOrder.Value == Constants.OrderValue.Golden);
            }
            order1.DeliverOrder();
            order1 = null;
            ScoreManager.Instance.UpdatePlacement();
        }
        else if(order2 == rightOrder)
        {
            using OrderSync.Scope change = OrderSync.Change(OrderChange.Deliver(rightOrder.Key, OrderSync.SeatOf(this)));

            soundPool.PlayOrderDropoff(key);
            if (GameAuthority.IsAuthority)
            {
                score += (int)order2.Value;
                FeatSync.Deliver(OrderSync.SeatOf(this), rightOrder.Value == Constants.OrderValue.Golden);
            }
            order2.DeliverOrder();
            order2 = null;
            ScoreManager.Instance.UpdatePlacement();
        }

        if(order1 == null && order2 == null)
        {
            hasOrder = false;
        }

        SetDrivingIndicators();

        ShowScore();

        if (tutHandler != null && tutHandler.HasLearnt)
            ball.SetBoostModifier(hasOrder);
    }

    /// <summary>
    /// Adds what the golden order earned while it was held, on top of its base value (delivering it already scored that)
    /// </summary>
    /// <param name="finalOrderValue">What the golden order was worth when it was delivered</param>
    public void AwardGoldenBonus(int finalOrderValue)
    {
        // Online, only the host changes scores
        if (!GameAuthority.IsAuthority)
            return;

        score += finalOrderValue - (int)Constants.OrderValue.Golden;
    }

    /// <summary>
    /// This method is for when the player "spins" out. It will drop all the orders the player is currently holding.
    /// Online, a client asks the host, which drops them for everyone
    /// </summary>
    /// <param name="basePos">The position of the order before adding the offset of the specific orderPosition.</param>
    public void DropEverything(Vector3 order1NewPos, Vector3 order2NewPos, bool shouldSpinout = true)
    {
        if (!OrderSync.MayChange)
        {
            OrderSync.AskDrop(this, order1NewPos, order2NewPos, shouldSpinout);
            return;
        }

        DropHeld(order1NewPos, Order.DropHeight(), order2NewPos, Order.DropHeight(), shouldSpinout);
    }

    /// <summary>
    /// Drops what this player holds: each order flies up to its height, then lands on its spot. Online, the host picks
    /// the heights, and every machine replays its drop
    /// </summary>
    public void DropHeld(Vector3 spot1, float height1, Vector3 spot2, float height2, bool spinOut)
    {
        if (!OrderSync.MayChange)
            return;

        using OrderSync.Scope change = OrderSync.Change(OrderChange.Drop(OrderSync.SeatOf(this),
            order1 != null ? order1.Key : OrderBook.NONE, spot1, height1,
            order2 != null ? order2.Key : OrderBook.NONE, spot2, height2, spinOut));

        if (order1 != null)
        {
            order1.Drop(spot1, height1);
            order1 = null;
        }
        if (order2 != null)
        {
            order2.Drop(spot2, height2);
            order2 = null;
        }
        // Another machine's scooter never starts driving here, so nothing listens to it
        if (spinOut) { GotHit?.Invoke(); }

        SetDrivingIndicators();

        hasOrder = false;
        ball.SetBoostModifier(hasOrder);
    }

    /// <summary>
    /// This player is leaving (online: their machine left): their orders go back to the pool. Each first goes back into
    /// its own scene, so it outlives this player's scooter. The golden order goes back to its start, at its starting value:
    /// a player leaving doesn't deliver it. Online, only the host changes them: a client waits for the host's changes
    /// </summary>
    public void ReleaseOrders()
    {
        Order[] held = { order1, order2 };
        foreach (Order order in held)
        {
            if (order == null)
                continue;

            order.ReturnHome();
            if (order.Value == Constants.OrderValue.Golden)
            {
                order.EraseGoldWithoutDelivering();
                order.InitOrder();
            }
            else
                order.EraseOrder();
        }

        order1 = order2 = null;
        hasOrder = false;
    }

    /// <summary>
    /// This method sets the driving indicators on the player based on the best package the player is holding
    /// </summary>
    public void SetDrivingIndicators()
    {
        Constants.OrderValue bestOrderScore = 0;
        try
        {
            bestOrderScore = GetBestOrder().Value;
        }
        catch
        {
            // Sets icon status to holding nothing 
            drivingIndicators.SetRotationSprites(DrivingIndicators.IndicatorStatus.HoldingNothing);
            return;
        }

        switch (bestOrderScore)
        {
            case Constants.OrderValue.Easy:
                drivingIndicators.SetRotationSprites(DrivingIndicators.IndicatorStatus.HoldingEasy);
                break;
            case Constants.OrderValue.Medium:
                drivingIndicators.SetRotationSprites(DrivingIndicators.IndicatorStatus.HoldingMedium);
                break;
            case Constants.OrderValue.Hard:
                drivingIndicators.SetRotationSprites(DrivingIndicators.IndicatorStatus.HoldingHard);
                break;
            case Constants.OrderValue.Golden:
                drivingIndicators.SetRotationSprites(DrivingIndicators.IndicatorStatus.HoldingGolden);
                break;
        }
    }

    /// <summary>
    /// This method returns the order of the highest value the player is holding. If the player isn't holidng anything it returns null.
    /// </summary>
    /// <returns></returns>
    public Order GetBestOrder()
    {
        if(order1 != null && order2 != null)
        {
            if(order2.Value > order1.Value)
            {
                return order2;
            }
            else
            {
                return order1;
            }
        }
        else if(order1 != null)
        {
            return order1;
        }
        else if(order2 != null)
        {
            return order2;
        }
        else
        {
            return null;
        }

    }

    /// <summary>
    /// This method accepts an order as a parameter and checks if the player is holding it. If they are it will remove it from their possession. Doesn't "drop" the order, intended for stealing.
    /// </summary>
    /// <param name="inOrder">Order to be removed</param>
    public void LoseOrder(Order inOrder)
    {
        if(inOrder == order1)
        {
            order1.RemovePlayerHolding();
            order1 = null;
        }
        else if(inOrder == order2)
        {
            order2.RemovePlayerHolding();
            order2 = null;
        }
        if(order1 == null && order2 == null)
        {
            hasOrder = false;
            ball.SetBoostModifier(hasOrder);
        }

        SetDrivingIndicators();
    }

    /// <summary>
    /// This method steals the best order from a victim player. It will then make the victim drop their other order if they have one,
    /// and bounce them away. A local match does this at once; online the host decides it (StealSync)
    /// </summary>
    /// <param name="victimPlayer">Player being stolen from</param>
    private void StealOrder(OrderHandler victimPlayer)
    {
        StealFrom(victimPlayer);
        victimPlayer.BounceFrom(this);
    }

    /// <summary>
    /// This player steals from a victim: the victim's best order when this player has room, then the victim drops the rest
    /// and spins out. Offline and on the host only: online every client replays both order changes, and the victim's own
    /// machine bounces them (OnlineSteals)
    /// </summary>
    /// <param name="victim">Player being stolen from</param>
    public void StealFrom(OrderHandler victim)
    {
        if (!GameAuthority.IsAuthority)
            return;

        Order newOrder = victim.GetBestOrder();
        if (newOrder != null && (order1 == null || order2 == null))
            TakeOrderFrom(victim, newOrder);
        victim.DropEverything(victim.order1Position.position, victim.order2Position.position);

        SetDrivingIndicators();
    }

    /// <summary>
    /// This player takes an order off a victim's scooter (a steal), with the grab and its whoosh. Online the host sends it
    /// as one change, and every client replays it
    /// </summary>
    public void TakeOrderFrom(OrderHandler victim, Order order)
    {
        // Online, the host decides steals: a client shows the host's
        if (!OrderSync.MayChange)
            return;

        using OrderSync.Scope change = OrderSync.Change(OrderChange.Steal(order.Key, OrderSync.SeatOf(this), OrderSync.SeatOf(victim)));

        playerAnimator.SetTrigger(HashReference._stealLeftTrigger);
        soundPool.PlayOrderTheft();
        victim.LoseOrder(order);
        AddOrder(order);
    }

    /// <summary>
    /// This player was stolen from: they bounce away from the thief. Only the machine that drives them does this (online,
    /// when the host says so)
    /// </summary>
    public void BounceFrom(OrderHandler thief)
    {
        BallDriving control = GetComponent<BallDriving>();
        if (control != null) control.BounceOff(thief.transform.position, control.ClashForce * 0.5f);
    }

    /// <summary>
    /// This player clashed with another (both were boosting): they bounce off each other, unless phasing. Only the machine
    /// that drives them does this (online, when the host says so)
    /// </summary>
    public void ClashWith(OrderHandler other)
    {
        Clash?.Invoke(other);
    }

    /// <summary>
    /// For initializing the handler once they enter the main scene.
    /// </summary>
    private void InitHandler()
    {
        // Another machine's scooter: its own machine freezes it when the main game ends (here it never started driving)
        if (RemoteAvatar.IsRemote(this))
            return;

        OrderManager.Instance.OnMainGameFinishes += () => ball.FreezeBall(true);
    }

    /// <summary>
    /// This method resets this handler.
    /// </summary>
    private void ResetHandler()
    {
        // Online, a session that ends in the menus or mid-load comes back here before any match scene's order manager
        if (OrderManager.Instance != null)
            OrderManager.Instance.OnMainGameFinishes -= () => ball.FreezeBall(false);

        if(order1 != null)
        {
            order1.EraseOrder();
        }
        if(order2 != null)
        {
            order2.EraseOrder();
        }

        // Any order still held goes back to its own scene. On a client the erases wait for the host, and the order
        // would otherwise ride into the menu on this player's scooter
        if (order1 != null)
            order1.ReturnHome();
        if (order2 != null)
            order2.ReturnHome();
        order1 = order2 = null;
        hasOrder = false;

        hasGoldenOrder = false;
        placement = 0;
        score = 0;
        ball.SetBoostModifier(hasOrder);
        ShowScore();
    }

    private void UpdateScore()
    {
        ShowScore();
        ScoreManager.Instance.UpdatePlacement();
    }

    public void UpdatePlacement()
    {
        if (numberHandler != null)
            numberHandler.UpdatePlacement(placement);
    }

    /// <summary>
    /// Online client: the host's score for this player (only the host changes scores), in their HUD
    /// </summary>
    public void ShowHostScore(int hostScore)
    {
        score = hostScore;
        ShowScore();
    }

    // The score and placing show in this player's HUD; another machine's scooter has none here
    private void ShowScore()
    {
        if (numberHandler != null)
            numberHandler.UpdateScoreUI(score.ToString());
    }

    /// <summary>
    /// Call when boost activates.
    /// </summary>
    public void AttemptSteal()
    {
        // Another machine's scooter: its own machine sees its hits (online)
        if (RemoteAvatar.IsRemote(this))
            return;

        if (playerTouching == null)
        {
            return;
        }

        Hit(playerTouching);
    }

    /// <summary>
    /// This machine's player, boosting, hit another: a steal, or a clash when they're boosting too. A local match does it
    /// at once; online the host decides, for everyone (StealSync)
    /// </summary>
    /// <param name="other">The player hit</param>
    private void Hit(OrderHandler other)
    {
        if (GameAuthority.IsOnline)
        {
            StealSync.Ask(OrderSync.SeatOf(this), OrderSync.SeatOf(other));
            return;
        }

        if (!other.IsBoosting)
        {
            StealOrder(other);
        }
        else
        {
            ClashWith(other);
            other.ClashWith(this);
        }
    }

    /// <summary>
    /// Typical OnTriggerEnter. Handles stealing orders from other players.
    /// </summary>
    /// <param name="other">Collider player has hit. Will attempt to steal if this hitbox is another player</param>
    private void OnTriggerEnter(Collider other)
    {
        OrderHandler otherHandler;
        try
        {
            otherHandler = other.gameObject.transform.parent.GetComponentInChildren<OrderHandler>();

            if(otherHandler == this)
            {
                return;
            }
            else
            {
                otherHandler.PlayerTouching = this;
            }
        }
        catch
        {
            return;
        }

        // Another machine's scooter: its own machine sees its hits (online). The touch is noted above all the same: its own
        // machine never tells this one, and this machine's player's boost needs it
        if (RemoteAvatar.IsRemote(this))
            return;

        if (ball.Boosting)
        {
            if (otherHandler != null) // I don't know why I have to do this but apparently I do
            {
                Hit(otherHandler);
                playerTouching = null;
                otherHandler.playerTouching = null;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        OrderHandler otherHandler;
        try
        {
            otherHandler = other.gameObject.transform.parent.GetComponentInChildren<OrderHandler>();

            if(otherHandler.PlayerTouching == this)
            {
                otherHandler.PlayerTouching = null;
            }
        }
        catch
        {
            return;
        }
    }
}
