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
    [SerializeField] private Constants.OrderValue value;
    public Constants.OrderValue Value { get { return value; } }

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
    [SerializeField] private GameObject orderMeshObject;
    private MeshRenderer meshRenderer;
    private MeshFilter meshFilter;
    [Tooltip("Scale of the mesh based on order difficulty.")]
    [SerializeField]private float[] meshScale = new float[4];

    [Header("Order Information")]
    [SerializeField] private Transform pickup;
    [SerializeField] private Transform dropoff;
    private Transform lastGrounded;
    public Transform LastGrounded { get { return lastGrounded; } set { lastGrounded = value;} }
    private OrderHandler playerHolding = null;
    public OrderHandler PlayerHolding { get {  return playerHolding; } }
    private OrderHandler playerDropped; // for cooldown with losing an order
    public OrderHandler PlayerDropped { get {  return playerDropped; } }

    [Tooltip("Arrow that points to the dropoff.")]
    [SerializeField] private GameObject arrow;

    [Tooltip("Time between a player dropping a package and being able to pick it back up again")]
    [SerializeField] private float pickupCooldown = 3;
    private bool canPickup = true; // if a player can pickup this order
    public bool CanPickup { get { return canPickup; } }
    [Tooltip("Default height of the order")]
    [SerializeField] private float height = 4.43f;

    [Tooltip("Reference to the beacon on this prefab")]
    [SerializeField] private OrderBeacon beacon;

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
    private Quaternion ogMeshRot;

    private void Awake()
    {
        meshRenderer = orderMeshObject.GetComponent<MeshRenderer>();
        meshFilter = orderMeshObject.GetComponent<MeshFilter>();
        initMeshRotation = orderMeshObject.transform.localRotation;

        ogMeshPos = orderMeshObject.transform.position;
        ogMeshRot = orderMeshObject.transform.rotation;
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
        OrderManager.Instance.OnMainGameFinishes += EraseWhenSwappingToGold;

        if (shouldAdd)
            OrderManager.Instance.AddOrder(this);
        
        isActive = true;
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

        // Removes the ui from all players
        //compassMarker.RemoveCompassUIFromAllPlayers();

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
    /// This method "throws" the order in the air and then reinits it once the DOTween is complete. Meant for stealing.
    /// </summary>
    public void Drop(Vector3 newPosition)
    {
        canPickup = false;
        ResetMesh();

        this.transform.parent = OrderManager.Instance.transform;
        orderMeshObject.transform.rotation = initMeshRotation;

        //// Removes the ui from all players
        //compassMarker.RemoveCompassUIFromAllPlayers();

        arrow.SetActive(false);
        transform.LookAt(Vector3.zero);
        
        beacon.ToggleBeaconMesh(false);

        float height = Random.Range(1f, 10f);
        transform.position = newPosition + height * transform.up;

        transform.DOMoveY(newPosition.y, pickupCooldown)
            .SetEase(Ease.OutBounce).OnComplete(() => ReInitOrder(newPosition));
        StartPickupCooldownCoroutine();


        beconIndicator.InitalizeBeconIndicator(value);
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
    /// This method erases the golden order without it being "delivered". Used for hotkey functionality.
    /// </summary>
    public void EraseGoldWithoutDelivering()
    {
        beacon.EraseBeacon();
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

        //OrderManager.Instance.ReparentOrder(gameObject);
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
            // Removes the ui from all players
            compassMarker.RemoveCompassUIFromAllPlayers();
            playerHolding.GetComponent<Compass>().RemoveCompassMarker(beacon.CompassMarker);
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
