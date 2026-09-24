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