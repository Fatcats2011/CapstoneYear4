using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The title screen's mist: a band of cloud that tiles left to right, drifting slowly along the road
/// </summary>
[RequireComponent(typeof(RawImage))]
public class TitleBackdrop : MonoBehaviour
{
    [SerializeField] internal Vector2 drift = new Vector2(0.012f, 0f);   // tiles a second
    [SerializeField] internal Vector2 tiles = new Vector2(1.6f, 1f);     // across the band

    RawImage fog;

    void Awake()
    {
        fog = GetComponent<RawImage>();
    }

    void Update()
    {
        Vector2 offset = drift * Time.unscaledTime;
        fog.uvRect = new Rect(offset.x % 1f, offset.y % 1f, tiles.x, tiles.y);
    }
}
