using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using DG.Tweening.Core.Easing;

public class MainMenu : SingletonMonobehaviour<MainMenu>
{
    GameManager gameManager;

    [Header("Selector Objects")]
    [SerializeField] internal GameObject selector;
    [SerializeField] internal GameObject[] selectorObjects;
    [SerializeField] internal Image menuGhostImage;
    [SerializeField] internal Sprite[] selectorGhostSprites;
    int selectorPos;

    [Header("Canvas Objects")]
    [SerializeField] internal Canvas PlayerSelectCanvas;
    [SerializeField] internal Canvas OptionsCanvas;
    [SerializeField] internal Canvas CreditsCanvas;

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

    internal void OnDisable()
    {
        if (gameManager != null)
        {
            gameManager.OnSwapPlayerSelect -= ShowPlayerSelect;
            gameManager.OnSwapMenu -= ShowTitleScreen;
        }
    }

    public void Start()
    {
        // The menu scene opens on the title screen. Online (after a match) it opens on player select, the lobby, where Y
        // invites and B leaves; on a client the host's state follows instead
        gameManager.SetGameState(GameAuthority.IsOnline ? GameState.PlayerSelect : GameState.Menu);
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

        // An entry added to the scene without its ghost sprite keeps the last one
        if (selectorPos < selectorGhostSprites.Length)
            menuGhostImage.sprite = selectorGhostSprites[selectorPos];

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    /// <summary>
    /// Whether the title screen has Online Play: the scene's entries decide it (TitleEntries)
    /// </summary>
    public bool HasOnlineEntry => selectorObjects != null && TitleEntries.HasOnline(selectorObjects.Length);

    public void ConfirmMenu()
    {
        Choose(TitleEntries.At(selectorPos, selectorObjects.Length));
    }

    internal void Choose(TitleEntry entry)
    {
        switch (entry)
        {
            case TitleEntry.Local:
                SwapToPlayerSelect();
                break;
            case TitleEntry.Online:
                SwapToOnline();
                break;
            case TitleEntry.Options:
                SwapToOptions();
                break;
            case TitleEntry.Credits:
                SwapToCredits();
                break;
            case TitleEntry.Quit:

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

    /// <summary>
    /// Online Play: player select, then a lobby with Steam's invite window (OnlinePlay). Player select comes first: a
    /// lobby still being made is kept only there. A failure comes back to the title screen
    /// </summary>
    public void SwapToOnline()
    {
        SwapToPlayerSelect();

        if (OnlinePlay.Instance != null)
            OnlinePlay.Instance.StartOnline();
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
