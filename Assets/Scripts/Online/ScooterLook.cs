using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Dresses a scooter (a PlayerAvatar) as a player: their company's scooter colours, logos and indicator icons, their
/// ghost colour and their hat. A player on this machine is dressed by its view (PlayerCameraResizer,
/// CustomizationSelector); another machine's player has no view, so online play dresses its scooter with this. The paths
/// are the parts those view scripts dress (ScooterLookTests checks they still match). At runtime only: renderer
/// materials are copied as in the view scripts
/// </summary>
public static class ScooterLook
{
    public const string MODEL = "Control/Groundcheck/Basket/SubBasket/ScooterBlockOut_01";
    public const string SCOOTER_BODY = MODEL + "/Scooter Object/Scooter";
    public const string LOGO = MODEL + "/Logo Decal";
    public const string LOGO_2 = MODEL + "/Logo Decal 2";
    public const string GHOST = MODEL + "/Player Model/Player_Model";
    public const string EYELID_LEFT = MODEL + "/Ghost Model/Eyelid Left";
    public const string EYELID_RIGHT = MODEL + "/Ghost Model/Eyelid Right";
    public const string HAT = MODEL + "/Player Model/Armature.001/Torso/Head/Hat Object";

    /// <summary>
    /// The company's scooter colour, logos and the icons other players see over the scooter (as PlayerCameraResizer.InitalizeCompanyScooter)
    /// </summary>
    public static void ShowCompany(GameObject avatar, CompanyInformation company)
    {
        Transform root = avatar.transform;
        root.Find(SCOOTER_BODY).GetComponent<MeshRenderer>().material = company.scooterColorMaterial;
        root.Find(LOGO).GetComponent<DecalProjector>().material = company.scooterDecalMaterial;
        root.Find(LOGO_2).GetComponent<DecalProjector>().material = company.scooterDecalMaterial;
        avatar.GetComponentInChildren<DrivingIndicators>(true).SetDrivingIndicatorsToCompany(company.playerIndicatorSprites);
    }

    /// <summary>
    /// The ghost's colour on its body and eyelids (as CustomizationSelector.UpdateGhostColor). The horns keep their
    /// plain material: their glow shows this machine's boost gauge, which another machine's player doesn't have here
    /// </summary>
    public static void ShowColour(GameObject avatar, PlayerColorInformationSO colour)
    {
        Transform root = avatar.transform;
        SkinnedMeshRenderer ghost = root.Find(GHOST).GetComponent<SkinnedMeshRenderer>();
        Material[] materials = ghost.materials;
        materials[0] = colour.colorMaterial;
        ghost.materials = materials;
        root.Find(EYELID_LEFT).GetComponent<MeshRenderer>().material = colour.colorMaterial;
        root.Find(EYELID_RIGHT).GetComponent<MeshRenderer>().material = colour.colorMaterial;
    }

    /// <summary>
    /// The hat, or none (as CustomizationSelector.UpdateHat)
    /// </summary>
    public static void ShowHat(GameObject avatar, PlayerHatInformationSO hat)
    {
        MeshRenderer hatModel = avatar.transform.Find(HAT).GetComponent<MeshRenderer>();
        hatModel.gameObject.SetActive(hat.displayHat);
        if (!hat.displayHat)
            return;

        hatModel.GetComponent<MeshFilter>().mesh = hat.hatMesh;
        hatModel.material = hat.hatMaterial;
        hatModel.transform.localPosition = hat.hatPosition;
        hatModel.transform.localRotation = Quaternion.Euler(hat.hatRotation);
        hatModel.transform.localScale = hat.hatScale;
    }
}
