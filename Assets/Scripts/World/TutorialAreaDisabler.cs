using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Disables the entire tutorial area when not use.
/// </summary>
public class TutorialAreaDisabler : MonoBehaviour
{
    [Tooltip("Area to be disabled.")]
    [SerializeField] private GameObject TutorialGO;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapBegin += DisableGround;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapBegin -= DisableGround;
    }

    private void DisableGround()
    {
        StartCoroutine(DisableCountdown());
    }

    private IEnumerator DisableCountdown()
    {
        yield return new WaitForSeconds(1f);
        TutorialGO.SetActive(false);
    }
}
