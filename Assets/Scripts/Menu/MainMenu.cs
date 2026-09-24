using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using DG.Tweening.Core.Easing;

public class MainMenu : SingletonMonobehaviour<MainMenu>
{
    GameManager gameManager;

    [Header("Selector Objects")]
    [SerializeField] GameObject selector;
    [SerializeField] GameObject[] selectorObjects;
    [SerializeField] Image menuGhostImage;
    [SerializeField] Sprite[] selectorGhostSprites;
    int selectorPos;

    [Header("Canvas Objects")]
    [SerializeField] Canvas PlayerSelectCanvas;
    [SerializeField] Canvas OptionsCanvas;
    [SerializeField] Canvas CreditsCanvas;

    [SerializeField] TMP_Text p1ConnectedController;
    PlayerInstantiate playerInstantiate;

    public void OnEnable()
    {
        gameManager = GameManager.Instance;

        // The menus follow the game state, whoever switched it: online, a client follows the host
        if (gameManager != null)
        {
            gameManager.OnSwapPlayerSelect += ShowPlayerSelect;
            gameManager.OnSwapMenu += ShowTitleScreen;
        }
    }

    void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnSwapPlayerSelect -= ShowPlayerSelect;
            gameManager.OnSwapMenu -= ShowTitleScreen;
        }
    }

    public void Start()
    {
        // Set game to begin upon loading into scene
        gameManager.SetGameState(GameState.Menu);
    }

    public void Update()
    {
        if (DevTools.GetKeyDown(KeyCode.L))
        {
            if (playerInstantiate == null)
                playerInstantiate = PlayerInstantiate.Instance;

            playerInstantiate.ClearPlayerArray();
            ScoreManager.Instance.UpdateOrderHandlers(playerInstantiate.Roster);

            p1ConnectedController.text = "";

        }
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

        menuGhostImage.sprite = selectorGhostSprites[selectorPos];

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    public void ConfirmMenu()
    {
        switch (selectorPos)
        {
            // Play
            case 0:
                SwapToPlayerSelect();
                break;
            // Options
            case 1:
                SwapToOptions();
                break;
            case 2:
                SwapToCredits();
                break;
            // Quit
            case 3:

            #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
            #endif
                Application.Quit();
                break;
        }
    }

    public void SwapToPlayerSelect()
    {
        GameManager.Instance.SetGameState(GameState.PlayerSelect);
    }

    public void SwapToOptions()
    {
        OptionsCanvas.enabled = true;

        GameManager.Instance.SetGameState(GameState.Options);

        OptionsMenu.Instance.UpdateSelectors();

        selectorPos = 0;
        menuGhostImage.sprite = selectorGhostSprites[0];
        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    public void SwapToCredits()
    {
        CreditsCanvas.enabled = true;

        GameManager.Instance.SetGameState(GameState.Credits);

        CreditsMenu.Instance.BeginCredits();

        selectorPos = 0;
        menuGhostImage.sprite = selectorGhostSprites[0];
        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    public void SwapToMainMenu()
    {
        GameManager.Instance.SetGameState(GameState.Menu);
    }

    void ShowPlayerSelect()
    {
        PlayerSelectCanvas.enabled = true;
    }

    void ShowTitleScreen()
    {
        PlayerSelectCanvas.enabled = false;
        OptionsCanvas.enabled = false;
        CreditsCanvas.enabled = false;
    }

    public void Player1ControllerConnected(PlayerInput playerInput)
    {
        p1ConnectedController.text = playerInput.devices[0].name;
    }
}
