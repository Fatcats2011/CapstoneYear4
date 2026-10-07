using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Compass : MonoBehaviour
{
    [SerializeField] RawImage compassImage;
    [SerializeField] Transform compassCalc;
    [SerializeField] Transform player;

    [SerializeField] OrbitalCamera orbitalCamera;

    [SerializeField] Camera playerCam;

    [Tooltip("The y position of the ui on the compass")]
    [SerializeField] float iconHeight = -70;

    [SerializeField] GameObject iconPrefab;


    [SerializeField] List<CompassInformationInstance> compassInformationObjects = new List<CompassInformationInstance>();

    //[SerializeField] List<CompassIconUI> compassUIObjects = new List<CompassIconUI>();
    //public List<CompassIconUI> CompassUIObjects { get { return compassUIObjects; } }

    //[SerializeField] List<CompassMarker> compassMarkerObjects = new List<CompassMarker>();
    //public List<CompassMarker> CompassMarkerObjects { get { return compassMarkerObjects; } }

    [SerializeField] float compassUnit;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapMenu += ResetCompass;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapMenu -= ResetCompass;
    }

    private void Start()
    {
        // Calculates compass unit based on the size of the compass image ui size
        compassUnit = compassImage.rectTransform.rect.width / 360f;
    }

    private void Update()
    {
        // Updates the uv rect of the compass image, to scroll based on player rotation
        //compassImage.uvRect = new Rect((player.localEulerAngles.y + orbitalCamera.smoothXAxis) / 360f, 0f, 1f, 1f);

        // Loops for all markers on player and updates their position on the compass ui (backwards: a marker whose object is
        // gone leaves the list on the way)
        for (int i = compassInformationObjects.Count - 1; i >= 0; i--)
        {
            CompassInformationInstance instance = compassInformationObjects[i];
            if (instance.compassIcon != null && instance.compassMarker != null)
            {
                CompassIconUI marker = instance.compassIcon;

                // Updates the position on the compass
                //marker.imageRect.rectTransform.anchoredPosition = GetPosOnCompass(marker.objectReference);
                marker.imageRect.rectTransform.anchoredPosition = GetPosOnCompass(marker.objectReference);

                // Calculates the distance of the player to the object
                marker.distance = CalculateDistance(marker.objectReference);
                marker.SetDistanceText();

                // Determines if it is time to fade in or out marker, based on fade distance
                if (marker.distance < Constants.DISTANCE_TO_FADE && marker.Faded == false)
                {
                    marker.FadeMarkerOut();
                }
                else if (marker.distance > Constants.DISTANCE_TO_FADE && marker.Faded == true)
                {
                    marker.FadeMarkerIn();
                }
            }
            else
            {
                compassInformationObjects.RemoveAt(i);
                if (instance.compassIcon != null)
                    Destroy(instance.compassIcon.gameObject);
            }

        }

        // The nearest icons draw last (on top): sorted in place, and a child moved only when its place changes, so the
        // canvas isn't rebuilt every frame
        SortByDistanceDescending(compassInformationObjects, IconDistance);
        for (int i = 0; i < compassInformationObjects.Count; i++)
        {
            Transform icon = compassInformationObjects[i].compassIcon.transform;
            if (icon.GetSiblingIndex() != i)
                icon.SetSiblingIndex(i);
        }

    }

    static readonly System.Func<CompassInformationInstance, float> IconDistance = instance => instance.compassIcon.distance;

    ///<summary>
    /// Sorts a list farthest first, in place and stable (ties keep their order), as OrderByDescending did, without its
    /// allocations every frame
    ///</summary>
    public static void SortByDistanceDescending<T>(List<T> items, System.Func<T, float> distance)
    {
        for (int i = 1; i < items.Count; i++)
        {
            T item = items[i];
            float key = distance(item);
            int j = i - 1;
            while (j >= 0 && distance(items[j]) < key)
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = item;
        }
    }

    ///<summary>
    /// Adds a compass marker to the player's compass
    ///</summary>
    public void AddCompassMarker(CompassMarker marker)
    {
        // Creates new object
        GameObject newMarker = Instantiate(iconPrefab, compassImage.transform);
        CompassIconUI compassIconUI = newMarker.GetComponent<CompassIconUI>();

        compassIconUI.SetCompassIconSprite(marker.icon);
        compassIconUI.objectReference = marker;

        CompassInformationInstance currentCompassIconInstance = new CompassInformationInstance(compassIconUI, marker);

        compassInformationObjects.Add(currentCompassIconInstance);

        // Adds to list
        //compassMarkerObjects.Add(marker);
        //compassUIObjects.Add(compassIconUI);

        //return compassIconUI;
    }

    ///<summary>
    /// Removes a compass marker from the player's compass and deletes the icon
    ///</summary>
    public void RemoveCompassMarker(CompassMarker marker)
    {

        CompassInformationInstance oneToRemove = null;

        foreach (CompassInformationInstance instance in compassInformationObjects)
        {
            if(instance.compassMarker == marker)
            {
                oneToRemove = instance;
                break;
            }
        }

        if(oneToRemove == null)
        {
            return;
        }

        // remove it
        Destroy(oneToRemove.compassIcon.gameObject);

        compassInformationObjects.Remove(oneToRemove);
    }

    ///<summary>
    /// Swaps the compass icon for one that displays the marker being carried by a player
    /// isCarried indicates a scooter icon
    /// !isCarried indicates a floor icon
    ///</summary>
    public void ChangeCompassMarkerIcon(CompassMarker marker, bool isCarried)
    {

    }

    ///<summary>
    /// Gets pos of compass marker and returns a vector2 transform for the compas icons
    ///</summary>
    Vector2 GetPosOnCompass(CompassMarker marker)
    {
        // Calculate vector from player to object
        Vector3 playerToObjectVector = marker.transform.position - compassCalc.transform.position;

        // Calculate player's forward vector based on the rotation
        Vector3 playerForwardVector = compassCalc.transform.rotation * Vector3.forward;

        // Calculate the dot product
        float dotProduct = Vector3.Dot(playerToObjectVector.normalized, playerForwardVector.normalized);

        // Calculate the angle in radians
        float angleRadians = Mathf.Acos(dotProduct);

        // Convert the angle to degrees, and adjust for negative values when turning counterclockwise (west)
        float angleDegrees = Mathf.Rad2Deg * angleRadians;

        // Adjust the angle to be negative when turning counterclockwise
        angleDegrees = (Vector3.Cross(playerForwardVector, playerToObjectVector).y < 0) ? -angleDegrees : angleDegrees;

        return new Vector2((angleDegrees - orbitalCamera.smoothXAxis) * compassUnit, iconHeight);
    }

    ///<summary>
    /// Returns the distance of the marker to the player
    ///</summary>
    int CalculateDistance(CompassMarker marker)
    {
        return (int) Vector3.Distance(marker.transform.position, compassCalc.transform.position);
    }

    ///<summary>
    /// Resets compass by removing any references
    ///</summary>
    public void ResetCompass()
    {

        for(int i = compassInformationObjects.Count - 1; i >= 0; i--)
        {
            // remove it

            if(compassInformationObjects[i].compassIcon != null)
                Destroy(compassInformationObjects[i].compassIcon.gameObject);

            compassInformationObjects.Remove(compassInformationObjects[i]);
        }
    }
}

[System.Serializable]
public class CompassInformationInstance
{
    public CompassIconUI compassIcon;
    public CompassMarker compassMarker;

    public CompassInformationInstance(CompassIconUI compassIconUI, CompassMarker compassMarker)
    {
        this.compassIcon = compassIconUI;
        this.compassMarker = compassMarker;
    }
}
