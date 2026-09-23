using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum MenuType
{
    MainMenu,
    Options,
    Credits,
    PlayerSelect,
    Loading,
    Cutscene,
    PauseMenu,
    ResultsMenu
}

public class MenuInteractions : MonoBehaviour
{
    [Header("Bool Information")]
    public bool hostPlayer;
    [SerializeField] bool readiedUp = false;
    private bool loadReady = false;
    public bool hostPause = false;

    [Header("Object References")]
    [SerializeField] PlayerInput playerInput;
    [SerializeField] PlayerUIHandler uiHandler;
    [SerializeField] InputManager drivingHandler;
    [SerializeField] BallDriving ballDriving;

    [Header("UI References")]
    [SerializeField] CustomizationSelector customizationSelector;
    public PauseMenu pauseMenu;
    [SerializeField] MainMenu mainMenu;
    [SerializeField] OptionsMenu optionsMenu;
    [SerializeField] CreditsMenu creditsMenu;
    [SerializeField] ResultsMenu resultsMenu;
    [SerializeField] GameObject readyUpText;
    [SerializeField] TutorialToggle tutorialToggle;

    private SoundPool soundPool;

    public MenuType curentMenuType;
    private MenuType cache;

    private void Update()
    {
        if(cache != curentMenuType)
        {
            SwapMenuType(curentMenuType);
        }
    }

    private void OnEnable()
    {
        soundPool = GetComponentInParent<SoundPool>();

        GameManager.Instance.OnSwapPlayerSelect += SwapToPlayerSelect;
        GameManager.Instance.OnSwapOptions += SwapToOptions;
        GameManager.Instance.OnSwapCredits += SwapToCredits;

        GameManager.Instance.OnSwapStartingCutscene += PlayerUnready;

        GameManager.Instance.OnSwapLoading += SwapToLoadingScreen;
    }

    private void OnDisable()
    {
        ClearMenuInputs();

        GameManager.Instance.OnSwapPlayerSelect -= SwapToPlayerSelect;
        GameManager.Instance.OnSwapOptions -= SwapToOptions;
        GameManager.Instance.OnSwapCredits -= SwapToCredits;

        GameManager.Instance.OnSwapStartingCutscene -= PlayerUnready;

        GameManager.Instance.OnSwapLoading -= SwapToLoadingScreen;
    }

    public void SwapToPlayerSelect()
    {
        SwapMenuType(MenuType.PlayerSelect);
    }

    public void SwapToOptions()
    {
        SwapMenuType(MenuType.Options);
    }

    public void SwapToCredits()
    {
        SwapMenuType(MenuType.Credits);
    }

    public void SwapToLoadingScreen()
    {
        SwapMenuType(MenuType.Loading);
    }

    public void SwapToCutscene()
    {
        SwapMenuType(MenuType.Cutscene);
    }

    public void SwapMenuType(MenuType curentMenuTypePass)
    {
        curentMenuType = curentMenuTypePass;
        cache = curentMenuTypePass;

        ClearMenuInputs();
        switch (curentMenuTypePass)
        {
            case MenuType.MainMenu:
                MainMenuInteractions();
                return;
            case MenuType.Options:
                OptionsInteractions();
                return;
            case MenuType.Credits:
                CreditsInteractions();
                return;
            case MenuType.PlayerSelect:
                CharacterSelectMenuInteractons();
                return;
            case MenuType.Loading:
                LoadingMenuInteractons();
                return;
            case MenuType.Cutscene:
                return;
            case MenuType.PauseMenu:
                PauseMenuInteractons();
                return;
            case MenuType.ResultsMenu:
                ResultsMenuInteractions();
                return;
        }
    }

    private void ClearMenuInputs()
    {
        customizationSelector.gameObject.SetActive(false);

        uiHandler.SouthFaceEvent.RemoveAllListeners();
        uiHandler.EastFaceEvent.RemoveAllListeners();

        uiHandler.DownPadEvent.RemoveAllListeners();
        uiHandler.UpPadEvent.RemoveAllListeners();
        uiHandler.RightPadEvent.RemoveAllListeners();
        uiHandler.LeftPadEvent.RemoveAllListeners();

        uiHandler.StartPadEvent.RemoveAllListeners();
        drivingHandler.StartPadEvent.RemoveAllListeners();
    }

