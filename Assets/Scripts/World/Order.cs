using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// This class is for the order items in the game. It has simple methods for adding and dropping off orders, and contains the enum for the values of the orders.
/// </summary>
public class Order : MonoBehaviour
{
    [SerializeField] internal Constants.OrderValue value;
    public Constants.OrderValue Value { get { return value; } }

    // This order's key, the same on every machine (OrderBook): online, an order change names its order by it
    internal int key;
    public int Key { get { return key; } }

    // The scene this order loaded with
    internal UnityEngine.SceneManagement.Scene homeScene;

    [Tooltip("Order will spawn at runtime. Use this for the tutorial orders.")]
    [SerializeField] private bool isActive = false;
    public bool IsActive { get { return isActive; } set { isActive = value; } }
    private bool stealActive = false;
    public bool StealActive { get { return stealActive; } set { stealActive = value; } }

    [Header("Positional Information")]
    [Tooltip("How far above the ground the order is by default.")]
    [SerializeField] private float orderHeight = 4.43f;

    [Header("Mesh Information")]
    [Tooltip("Actual mesh of the order for rotation and swapping models.")]
    [SerializeField] internal GameObject orderMeshObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    [Tooltip("Scale of the mesh based on order difficulty.")]
    [SerializeField]private float[] meshScale = new float[4];

    [Header("Order Information")]
    [SerializeField] private Transform pickup;
    [SerializeField] private Transform dropoff;
    public Transform PickupPoint { get { return pickup; } }
    public Transform DropoffPoint { get { return dropoff; } }
    private Transform lastGrounded;
    public Transform LastGrounded { get { return lastGrounded; } set { lastGrounded = value;} }
    internal OrderHandler playerHolding = null;
    public OrderHandler PlayerHolding { get {  return playerHolding; } }
    private OrderHandler playerDropped; // for cooldown with losing an order
    public OrderHandler PlayerDropped { get {  return playerDropped; } }

    [Tooltip("Arrow that points to the dropoff.")]
    [SerializeField] internal GameObject arrow;

    [Tooltip("Time between a player dropping a package and being able to pick it back up again")]
    [SerializeField] private float pickupCooldown = 3;
    private bool canPickup = true; // if a player can pickup this order
    public bool CanPickup { get { return canPickup; } }
    [Tooltip("Default height of the order")]
    [SerializeField] private float height = 4.43f;

    [Tooltip("Reference to the beacon on this prefab")]
    [SerializeField] internal OrderBeacon beacon;

    [Tooltip("Reference to the compass marker component on this object")]
    public CompassMarker compassMarker;

    [Header("Order Movement When Holding")]
    [Tooltip("The amount of time it takes to rotate the order.")]
    [SerializeField] private float rotationDuration;
    [Tooltip("The amount the order will rotation per duration.")]
    [SerializeField] private Vector3 meshRotation;
    [Tooltip("How long it takes for the order to bob between positions.")]
    [SerializeField] private float bobbingDuration;
    [Tooltip("Bob offset of original order position.")]
    [SerializeField] private Vector3 bobPosition;

    // tweening
    private Tween floatyTween, bobbyTween, arrowTween;
    private Quaternion initMeshRotation;

    [Header("Order Type Information")]
    [SerializeField] Sprite[] possiblePackageTypes;

    [Tooltip("The HDR color options for different tiers of order glow")]
    [SerializeField][ColorUsageAttribute(true, true)]private Color[] glowColors;

    [Tooltip("Ref to glow VFX")]
    [SerializeField] private VisualEffect glow;

    [Tooltip("Materials for different orders")]
    [SerializeField] private Material[] orderMaterials;

    [Tooltip("Different meshes of the package depending on the difficulty")]
    [SerializeField] private Mesh[] orderMesh;

    [Header("Becon Indicator Info")]
    [SerializeField] BeconIndicator beconIndicator;

    private IEnumerator pickupCooldownCoroutine; // IEnumerator reference for pickupCooldown coroutine

    private Vector3 ogMeshPos;
    internal Quaternion ogMeshRot;

    private void Awake()
    {
        meshRenderer = orderMeshObject.GetComponent<MeshRenderer>();
        meshFilter = orderMeshObject.GetComponent<MeshFilter>();
        initMeshRotation = orderMeshObject.transform.localRotation;

        ogMeshPos = orderMeshObject.transform.position;
        ogMeshRot = orderMeshObject.transform.rotation;

        homeScene = gameObject.scene;
        key = OrderBook.KeyOf(this);
        OrderBook.Add(this);
    }

    private void OnDestroy()
    {
        OrderBook.Remove(this);
    }

