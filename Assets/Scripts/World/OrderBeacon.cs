using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

/// <summary>
/// This class is for the beacon to indicate where an order is and where it needs to be delivered to
/// </summary>
public class OrderBeacon : MonoBehaviour
{
    private Order order; // order the beacon is tracking
    private bool isPickup = true;
    public bool IsPickup { get { return isPickup; } }

    private Color color;

    [SerializeField] private CompassMarker compassMarker; // for the dropoff location on the compass marker
    public CompassMarker CompassMarker { get { return compassMarker; } }

    [SerializeField] private OrderGhost customer;

    [Header("Material Information")]
    [SerializeField] private VisualEffect beaconFX;
    [Tooltip("The last element in the array is for dropoff beacons.")]
    [SerializeField][ColorUsage(true, true)] private Color[] mainColors;
    [Tooltip("The last element in the array is for dropoff beacons.")]
    [SerializeField][ColorUsage(true, true)] private Color[] subColors;
    private Color cachedMain, cachedSub;

    [Header("Flame Logic")]
    [SerializeField] private MeshRenderer dissolveRend;
    [SerializeField] private Color[] flameColors;
    [Tooltip("Height offset for the flame. Employ guess and check strategies to fine tune this number.")]
    [SerializeField] private float flameOffset = 0.06f;

    //private Material cachedMat;
    private bool canInteract;

    private const float REND_HEIGHT = 1.9f; // for calculating the height of the flame
    [SerializeField] private Transform flameChecker;

    /// <summary>
    /// This method initializes the beacon. It sets the order the beacon is tracking, sets the color, and the position.
    /// </summary>
    /// <param name="</param>
    public void InitBeacon(Order inOrder, int orderIndex)
    {
        canInteract = true;
        ToggleBeaconMesh(true);
        order = inOrder;
        this.transform.position = order.transform.position;

        // set the color of the dissolve
        switch(order.Value)
        {
            case Constants.OrderValue.Easy:
                dissolveRend.material.color = flameColors[0];
                beaconFX.SetVector4("MainColor", mainColors[0]);
                beaconFX.SetVector4("SubColor", subColors[0]);
                break;
            case Constants.OrderValue.Medium:
                dissolveRend.material.color = flameColors[1];
                beaconFX.SetVector4("MainColor", mainColors[1]);
                beaconFX.SetVector4("SubColor", subColors[1]);
                break;
            case Constants.OrderValue.Hard:
                dissolveRend.material.color = flameColors[2];
                beaconFX.SetVector4("MainColor", mainColors[2]);
                beaconFX.SetVector4("SubColor", subColors[2]);
                break;
            case Constants.OrderValue.Golden:
                dissolveRend.material.color = flameColors[3];
                beaconFX.SetVector4("MainColor", mainColors[3]);
                beaconFX.SetVector4("SubColor", subColors[3]);
                break;
            default:
                break;
        }

        cachedMain = beaconFX.GetVector4("MainColor");
        cachedSub = beaconFX.GetVector4("SubColor");
        beaconFX.gameObject.layer = 28;
        beaconFX.gameObject.SetActive(true);
        CheckFlamePosition();
    }

    /// <summary>
    /// This method changes the pickup beacon to a dropoff beacon.
    /// </summary>
    /// <param name="dropoff">Dropoff location</param>
    public void SetDropoff(Transform dropoff)
    {
        canInteract = true;
        this.transform.position = dropoff.position;
        this.transform.parent = OrderManager.Instance.transform;

        customer.transform.position = dropoff.position;
        customer.transform.parent = this.transform;
        customer.InitCustomer();
        
        isPickup = false;
        beaconFX.SetVector4("MainColor", mainColors[4]);
        beaconFX.SetVector4("SubColor", subColors[4]);

        compassMarker.RemoveCompassUIFromAllPlayers();

        order.PlayerHolding.GetComponent<Compass>().AddCompassMarker(compassMarker);
        
        // NOTE: if camera layers change it'll fuck with beacon rendering
        beaconFX.gameObject.layer = order.PlayerHolding.transform.parent.GetComponentInChildren<SphereCollider>().gameObject.layer + 7;
        beaconFX.gameObject.SetActive(true);

        CheckFlamePosition();
    }

