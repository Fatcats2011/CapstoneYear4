using UnityEngine;

/// <summary>
/// Another machine's player on this machine: their scooter (PlayerAvatar.prefab) with no view: no cameras, menus or
/// controller. What only the player's own machine runs is off: controls and physics (BallDriving; the ball is
/// kinematic), falling in water (Respawn) and the horn gauge (PhaseIndicator, whose sliders live in a view). It stays
/// where the online game puts it, dressed as the player. See docs/online.md
/// </summary>
public class RemoteAvatar : MonoBehaviour
{
    /// <summary>The scooter's controls (switched off here)</summary>
    public BallDriving Driving { get; private set; }

    /// <summary>
    /// Makes another machine's player's scooter under parent (null = the scene root). It's built switched off, so none
    /// of its scripts start before the parts that belong to the player's own machine are off
    /// </summary>
    public static RemoteAvatar Create(GameObject avatarPrefab, Transform parent)
    {
        GameObject holder = new GameObject("Remote Avatar (being built)");
        holder.SetActive(false);
        GameObject avatar = Instantiate(avatarPrefab, holder.transform);
        RemoteAvatar remote = avatar.AddComponent<RemoteAvatar>();
        remote.TurnOffOwnerParts();
        avatar.transform.SetParent(parent, false);
        Destroy(holder);
        return remote;
    }

    void TurnOffOwnerParts()
    {
        Driving = GetComponentInChildren<BallDriving>(true);
        Driving.enabled = false;
        foreach (Respawn respawn in GetComponentsInChildren<Respawn>(true))
            respawn.enabled = false;
        foreach (PhaseIndicator horns in GetComponentsInChildren<PhaseIndicator>(true))
            horns.enabled = false;

        Rigidbody ball = Driving.Sphere.GetComponent<Rigidbody>();
        ball.isKinematic = true;
        ball.interpolation = RigidbodyInterpolation.None;
    }

    public void ShowCompany(CompanyInformation company)
    {
        ScooterLook.ShowCompany(gameObject, company);
    }

    public void ShowColour(PlayerColorInformationSO colour)
    {
        ScooterLook.ShowColour(gameObject, colour);
    }

    public void ShowHat(PlayerHatInformationSO hat)
    {
        ScooterLook.ShowHat(gameObject, hat);
    }
}