    /// <summary>
    /// Takes this order off whatever carries it and back into the scene it loaded with, so it goes when that scene does.
    /// Scooters and the order manager outlive scenes (DontDestroyOnLoad), and an order under one would go along with it.
    /// If that scene is gone already, so is the order
    /// </summary>
    public void ReturnHome()
    {
        transform.SetParent(null);
        if (homeScene.isLoaded)
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(gameObject, homeScene);
        else
            Destroy(gameObject);
    }

    private void Update()
    {
        meshRenderer.enabled = isActive || stealActive;
        glow.enabled = meshRenderer.enabled;

        beacon.gameObject.SetActive(isActive);
        
        if (playerHolding != null)
        {
            this.gameObject.transform.forward = -playerHolding.transform.right;
        }

        Vector3 newDir = dropoff.position - arrow.transform.position;
        newDir = new Vector3(newDir.x, 0, newDir.z);
        arrow.transform.rotation = Quaternion.LookRotation(newDir, Vector3.up);
    }


    /// <summary>
    /// This method initializes this order to be ready for pickup. It also initializes the beacon for this order.
    /// </summary>
    public void InitOrder(bool shouldAdd = true)
    {
        // Online, the host spawns orders: a client shows the host's spawn
        if (!OrderSync.MayChange)
            return;

        using OrderSync.Scope change = OrderSync.Change(OrderChange.Spawn(key, shouldAdd));

        OrderManager.Instance.OnMainGameFinishes += EraseWhenSwappingToGold;

        if (shouldAdd)
            OrderManager.Instance.AddOrder(this);
        
        isActive = true;
        // A cutout's order leaves its hold (online, a replayed steal too)
        stealActive = false;
        arrow.SetActive(false);
        this.transform.position = pickup.position;

        // Determines the pacakge type value based on the order type
        int packageType = 0;
        switch (value)
        {
            case Constants.OrderValue.Easy:
                packageType = 0;
                break;
            case Constants.OrderValue.Medium:
                packageType = 1;
                break;
            case Constants.OrderValue.Hard:
                packageType = 2;
                break;
            case Constants.OrderValue.Golden:
                packageType = 3;
                break;
        }

        // set mesh based on package type
        meshRenderer.material = orderMaterials[packageType];
        meshFilter.mesh = orderMesh[packageType];
        orderMeshObject.transform.localScale = new Vector3(meshScale[packageType], meshScale[packageType], meshScale[packageType]);

        // beacon and compass marker information
        beacon.InitBeacon(this, packageType);
        compassMarker.icon = possiblePackageTypes[packageType];
        compassMarker.InitalizeCompassUIOnAllPlayers();
        beconIndicator.InitalizeBeconIndicator(value);

        // glow up gurl
        glow.SetVector4("GlowColour", glowColors[packageType]);
    }
    
    /// <summary>
    /// Sets mesh position so stealing doesn't make it rise.
    /// </summary>
    /// <param name="inPosition"></param>
    public void SetMeshPosition(Vector3 inPosition)
    {
        this.transform.position = inPosition;
        orderMeshObject.transform.parent = transform;
        orderMeshObject.transform.localPosition = -Vector3.up;
    }

