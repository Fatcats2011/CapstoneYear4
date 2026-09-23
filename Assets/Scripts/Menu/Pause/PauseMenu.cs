using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PauseMenu : MonoBehaviour
{
    public bool goToMainMenu = false;

    [SerializeField] GameObject tint;
    
    public enum PauseType
    {
        Host,
        Sub
    }
    public PauseType pauseType = PauseType.Sub;

    [Header("Sub Pause")]
    [SerializeField] GameObject subPauseGO;

    [Header("Host Pause")]
    [SerializeField] GameObject hostPauseGO;
    [SerializeField] GameObject selector;
    [SerializeField] GameObject[] selectorObjects;
    int selectorPos;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapPlayerSelect += ResetMenu;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapPlayerSelect -= ResetMenu;
    }

    public void OnPause(PauseType pauseType)
    {
        tint.SetActive(true);

        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[0].transform.position.y, selector.transform.position.z);

        switch (pauseType)
        {
            case PauseType.Host:
                hostPauseGO.SetActive(true);
                subPauseGO.SetActive(false);
                return;
            case PauseType.Sub:
                hostPauseGO.SetActive(false);
                subPauseGO.SetActive(true);
                return;
            default: return;
        }

    }

    public void OnPlay()
    {
        hostPauseGO.SetActive(false);
        subPauseGO.SetActive(false);

        tint.SetActive(false);

        SoundManager.Instance.ChangeSnapshot("gameplay");
    }

    public void ScrollMenu(bool direction)
    {
        // Positive Scroll
        if (direction)
        {
            if (selectorPos == selectorObjects.Length - 1)
            {
                selectorPos = 0;
            }
            else
            {
                selectorPos = selectorPos + 1;
            }
        }
        // Negative Scroll
        else
        {
            if (selectorPos == 0)
            {
                selectorPos = selectorObjects.Length - 1;
            }
            else
            {
                selectorPos = selectorPos - 1;
            }
        }

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    public void ConfirmMenu()
    {
        if (goToMainMenu == true)
            return;

        goToMainMenu = true;

        switch (selectorPos)
        {
            // Resume
            case 0:
                PlayerInstantiate.Instance.PlayerPlay();
                ResetMenu();
                break;
            // Main Menu
            case 1:
                ReturnToMenu();
                break;
        }
    }

    private void ReturnToMenu()
    {
        SoundManager.Instance.ChangeSnapshot("gameplay");
        PlayerInstantiate.Instance.PlayerPlay();
        SceneFlow.Current.ReturnToMenu();
        selectorPos = 0;
        //GameManager.Instance.SetGameState(GameState.Menu);
    }

    public void ResetMenu()
    {
        goToMainMenu = false;
    }
}