    private void MainMenuInteractions()
    {
        // Reference main menu every time we swap
        mainMenu = MainMenu.Instance;

        uiHandler.DownPadEvent.AddListener(MainMenuScrollDown);
        uiHandler.UpPadEvent.AddListener(MainMenuScrollUp);

        uiHandler.SouthFaceEvent.AddListener(MainMenuConfirm);
    }

    private void OptionsInteractions()
    {
        // Reference options menu every time we swap
        optionsMenu = OptionsMenu.Instance;

        uiHandler.EastFaceEvent.AddListener(OptionsExit);

        uiHandler.DownPadEvent.AddListener(OptionsScrollDown);
        uiHandler.UpPadEvent.AddListener(OptionsScrollUp);
        uiHandler.RightPadEvent.AddListener(OptionsScrollRight);
        uiHandler.LeftPadEvent.AddListener(OptionsScrollLeft);
    }

    private void CreditsInteractions()
    {
        creditsMenu = CreditsMenu.Instance;

        uiHandler.EastFaceEvent.AddListener(CreditsExit);
    }

    private void CharacterSelectMenuInteractons()
    {

        customizationSelector.gameObject.SetActive(true);

        uiHandler.SouthFaceEvent.AddListener(PlayerReady);
        uiHandler.EastFaceEvent.AddListener(PlayerUnreadyDespawn);

        uiHandler.DownPadEvent.AddListener(CustomizationScrollDown);
        uiHandler.UpPadEvent.AddListener(CustomizationScrollUp);
        uiHandler.RightPadEvent.AddListener(CustomizationScrollRight);
        uiHandler.LeftPadEvent.AddListener(CustomizationScrollLeft);
    }

    private void LoadingMenuInteractons()
    {

        uiHandler.SouthFaceEvent.AddListener(PlayerConfirmLoad);
    }


    private void PauseMenuInteractons()
    {

        uiHandler.StartPadEvent.AddListener(PlayGame);
        drivingHandler.StartPadEvent.AddListener(PauseGame);

        uiHandler.DownPadEvent.AddListener(PauseScrollDown);
        uiHandler.UpPadEvent.AddListener(PauseScrollUp);

        uiHandler.SouthFaceEvent.AddListener(PauseConfirm);
    }


    private void ResultsMenuInteractions()
    {

        // Reference main menu every time we swap
        mainMenu = MainMenu.Instance;

        uiHandler.SouthFaceEvent.AddListener(ResultsMenuConfirm);
    }

    ///<summary>
    /// Calls method when player wants to ready
    ///</summary>
    private void PlayerReady(bool button)
    {
        if (!readiedUp)
            soundPool.PlayEnterUI();

        readyUpText.SetActive(true);
        readiedUp = true;

        PlayerInstantiate.Instance.ReadyUp(ballDriving.playerIndex - 1);
        
    }

    ///<summary>
    /// Calls method when player wants to unready or despawn
    ///</summary>
    private void PlayerUnreadyDespawn(bool button)
    {
        // Despawn
        if (readiedUp == false)
        {
            if(hostPlayer == false)
            {
                PlayerInstantiate.Instance.RemovePlayerRef(transform.parent.gameObject.transform.parent.GetComponent<PlayerInput>());
            }
            else
            {
                // Goes back to main menu
                MainMenu.Instance.SwapToMainMenu();
            }
        }
        else
        {
/*            if(hostPlayer) // outdated tutorial enabling stuff
            {
                tutorialToggle.gameObject.SetActive(false);
                uiHandler.SouthFaceEvent.RemoveListener(tutorialToggle.SetTutorial);
            }*/
            readyUpText.SetActive(false);
            readiedUp = false;
            PlayerInstantiate.Instance.UnreadyUp(ballDriving.playerIndex - 1);
            soundPool.PlayBackUI();
        }
    }

    ///<summary>
    /// Calls method when player wants to unready
    ///</summary>
    public void PlayerUnready()
    {
        if(readiedUp)
        {
            soundPool.PlayBackUI();
        }

        readyUpText.SetActive(false);
        readiedUp = false;
        PlayerInstantiate.Instance.UnreadyUp(ballDriving.playerIndex - 1);
    }

    ///<summary>
    /// Calls method when player wants to ready
    ///</summary>
    private void PlayerConfirmLoad(bool button)
    {
        if (!SceneFlow.Current.WaitingForConfirm && !loadReady)
            return;

        loadReady = true;
        soundPool.PlayEnterUI();
        PlayerInstantiate.Instance.LoadingConfirm(ballDriving.playerIndex - 1);
    }

