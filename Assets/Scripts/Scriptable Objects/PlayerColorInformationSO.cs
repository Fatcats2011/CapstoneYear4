using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Player Color Information", menuName = "Customization/Player Color Information", order = 1)]
public class PlayerColorInformationSO : ScriptableObject
{
    [Header("Color Information")]
    public Material colorMaterial;
    public Sprite colorIcon;
    public string displayName; // shown on Dispatch's tickets (the asset's name when empty)
}