    /// <summary>
    /// This method will set the beacon back to a pickup beacon in the event it is knocked out of the player's possession.
    /// </summary>
    public void ResetPickup()
    {
        OrderManager.Instance.ReparentOrder(order.gameObject);

        compassMarker.RemoveCompassUIFromAllPlayers();
        order.compassMarker.InitalizeCompassUIOnAllPlayers();
        //meshRenderer.material = cachedMat;
        beaconFX.SetVector4("MainColor", cachedMain);
        beaconFX.SetVector4("SubColor", cachedSub);

        customer.transform.parent = OrderManager.Instance.transform;
        this.transform.position = order.transform.position;
        this.transform.parent = order.transform;
        ToggleBeaconMesh(true);
        //meshRenderer.material.color = color;
        isPickup = true;
        order.RemovePlayerHolding();
        beaconFX.gameObject.layer = 28; // reset to render in phase layer
        beaconFX.gameObject.SetActive(true);

        CheckFlamePosition();
    }

    private void CheckFlamePosition()
    {
        dissolveRend.transform.gameObject.SetActive(false);
        dissolveRend.transform.localPosition = new Vector3(0, 1, 0); // hard coded value be careful
        int lm = 1 << 29; // default layer only
        RaycastHit hit;

        if (Physics.Raycast(dissolveRend.transform.position, Vector3.down, out hit, Mathf.Infinity, lm))
        {
            float diff = hit.distance;// - REND_HEIGHT;
            dissolveRend.transform.position -= diff * Vector3.up;
            dissolveRend.transform.localPosition += flameOffset * Vector3.up;
        }

        dissolveRend.transform.gameObject.SetActive(true);
    }

    /// <summary>
    /// Sets the mesh renderer of the beacon to true or false. Used for hiding the beacon during a drop animation.
    /// </summary>
    /// <param name="status">Enabled property of the mesh renderer</param>
    public void ToggleBeaconMesh(bool status)
    {
        canInteract = status;
        //meshRenderer.enabled = status;
        dissolveRend.enabled = status;
        beaconFX.enabled = status;
    }

    /// <summary>
    /// This method is the first half of the process to reset the beacon. It "throws" the order to the customer for them to catch.
    /// </summary>
    public void ThrowOrder(float throwTime)
    {
        ToggleBeaconMesh(false);
        gameObject.layer = 0;

        customer.transform.parent = OrderManager.Instance.transform;
        customer.DeliveredOrder();
        order.transform.DOMove(customer.CustomerPos, throwTime).OnComplete(() => { EraseBeacon(); });
    }

    /// <summary>
    /// This method is the second half of the process to reset the beacon. It is called when the customer "catches" the order.
    /// </summary>
    public void EraseBeacon()
    {
        gameObject.GetComponent<CompassMarker>().RemoveCompassUIFromAllPlayers();

        customer.ThankYouComeAgain();
        ToggleBeaconMesh(false);
        gameObject.layer = 0;

        if (order != null)
        {
            order.EraseOrder();
        }
        isPickup = true;
    }

    /// <summary>
    /// Will execute whenever something enters the beacon's light.
    /// </summary>
    /// <param name="other"></param>
    private void OnTriggerStay(Collider other)
    {
        // Online, only the host decides pickups and deliveries
        if (!GameAuthority.IsAuthority)
            return;

        if (!canInteract || !order.CanPickup)
            return;

        if (other.name == "Ball Of Fun")
        {
            Transform parent = other.transform.parent;
            OrderHandler orderHandler = parent.GetComponentInChildren<OrderHandler>();
            if (isPickup)
            {
                if (orderHandler.CanTakeOrder)
                {
                    canInteract = false;
                    orderHandler.AddOrder(order); // add the order if the beacon is a pickup beacon
                }
            }
            else
            {
                if (order.PlayerHolding == orderHandler)
                {
                    canInteract = false;
                    orderHandler.DeliverOrder(order); // deliver the order if the beacon is a dropoff beacon
                }
            }
        }
    }
}
