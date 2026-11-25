using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CustomizationSelector : MonoBehaviour
{
    const int CUSTOMIZATION_OPTIONS = 2;
    private bool disableOptionsCustomization = false;
    public void SetDisableOptionsCustomization(bool newValue) { disableOptionsCustomization = newValue; }

    /// <summary>The ghost colours to choose from</summary>
    public IReadOnlyList<PlayerColorInformationSO> Colours { get { return playerColors; } }

    /// <summary>The hats to choose from</summary>
    public IReadOnlyList<PlayerHatInformationSO> Hats { get { return playerHats; } }

    /// <summary>The chosen colour: an index into Colours</summary>
    public int ColourIndex { get { return currentPlayerColor; } }

    /// <summary>The chosen hat: an index into Hats</summary>
    public int HatIndex { get { return currentPlayerHat; } }

    [Header("Character Customization Options")]
    [SerializeField] CustomizationChanging customizationChanging;

    [SerializeField] int currentPlayerColor = 0;
    [SerializeField] PlayerColorInformationSO[] playerColors;

    [SerializeField] int currentPlayerHat = 0;
    [SerializeField] PlayerHatInformationSO[] playerHats;

    [Header("References")]
    [SerializeField] GameObject selector;
    [SerializeField] SkinnedMeshRenderer ghostModel;
    [SerializeField] MeshRenderer ghostEyelid1;
    [SerializeField] MeshRenderer ghostEyelid2;

    [SerializeField] MeshRenderer hatModel;
    Material[] ghostMats;

    [SerializeField] PhaseIndicator phaseIndicator;
    [SerializeField] GameObject[] sliderGameobjcts;
    CanvasGroup sliderLook; // hides the sliders' look while Dispatch's pop-up shows the choices itself (SetSlidersShown)

    [Header("Animations")]
    Animator currentSlider;
    [SerializeField] Animator colorSlider;
    [SerializeField] CustomizationSlider colorSliderCustomization;

    [SerializeField] Animator hatSlider;
    [SerializeField] CustomizationSlider hatSliderCustomization;

    enum CustomizationChanging
    {
        playerColor = 0,
        playerHat = 1
    }

    public void Start()
    {
        UpdateGhostColor();
        UpdateHat();
    }

    // The sliders get their icons as soon as this menu exists: their idle animation shows them from its first frame,
    // which can come before Start (online, a player moving seat joins again late in a frame)
    void Awake()
    {
        // Set Ghost Color Icons
        Sprite[] colorSprites = new Sprite[playerColors.Length];
        for(int i = 0; i < playerColors.Length; i++)
        {
            colorSprites[i] = playerColors[i].colorIcon;
        }
        colorSliderCustomization.SetSliderSprites(colorSprites);

        // Set Ghost Hat Icons
        Sprite[] hatSprites = new Sprite[playerHats.Length];
        for (int i = 0; i < playerHats.Length; i++)
        {
            hatSprites[i] = playerHats[i].hatIcon;
        }
        hatSliderCustomization.SetSliderSprites(hatSprites);

    }

    public void SetCustomizationType(bool direction)
    {
        if (disableOptionsCustomization)
            return;

        // Positive Scroll
        if (direction)
        {
            if ((int)customizationChanging == CUSTOMIZATION_OPTIONS - 1)
            {
                customizationChanging = 0;
            }
            else
            {
                customizationChanging = customizationChanging + 1;
            }
        }
        // Negative Scroll
        else
        {
            if ((int)customizationChanging == 0)
            {
                customizationChanging = (CustomizationChanging)CUSTOMIZATION_OPTIONS - 1;
            }
            else
            {
                customizationChanging = customizationChanging - 1;
            }
        }

        // Updates selector for current slider selected
        selector.transform.position = sliderGameobjcts[(int)customizationChanging].transform.position;
    }

    public void SetCustomizationValue(bool direction)
    {
        if (disableOptionsCustomization)
            return;

        switch (customizationChanging)
        {
            case CustomizationChanging.playerColor:
                currentSlider = colorSlider;
                SetIntValue(direction, ref currentPlayerColor, playerColors.Length);
                UpdateGhostColor();
                return;
            case CustomizationChanging.playerHat:
                currentSlider = hatSlider;
                SetIntValue(direction, ref currentPlayerHat, playerHats.Length);
                UpdateHat();
                return;
        }
    }

    /// <summary>Whether the hat row is the one the d-pad's left and right change (else the colour row)</summary>
    public bool ChangingHat { get { return customizationChanging == CustomizationChanging.playerHat; } }

    // A scroll is no reason to show off (playtest, 2026-11-25): on Dispatch the ghost does once DONE closes the pop-up
    // (MenuInteractions), and in Local Play once the new look has stayed put a moment (Update)
    readonly RestyleWatch restyle = new RestyleWatch();

    // The sliders come up (Dispatch's X, Local Play's player select): the look to compare with is the one now
    void OnEnable()
    {
        MarkLook();
    }

    // Local Play has no DONE: its ghost shows a new look off once the player stops scrolling. Online, MenuInteractions
    // decides, as the pop-up closes
    void Update()
    {
        if (!GameAuthority.IsOnline && restyle.Settled(currentPlayerColor, currentPlayerHat, Time.unscaledTime))
            ShowOff();
    }

    /// <summary>The look now is the one a later change is measured from (the pop-up opened, or the player readied)</summary>
    public void MarkLook()
    {
        restyle.Mark(currentPlayerColor, currentPlayerHat);
    }

    /// <summary>Whether the colour or hat changed since the look was last marked; marks the look now either way</summary>
    public bool TakeChanged()
    {
        return restyle.Take(currentPlayerColor, currentPlayerHat);
    }

    /// <summary>
    /// Shows or hides the sliders' look (they still work): Dispatch's customize pop-up shows the player's ghost from
    /// their preview and the choices itself, so the sliders mustn't show in that picture
    /// </summary>
    public void SetSlidersShown(bool shown)
    {
        if (sliderLook == null)
        {
            if (shown)
                return;
            sliderLook = GetComponent<CanvasGroup>();
            if (sliderLook == null)
                sliderLook = gameObject.AddComponent<CanvasGroup>();
        }
        float alpha = shown ? 1f : 0f;
        if (sliderLook.alpha != alpha)
            sliderLook.alpha = alpha;
    }

    /// <summary>The player's ghost shows off its new colour or hat (PlayerEmotes)</summary>
    public void ShowOff()
    {
        PlayerEmotes emotes = GetComponentInParent<PlayerEmotes>(true);
        if (emotes != null)
            emotes.Restyled();
    }

    public void SetIntValue(bool direction, ref int valueToChange, int maxAmount)
    {
        // Positive Scroll
        if (direction)
        {
            if (valueToChange == maxAmount - 1)
            {
                valueToChange = 0;
            }
            else
            {
                valueToChange++;
            }
            currentSlider.SetTrigger(HashReference._slideLeftTrigger);
        }
        // Negative Scroll
        else
        {
            if (valueToChange == 0)
            {
                valueToChange = maxAmount - 1;
            }
            else
            {
                valueToChange--;
            }
            currentSlider.SetTrigger(HashReference._slideRightTrigger);
        }

        if (customizationChanging == CustomizationChanging.playerColor)
        {
            colorSliderCustomization.currentMainPos = valueToChange;
        }
        else if (customizationChanging == CustomizationChanging.playerHat)
        {
            hatSliderCustomization.currentMainPos = valueToChange;
        }

    }

    public void UpdateGhostColor()
    {
        ghostMats = ghostModel.materials;
        ghostMats[0] = playerColors[currentPlayerColor].colorMaterial;
        ghostModel.materials = ghostMats;

        ghostEyelid1.material = playerColors[currentPlayerColor].colorMaterial;
        ghostEyelid2.material = playerColors[currentPlayerColor].colorMaterial;

        phaseIndicator.ReferenceHornMaterial();
    }

    public void UpdateHat() 
    {
        PlayerHatInformationSO hatInfo = playerHats[currentPlayerHat];

        if(hatInfo.displayHat == true)
        {
            hatModel.gameObject.SetActive(true);
            hatModel.gameObject.GetComponent<MeshFilter>().mesh = hatInfo.hatMesh;
            hatModel.material = hatInfo.hatMaterial;

            hatModel.gameObject.transform.localPosition = hatInfo.hatPosition;
            hatModel.gameObject.transform.localRotation = Quaternion.Euler(hatInfo.hatRotation.x, hatInfo.hatRotation.y, hatInfo.hatRotation.z);
            hatModel.gameObject.transform.localScale = hatInfo.hatScale;
        }
        else
        {
            hatModel.gameObject.SetActive(false);
        }
    }
}