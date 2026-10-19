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
    [SerializeField] internal GameObject readyUpText;
    [SerializeField] TutorialToggle tutorialToggle;

    private SoundPool soundPool;

    // Dispatch (online player select): X opens the customization sliders. Offline it does nothing
    private readonly DispatchMenu dispatchMenu = new DispatchMenu();

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
        dispatchMenu.Reset();
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
        uiHandler.NorthFaceEvent.RemoveAllListeners();
        uiHandler.WestFaceEvent.RemoveAllListeners();
        uiHandler.LeftShoulderEvent.RemoveAllListeners();
        uiHandler.RightShoulderEvent.RemoveAllListeners();

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
        uiHandler.SouthFaceEvent.AddListener(OptionsConfirm);

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

        RefreshDispatchSliders();

        uiHandler.SouthFaceEvent.AddListener(PlayerReadyButton);
        uiHandler.EastFaceEvent.AddListener(PlayerUnreadyDespawnButton);
        uiHandler.NorthFaceEvent.AddListener(PlayOnline);
        uiHandler.WestFaceEvent.AddListener(PlayerCustomize);
        uiHandler.LeftShoulderEvent.AddListener(FindMatchButton);
        uiHandler.RightShoulderEvent.AddListener(FillButton);

        uiHandler.DownPadEvent.AddListener(CustomizationScrollDownButton);
        uiHandler.UpPadEvent.AddListener(CustomizationScrollUpButton);
        uiHandler.RightPadEvent.AddListener(CustomizationScrollRightButton);
        uiHandler.LeftPadEvent.AddListener(CustomizationScrollLeftButton);
    }

    ///<summary>
    /// Shows the customization sliders by Dispatch's rule: online, only while X has them open. Offline they are always
    /// there. Safe to call any time (no sliders, offline, not in player select)
    ///</summary>
    public void RefreshDispatchSliders()
    {
        if (customizationSelector == null)
            return;

        if (curentMenuType != MenuType.PlayerSelect)
            return;

        customizationSelector.gameObject.SetActive(!(GameAuthority.IsOnline || Switching()) || dispatchMenu.Open);
    }

    ///<summary>
    /// Whether a lobby switch (Find) has this machine between sessions: offline for a moment, but still in Dispatch. Its
    /// buttons wait meanwhile (X and the shoulders do nothing offline anyway), and B cancels the switch, so nothing falls
    /// through to Local Play
    ///</summary>
    private static bool Switching()
    {
        OnlinePlay play = OnlinePlay.Instance;
        return play != null && play.Match != null && play.Match.Switching;
    }

    ///<summary>
    /// X in player select: online, opens or closes the sliders
    ///</summary>
    private void PlayerCustomize(bool button)
    {
        if (!GameAuthority.IsOnline)
            return;

        DispatchAction action = dispatchMenu.Press(DispatchButton.West, readiedUp);
        if (action == DispatchAction.None)
            return;

        RefreshDispatchSliders();
        PlayDispatchSound(action == DispatchAction.OpenSliders);
    }

    ///<summary>
    /// Left shoulder in player select, online: looks for a match for the host's lobby (Matchmaker.Find)
    ///</summary>
    private void FindMatchButton(bool button)
    {
        if (!GameAuthority.IsOnline)
            return;

        if (dispatchMenu.Press(DispatchButton.LeftShoulder, readiedUp) != DispatchAction.Find)
            return;

        OnlinePlay play = OnlinePlay.Instance;
        if (play == null || play.Match == null)
            return;

        if (play.Match.Find())
            PlayDispatchSound(true);
    }

    ///<summary>
    /// Right shoulder in player select, online: the host opens the lobby to anyone or back to friends only. A client gets
    /// nothing (no action, no sound)
    ///</summary>
    private void FillButton(bool button)
    {
        if (!GameAuthority.IsOnline)
            return;

        if (dispatchMenu.Press(DispatchButton.RightShoulder, readiedUp) != DispatchAction.Fill)
            return;

        OnlinePlay play = OnlinePlay.Instance;
        if (play == null || play.Lobby == null || !play.Lobby.IsHost)
            return;

        play.Lobby.SetFill(!play.Lobby.Filling);
        PlayDispatchSound(true);
    }

    private void PlayDispatchSound(bool enter)
    {
        if (soundPool == null)
            return;

        if (enter)
            soundPool.PlayEnterUI();
        else
            soundPool.PlayBackUI();
    }

    ///<summary>
    /// A in player select. Online, it also closes the sliders it readies from. During a lobby switch it does nothing
    ///</summary>
    private void PlayerReadyButton(bool button)
    {
        if (Switching())
            return;

        if (!GameAuthority.IsOnline)
        {
            PlayerReady(button);
            return;
        }

        switch (dispatchMenu.Press(DispatchButton.South, readiedUp))
        {
            case DispatchAction.CloseAndReady:
                RefreshDispatchSliders();
                PlayerReady(button);
                break;
            case DispatchAction.Ready:
                PlayerReady(button);
                break;
        }
    }

    ///<summary>
    /// Online: readies this player as A in Dispatch does (closing the sliders), when an open lobby starts by itself
    ///</summary>
    public void ReadyAsA()
    {
        PlayerReadyButton(true);
    }

    ///<summary>
    /// B in player select. Online, it closes the sliders first, then unreadies, then leaves. During a lobby switch it
    /// cancels the switch (not back to the title screen)
    ///</summary>
    private void PlayerUnreadyDespawnButton(bool button)
    {
        OnlinePlay play = OnlinePlay.Instance;
        if (Switching())
        {
            play.Match.Cancel();
            PlayDispatchSound(false);
            return;
        }

        if (!GameAuthority.IsOnline)
        {
            PlayerUnreadyDespawn(button);
            return;
        }

        bool searching = play != null && play.Match != null && play.Match.Searching;
        switch (dispatchMenu.Press(DispatchButton.East, readiedUp, searching))
        {
            case DispatchAction.CancelFind:
                play.Match.Cancel();
                PlayDispatchSound(false);
                break;
            case DispatchAction.CloseSliders:
                RefreshDispatchSliders();
                PlayDispatchSound(false);
                break;
            case DispatchAction.Unready:
            case DispatchAction.Leave:
                PlayerUnreadyDespawn(button);
                break;
        }
    }

    ///<summary>
    /// A d-pad press in player select. Offline it always scrolls the sliders; online, only while X has them open. During
    /// a lobby switch, never
    ///</summary>
    private bool DispatchScrolls()
    {
        if (Switching())
            return false;

        return !GameAuthority.IsOnline || dispatchMenu.Press(DispatchButton.Pad, readiedUp) == DispatchAction.Scroll;
    }

    private void CustomizationScrollDownButton(bool button)
    {
        if (DispatchScrolls())
            CustomizationScrollDown(button);
    }

    private void CustomizationScrollUpButton(bool button)
    {
        if (DispatchScrolls())
            CustomizationScrollUp(button);
    }

    private void CustomizationScrollRightButton(bool button)
    {
        if (DispatchScrolls())
            CustomizationScrollRight(button);
    }

    private void CustomizationScrollLeftButton(bool button)
    {
        if (DispatchScrolls())
            CustomizationScrollLeft(button);
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
    /// Y in player select: online, invites friends (Steam's invite window). In a title screen without Online Play,
    /// offline, it goes online (OnlinePlay.YAction)
    ///</summary>
    private void PlayOnline(bool button)
    {
        if (OnlinePlay.Instance != null)
            OnlinePlay.Instance.PressY(MainMenu.Instance != null && MainMenu.Instance.HasOnlineEntry);
    }

    ///<summary>
    /// Calls method when player wants to unready or despawn
    ///</summary>
    private void PlayerUnreadyDespawn(bool button)
    {
        // Despawn
        if (readiedUp == false)
        {
            // Online, this machine's menu player's B leaves the match (the host's ends it for everyone) and goes back to the
            // title screen. Another player here leaves player select, as offline, and gives their seat back
            if (GameAuthority.IsOnline && hostPlayer)
            {
                OnlineGame.LeaveOnline();
                MainMenu.Instance.SwapToMainMenu();
                return;
            }

            if(hostPlayer == false)
            {
                PlayerInstantiate.Instance.RemovePlayerRef(GetComponentInParent<PlayerInput>());
            }
            else
            {
                // Goes back to main menu
                MainMenu.Instance.SwapToMainMenu();
            }
        }
        else
        {
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
    /// A on the Options Menu: applies the Resolution row's size (the other rows apply as they change)
    ///</summary>
    private void OptionsConfirm(bool button)
    {
        if (hostPlayer == false)
            return;

        if (optionsMenu == null)
            optionsMenu = OptionsMenu.Instance;

        optionsMenu.Confirm();

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
