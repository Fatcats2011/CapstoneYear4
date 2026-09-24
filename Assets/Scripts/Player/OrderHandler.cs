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
        if (inOrder == order1 || inOrder == order2)
            return;

        if (inOrder.CanPickup || inOrder.PlayerDropped != this)
        {
            // will add order if it fits, elsewise will not do anything
            if (order1 == null || order2 == null)
            {
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
        if(tutHandler.HasLearnt)
            ball.SetBoostModifier(hasOrder);
    }

    /// <summary>
    /// This method is called when the player delivers an order to the dropoff location. If the player has the right order it will drop it off and award the player.
    /// </summary>
    /// <param name="rightOrder">The correct order of the dropoff spot</param>
    public void DeliverOrder(Order rightOrder)
    {
        string key = rightOrder.Value == Constants.OrderValue.Golden ? "final_dropoff" : "dropoff";
        if (order1 == rightOrder)
        {
            soundPool.PlayOrderDropoff(key);
            score += (int)order1.Value;
            order1.DeliverOrder();
            order1 = null;
            ScoreManager.Instance.UpdatePlacement();
        }
        else if(order2 == rightOrder)
        {
            soundPool.PlayOrderDropoff(key);
            score += (int)order2.Value;
            order2.DeliverOrder();
            order2 = null;
            ScoreManager.Instance.UpdatePlacement();
        }

        if(order1 == null && order2 == null)
        {
            hasOrder = false;
        }

        SetDrivingIndicators();

        numberHandler.UpdateScoreUI(score.ToString());
        
        if(tutHandler.HasLearnt)
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
    /// </summary>
    /// <param name="basePos">The position of the order before adding the offset of the specific orderPosition.</param>
    public void DropEverything(Vector3 order1NewPos, Vector3 order2NewPos, bool shouldSpinout = true)
    {
        if (order1 != null)
        {
            order1.Drop(order1NewPos);
            order1 = null;
        }
        if (order2 != null)
        {
            order2.Drop(order2NewPos);
            order2 = null;
        }
        if(shouldSpinout) { GotHit(); }

        SetDrivingIndicators();

        hasOrder = false;
        ball.SetBoostModifier(hasOrder);
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
    /// This method steals the best order from a victim player. It will then make the victim drop their other order if they have one.
    /// </summary>
    /// <param name="victimPlayer">Player being stolen from</param>
    private void StealOrder(OrderHandler victimPlayer)
    {
        Order newOrder = victimPlayer.GetBestOrder();
        if (newOrder != null && (order1 == null || order2 == null))
        {
            playerAnimator.SetTrigger(HashReference._stealLeftTrigger);
            soundPool.PlayOrderTheft();
            victimPlayer.LoseOrder(newOrder);
            AddOrder(newOrder);
        }
        victimPlayer.DropEverything(victimPlayer.order1Position.position, victimPlayer.order2Position.position);
        BallDriving victimControl = victimPlayer.gameObject.GetComponent<BallDriving>();
        if (victimControl != null) victimControl.BounceOff(transform.position, victimControl.ClashForce * 0.5f);

        SetDrivingIndicators();
    }

    /// <summary>
    /// For initializing the handler once they enter the main scene.
    /// </summary>
    private void InitHandler()
    {
        OrderManager.Instance.OnMainGameFinishes += () => ball.FreezeBall(true);
    }

    /// <summary>
    /// This method resets this handler.
    /// </summary>
    private void ResetHandler()
    {
        OrderManager.Instance.OnMainGameFinishes -= () => ball.FreezeBall(false);

        if(order1 != null)
        {
            order1.EraseOrder();
        }
        if(order2 != null)
        {
            order2.EraseOrder();
        }

        hasGoldenOrder = false;
        placement = 0;
        score = 0;
        ball.SetBoostModifier(hasOrder);
        numberHandler.UpdateScoreUI(score.ToString());
    }

    private void UpdateScore()
    {
        numberHandler.UpdateScoreUI(score.ToString());
        ScoreManager.Instance.UpdatePlacement();
    }

    public void UpdatePlacement()
    {
        numberHandler.UpdatePlacement(placement);
    }

    /// <summary>
    /// Call when boost activates.
    /// </summary>
    public void AttemptSteal()
    {
        // Online, only the host decides steals and clashes
        if (!GameAuthority.IsAuthority)
            return;

        if (playerTouching == null)
        {
            return;
        }

        if (!playerTouching.IsBoosting)
        {
            StealOrder(playerTouching);
        }
        else
        {
            Clash(playerTouching);
            playerTouching.Clash(this);
        }
        ;
    }

    /// <summary>
    /// Typical OnTriggerEnter. Handles stealing orders from other players.
    /// </summary>
    /// <param name="other">Collider player has hit. Will attempt to steal if this hitbox is another player</param>
    private void OnTriggerEnter(Collider other)
    {
        // Online, only the host decides steals and clashes
        if (!GameAuthority.IsAuthority)
            return;

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
        if (ball.Boosting)
        {
            if (otherHandler != null) // I don't know why I have to do this but apparently I do
            {
                if (!otherHandler.IsBoosting)
                {
                    StealOrder(otherHandler);
                }
                else
                {
                    Clash(otherHandler);
                    otherHandler.Clash(this);
                }
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