    ///<summary>
    /// Calls method when player scrolls up on Main Menu
    ///</summary>
    private void MainMenuScrollUp(bool button)
    {
        if (hostPlayer == false)
            return;

        if (mainMenu == null)
            mainMenu = MainMenu.Instance;

        // Scrolls selector up
        mainMenu.ScrollMenu(false);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Main Menu
    ///</summary>
    private void MainMenuScrollDown(bool button)
    {
        if (hostPlayer == false)
            return;

        if (mainMenu == null)
            mainMenu = MainMenu.Instance;

        // Scrolls selector down
        mainMenu.ScrollMenu(true);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player confirms on the main menu
    ///</summary>
    private void MainMenuConfirm(bool button)
    {
        if (hostPlayer == false)
            return;

        if (mainMenu == null)
            mainMenu = MainMenu.Instance;

        // Scrolls selector down
        mainMenu.ConfirmMenu();

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls up on Options Menu
    ///</summary>
    private void OptionsScrollUp(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        // Scrolls selector up
        optionsMenu.ScrollMenuUpDown(false);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Options Menu
    ///</summary>
    private void OptionsScrollDown(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        // Scrolls selector down
        optionsMenu.ScrollMenuUpDown(true);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls left on options menu
    ///</summary>
    private void OptionsScrollLeft(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        // Scrolls selector up
        optionsMenu.ScrollMenuLeftRight(false);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls right on options menu
    ///</summary>
    private void OptionsScrollRight(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        // Scrolls selector down
        optionsMenu.ScrollMenuLeftRight(true);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player exits the options menu
    ///</summary>
    private void OptionsExit(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        // Scrolls selector down
        optionsMenu.ExitMenu();

        soundPool.PlayBackUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Main Menu
    ///</summary>
    private void CreditsExit(bool button)
    {
        if (hostPlayer == false)
            return;

        if (creditsMenu == null)
            creditsMenu = CreditsMenu.Instance;

        // Scrolls selector down
        creditsMenu.ExitMenu();

        soundPool.PlayBackUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Main Menu
    ///</summary>
    private void ResultsMenuConfirm(bool button)
    {
        if (hostPlayer == false)
            return;

        if (resultsMenu == null)
            resultsMenu = ResultsMenu.Instance;

        // Scrolls selector down
        resultsMenu.ConfirmMenu();

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on character customization
    ///</summary>
    private void CustomizationScrollDown(bool button)
    {
        // Scrolls selector down
        customizationSelector.SetCustomizationType(true);
        if(!readiedUp)
            soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls up on character customization
    ///</summary>
    private void CustomizationScrollUp(bool button)
    {
        // Scrolls selector up
        customizationSelector.SetCustomizationType(false);
        if(!readiedUp)
            soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls left on character customization
    ///</summary>
    private void CustomizationScrollLeft(bool button)
    {
        // Scrolls selector left
        customizationSelector.SetCustomizationValue(false);
        if(!readiedUp)
            soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls right on character customization
    ///</summary>
    private void CustomizationScrollRight(bool button)
    {
        // Scrolls selector right
        customizationSelector.SetCustomizationValue(true);
        if(!readiedUp)
            soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls up on Pause
    ///</summary>
    private void PauseScrollUp(bool button)
    {
        if (hostPause == false)
            return;

        // Scrolls selector up
        pauseMenu.ScrollMenu(false);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Pause
    ///</summary>
    private void PauseScrollDown(bool button)
    {
        if (hostPause == false)
            return;

        // Scrolls selector down
        pauseMenu.ScrollMenu(true);

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method when player scrolls down on Pause
    ///</summary>
    private void PauseConfirm(bool button)
    {
        if (hostPause == false)
            return;

        // Scrolls selector down
        pauseMenu.ConfirmMenu();

        soundPool.PlayScrollUI();
    }

    ///<summary>
    /// Calls method to reset canvas
    ///</summary>
    public void ResetCanvas()
    {
        readyUpText.SetActive(false);
        soundPool.PlayScrollUI();
    }

    public void PlayGame(bool button)
    {
        SoundManager.Instance.ChangeSnapshot("gameplay");

        PlayerInstantiate.Instance.PlayerPlay();
    }

    public void PauseGame(bool button)
    {
        soundPool.PlayPauseUI();
        SoundManager.Instance.ChangeSnapshot("paused");

        PlayerInstantiate.Instance.PlayerPause(playerInput);
    }

}