    /// <summary>
    /// This method is called when the order is picked up by a player.
    /// </summary>
    public void Pickup(OrderHandler player)
    {
        playerHolding = player;

        ResetMesh();
        arrow.SetActive(true);

        if (value == Constants.OrderValue.Golden)
        {
            playerHolding.HasGoldenOrder = true;
        }
        playerDropped = null;
        beacon.SetDropoff(dropoff);
        beacon.gameObject.SetActive(true);
        compassMarker.SwitchCompassUIForPlayers(true);

        floatyTween.Kill();
        bobbyTween.Kill();
        arrowTween.Kill();

        // start tweening
        floatyTween = orderMeshObject.transform.DOLocalRotate(meshRotation, rotationDuration, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Incremental)
            .SetRelative()
            .SetEase(Ease.Linear);
        bobbyTween = orderMeshObject.transform.DOLocalMove(bobPosition, bobbingDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetRelative()
            .SetEase(Ease.Linear);
        arrowTween = arrow.transform.DOLocalMove(bobPosition, bobbingDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetRelative()
            .SetEase(Ease.Linear);


        beconIndicator.RemoveBeconIndicator();
    }

    /// <summary>
    /// Similar to pickup but for the cardboard cutout to hold the tutorial order.
    /// </summary>
    public void CardboardHold()
    {
        int packageType = 0;
        switch (value)
        {
            case Constants.OrderValue.Easy:
                packageType = 0;
                break;
            case Constants.OrderValue.Medium:
                packageType = 1;
                break;
            case Constants.OrderValue.Hard:
                packageType = 2;
                break;
            case Constants.OrderValue.Golden:
                packageType = 3;
                break;
        }

        // set mesh based on package type
        meshRenderer.material = orderMaterials[packageType];
        meshFilter.mesh = orderMesh[packageType];
        orderMeshObject.transform.localScale = new Vector3(meshScale[packageType], meshScale[packageType], meshScale[packageType]);
        stealActive = true;

        ResetMesh();

        beacon.gameObject.SetActive(false);

        // start tweening
        floatyTween = orderMeshObject.transform.DORotate(meshRotation, rotationDuration, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Incremental)
            .SetRelative()
            .SetEase(Ease.Linear);
        bobbyTween = orderMeshObject.transform.DOLocalMove(bobPosition, bobbingDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetRelative()
            .SetEase(Ease.Linear);
        arrowTween = arrow.transform.DOLocalMove(bobPosition, bobbingDuration)
            .SetLoops(-1, LoopType.Yoyo)
            .SetRelative()
            .SetEase(Ease.Linear);
    }

    /// <summary>
    /// How high a dropped order can fly before it falls back down
    /// </summary>
    public const float DROP_HEIGHT_MIN = 1f, DROP_HEIGHT_MAX = 10f;

    /// <summary>
    /// A random height for a dropped order to fly up to. Online the host picks it, so every machine shows the same drop
    /// </summary>
    public static float DropHeight()
    {
        return Random.Range(DROP_HEIGHT_MIN, DROP_HEIGHT_MAX);
    }

    /// <summary>
    /// This method "throws" the order in the air and then reinits it once the DOTween is complete. Meant for stealing.
    /// </summary>
    public void Drop(Vector3 newPosition, float height)
    {
        canPickup = false;
        ResetMesh();

        this.transform.parent = OrderManager.Instance.transform;
        orderMeshObject.transform.rotation = initMeshRotation;

        arrow.SetActive(false);
        transform.LookAt(Vector3.zero);
        
        beacon.ToggleBeaconMesh(false);

        transform.position = newPosition + height * transform.up;

        transform.DOMoveY(newPosition.y, pickupCooldown)
            .SetEase(Ease.OutBounce).OnComplete(() => ReInitOrder(newPosition));
        StartPickupCooldownCoroutine();


        beconIndicator.InitalizeBeconIndicator(value);
    }

    /// <summary>
    /// Ends this order's move now, with what happens at its end: a drop's fall lands, a delivery's throw reaches the
    /// customer. A drop's pickup cooldown ends with its fall: ending later by itself, it would take the order off whoever
    /// has it by then. Online, a client does this before it replays the host's next change to this order
    /// </summary>
    public void FinishMoves()
    {
        DOTween.Complete(transform, true);

        if (pickupCooldownCoroutine != null)
        {
            StopCoroutine(pickupCooldownCoroutine);
            pickupCooldownCoroutine = null;
            canPickup = true;
            playerDropped = null;
        }
    }

    /// <summary>
    /// Resets the properties of the order so it can be picked up again.
    /// </summary>
    /// <param name="newPosition"></param>
    private void ReInitOrder(Vector3 newPosition)
    {
        if (value == Constants.OrderValue.Golden)
        {
            playerHolding.HasGoldenOrder = false;
        }

        playerDropped = playerHolding;
        beacon.ResetPickup();
        RemovePlayerHolding();
    }
    /// <summary>
    /// This method performs the first half of the delivery, basically just hands the order to the customer.
    /// </summary>
    public void DeliverOrder()
    {
        arrow.SetActive(false);
        if (value != Constants.OrderValue.Golden)
        {
            RemovePlayerHolding();
            // Off the scooter: the throw outlives it, so a player who leaves mid-throw doesn't take the order with them
            ReturnHome();
            beacon.ThrowOrder(0.25f); // hard coded value for throwing the order to a customer
        }
        else
        {
            EraseOrder(); // temp fix for dotween handoff bug
        }
    }

    /// <summary>
    /// This method fully erases the order so it's available in the pool.
    /// </summary>
    public void EraseOrder()
    {
        // Online, the host erases orders: a client shows the host's erase (its own delivery throw ends here, and waits)
        if (!OrderSync.MayChange)
            return;

        using OrderSync.Scope change = OrderSync.Change(OrderChange.Erase(key));

        ResetMesh();

        orderMeshObject.transform.rotation = initMeshRotation;

        DOTween.Kill(transform);
        arrow.SetActive(false);
        
        // Removes the ui from all players
        compassMarker.RemoveCompassUIFromAllPlayers();

        beacon.CompassMarker.RemoveCompassUIFromAllPlayers();

        OrderManager.Instance.IncrementCounters(value, -1);
        OrderManager.Instance.RemoveOrder(this);
        OrderManager.Instance.OnMainGameFinishes -= EraseWhenSwappingToGold;

        if (value == Constants.OrderValue.Golden)
        {
            if (playerHolding != null)
            {
                playerHolding.AwardGoldenBonus(OrderManager.Instance.FinalOrderValue); // delivering already added the base gold value
                playerHolding.HasGoldenOrder = false;
            }
            if (GameManager.Instance.MainState == GameState.FinalPackage) // gold order was legit delivered
            {
                OrderManager.Instance.GoldOrderDelivered(); // lets the OM know the golden order has been delivered
            }
        }
        
        if (playerHolding != null)
        {
            playerHolding.LoseOrder(this);
        }
        
        isActive = false;
        transform.position = pickup.position;

        if(value != Constants.OrderValue.Golden)
            OrderManager.Instance.ReparentOrder(gameObject);

        beconIndicator.RemoveBeconIndicator();
    }

    /// <summary>
    /// This method erases the golden order without it being "delivered": nobody gets its bonus, and the golden round
    /// doesn't end. Used when the game leaves the golden round, and when a player holding it leaves (online)
    /// </summary>
    public void EraseGoldWithoutDelivering()
    {
        // Online, the host erases orders: a client shows the host's erase
        if (!OrderSync.MayChange)
            return;

        using OrderSync.Scope change = OrderSync.Change(OrderChange.EraseGold(key));

        beacon.PutOut(); // not EraseBeacon: erasing the order there delivers it, which ends the golden round
        DOTween.Kill(transform); // a fall still landing stops: its landing would make it a pickup again
        arrow.SetActive(false);
        beconIndicator.RemoveBeconIndicator();
        OrderManager.Instance.FinalOrderValue = (int)Constants.OrderValue.Golden;
        if (value == Constants.OrderValue.Golden)
        {
            if (playerHolding != null)
            {
                playerHolding.HasGoldenOrder = false;
                playerHolding.LoseOrder(this);
                RemovePlayerHolding();
            }
            compassMarker.RemoveCompassUIFromAllPlayers();
            OrderManager.Instance.IncrementCounters(value, -1);
            OrderManager.Instance.RemoveOrder(this);
            isActive = false;
        }
        transform.position = pickup.position;
    }

    /// <summary>
    /// Erases order when the game swaps to the final order sequence. Doesn't erase gold order.
    /// </summary>
    private void EraseWhenSwappingToGold()
    {
        if (value == Constants.OrderValue.Golden)
            return;

        EraseOrder();
    }

    /// <summary>
    /// This method removes the beacon compass marker from the UI of playerHolding then sets playerHolding to null.
    /// </summary>
    public void RemovePlayerHolding()
    {
        ResetMesh();

        if (playerHolding != null)
        {
            // The golden order slows whoever holds it: no longer, once it leaves them (a steal takes it)
            if (value == Constants.OrderValue.Golden)
                playerHolding.HasGoldenOrder = false;

            // Removes the ui from all players
            compassMarker.RemoveCompassUIFromAllPlayers();
            // Another machine's scooter has no compass here
            Compass compass = playerHolding.GetComponent<Compass>();
            if (compass != null)
                compass.RemoveCompassMarker(beacon.CompassMarker);
        }
        playerHolding = null;
    }

    /// <summary>
    /// Resets the DOTween animations for the order.
    /// </summary>
    private void ResetMesh()
    {
        floatyTween.Kill();
        bobbyTween.Kill();
        arrowTween.Kill();

        arrow.transform.localPosition = Vector3.zero;

        orderMeshObject.transform.position = orderMeshObject.transform.position;
        orderMeshObject.transform.rotation = ogMeshRot;//Quaternion.Euler(-90, 0, 180);//ogMesh.localRotation;
    }

    private void StartPickupCooldownCoroutine()
    {
        if(pickupCooldownCoroutine == null)
        {
            pickupCooldownCoroutine = PickupCooldown();
            StartCoroutine(pickupCooldownCoroutine);
        }
    }

    private void StopPickupCountdownCoroutine()
    {
        beacon.ResetPickup();
        if (pickupCooldownCoroutine != null)
        {
            StopCoroutine(pickupCooldownCoroutine);
            pickupCooldownCoroutine = null;
        }
    }

    /// <summary>
    /// Coroutine that runs every time an order is knocked out of a player's possession. Will make the order ungrabbable for a predetermined time.
    /// </summary>
    /// <returns></returns>
    private IEnumerator PickupCooldown()
    {
        canPickup = false;
        yield return new WaitForSeconds(pickupCooldown);
        canPickup = true;
        playerDropped = null;
        StopPickupCountdownCoroutine();
    }
}
