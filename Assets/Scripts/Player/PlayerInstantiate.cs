using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

public class PlayerInstantiate : SingletonMonobehaviour<PlayerInstantiate>
{
    [SerializeField]
    Rect[] cameraRects;

    [Header("Player Info")]
    [SerializeField] GameManager gameManager;
    [SerializeField] GameObject playerHolder;
    [SerializeField] SceneManager sceneManager;

    [Space(10)]
    [Tooltip("Enables or disables the ability for players to spawn into the lobby")]
    [SerializeField] bool allowPlayerSpawn = true;

    // Who is in each of the 4 player slots
    readonly PlayerRoster roster = new PlayerRoster();
    public PlayerRoster Roster { get { return roster; } }

    // How many players have joined
    public int PlayerCount { get { return roster.Count; } }
    [Tooltip("The indexed array of player spawn positions")]
    [SerializeField] GameObject[] menuSpawnPositions = new GameObject[Constants.MAX_PLAYERS];

    [Tooltip("The indexed array of player render texutres")]
    [SerializeField] RenderTexture[] playerRenderTextures = new RenderTexture[Constants.MAX_PLAYERS];

    [Header("Company Information")]
    [SerializeField] CompanyInformation[] companies;

    [Header("Ready Up Information")]
    [Tooltip("The indexed array tracking players' ready up status")]
    [SerializeField] bool[] playerReadyUp = new bool[Constants.MAX_PLAYERS];
    public event Action OnReadiedUp;
    int readyUpCounter = 0;
    [Tooltip("Set to true when all players are readied up")]
    [SerializeField] bool isAllReadedUp = false;
    Coroutine readyUpCountdown;

    [Header("Loading Screen Ready Information")]
    [Tooltip("The indexed array tracking players' loading screen ready status")]
    [SerializeField] bool[] playerLoadingConfirm = new bool[Constants.MAX_PLAYERS];

    [Header("Other")]
    private Gamepad[] playerGamepads = new Gamepad[Constants.MAX_PLAYERS];
    public Gamepad[] PlayerGamepads => playerGamepads;
    ReplacementControllerListener replacementListener;
    ReplacementControllerListener ReplacementListener =>
        replacementListener ??= new ReplacementControllerListener(GiveControllerToMissingPlayer, ShowUnsupportedDeviceHint);
    CutsceneManager cutsceneManager;

    // Online: the seat the host gave this machine's one player (-1 offline)
    int onlineSeat = -1;
       
    ///<summary>
    /// OnEnable, where i set event methods
    ///</summary>
    public void OnEnable()
    {
        gameManager.OnSwapPlayerSelect += EnablePlayerSpawn;
        gameManager.OnSwapPlayerSelect += SwapForCharacterSelect;

        gameManager.OnSwapLoading += DisablePlayerSpawn;

        gameManager.OnSwapStartingCutscene += PlayerUpdateDrivingIndicators;

        gameManager.OnSwapTutorial += SwapForDriving;

        gameManager.OnSwapStartingCutscene += SwapForCutscene;
        gameManager.OnSwapGoldenCutscene += SwapForCutscene;
        gameManager.OnSwapGoldenCutscene += PlayerUpdateDrivingIndicators;

        gameManager.OnSwapFinalPackage += SwapForDriving;

        gameManager.OnSwapResults += DisableReadiedUp;
        gameManager.OnSwapResults += ResetPlayerCanvas;
        gameManager.OnSwapResults += SwapForResults;

        gameManager.OnSwapMenu += SwapForMainMenu;

        // When players confirm load, disable all confirm bools
        sceneManager.OnConfirmToLoad += DisableLoadingConfirmCount;
    }

    ///<summary>
    /// OnDisable, where i set event methods
    ///</summary>
    public void OnDisable()
    {
        gameManager.OnSwapPlayerSelect -= EnablePlayerSpawn;
        gameManager.OnSwapPlayerSelect -= SwapForCharacterSelect;

        gameManager.OnSwapLoading -= DisablePlayerSpawn;

        gameManager.OnSwapStartingCutscene -= PlayerUpdateDrivingIndicators;

        gameManager.OnSwapTutorial -= SwapForDriving;

        gameManager.OnSwapStartingCutscene -= SwapForCutscene;
        gameManager.OnSwapGoldenCutscene -= SwapForCutscene;
        gameManager.OnSwapGoldenCutscene -= PlayerUpdateDrivingIndicators;

        gameManager.OnSwapFinalPackage -= SwapForDriving;

        gameManager.OnSwapResults -= DisableReadiedUp;
        gameManager.OnSwapResults -= ResetPlayerCanvas;
        gameManager.OnSwapResults -= SwapForResults;
        //gameManager.OnSwapResults -= SetAllPlayerSpawn;

        gameManager.OnSwapMenu -= SwapForMainMenu;

        // When players confirm load, disable all confirm bools
        sceneManager.OnConfirmToLoad -= DisableLoadingConfirmCount;

        // Stops waiting for replacement controllers
        replacementListener?.SetListening(false);
    }

    ///<summary>
    /// Event to add the player references and rename player
    ///</summary>
    public void AddPlayerReference(PlayerInput playerInput)
    {
        if(playerInput.currentControlScheme != "Gamepad")
        {
            // Only controllers can play: outside a match, tell whoever pressed a key or an unsupported controller why nothing happened
            if (allowPlayerSpawn || PlayerCount == 0)
                ShowUnsupportedDeviceHint(playerInput.devices.Count > 0 ? playerInput.devices[0] : null);

            Destroy(playerInput.gameObject);
            return;
        }

        // Online, this machine has one player, in the seat the host gave them
        bool online = onlineSeat >= 0;
        if (online && roster.LocalCount > 0)
        {
            Destroy(playerInput.gameObject);
            return;
        }

        // If player spawn is disabled
        if(!online && allowPlayerSpawn == false && PlayerCount >= 1)
        {

            Destroy(playerInput.gameObject);
            return;
        }


        bool isFirstPlayer = online || PlayerCount == 0;

        // Takes the lowest free slot (online: the seat)
        PlayerSlot slot = online ? roster.JoinLocalAt(playerInput, onlineSeat) : roster.JoinLocal(playerInput);
        if (slot == null)
        {
            Destroy(playerInput.gameObject);
            return;
        }

        if (isFirstPlayer)
        {
            MenuInteractions menus = playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions;
            menus.hostPlayer = true;

            // Online, this machine's player can arrive (or move seats) while the host is in player select
            if (online && gameManager.MainState == GameState.PlayerSelect)
                menus.SwapToPlayerSelect();
            else
            {
                menus.SwapMenuType(MenuType.MainMenu);
                MainMenu.Instance.Player1ControllerConnected(playerInput);
            }
        }
        else
        {
            playerInput.gameObject.GetComponent<PlayerUIHandler>().menuInteractions.SwapToPlayerSelect();
        }

        GameObject ColliderObject = playerInput.gameObject.GetComponentInChildren<SphereCollider>().gameObject;
        BallDriving ballDriving = playerInput.gameObject.GetComponentInChildren<BallDriving>();
        Camera baseCam = playerInput.camera;
        PlayerCameraResizer playerCameraResizer = playerInput.gameObject.GetComponentInChildren<PlayerCameraResizer>();

        int nextFillSlot = slot.Index + 1;
        slot.Company = companies[slot.Index];

        // Update tag of player
        switch (nextFillSlot)
        {
            case 1:
                ColliderObject.layer = 10; // Player 1;
                ballDriving.playerIndex = 1;
                baseCam.cullingMask |= (1 << 10);
                playerCameraResizer.PlayerRenderCamera.targetTexture = playerRenderTextures[0];
                playerGamepads[0] = playerInput.GetDevice<Gamepad>();
                break;
            case 2:
                ColliderObject.layer = 11; // Player 2;
                ballDriving.playerIndex = 2;
                baseCam.cullingMask |= (1 << 11);
                playerCameraResizer.PlayerRenderCamera.targetTexture = playerRenderTextures[1];
                playerGamepads[1] = playerInput.GetDevice<Gamepad>();
                break;
            case 3:
                ColliderObject.layer = 12; // Player 3;
                ballDriving.playerIndex = 3;
                baseCam.cullingMask |= (1 << 12);
                playerCameraResizer.PlayerRenderCamera.targetTexture = playerRenderTextures[2];
                playerGamepads[2] = playerInput.GetDevice<Gamepad>();
                break;
            case 4:
                ColliderObject.layer = 13; // Player 4;
                ballDriving.playerIndex = 4;
                baseCam.cullingMask |= (1 << 13);
                playerCameraResizer.PlayerRenderCamera.targetTexture = playerRenderTextures[3];
                playerGamepads[3] = playerInput.GetDevice<Gamepad>();
                break;
        }

        playerCameraResizer.InitalizeCompanyScooter(slot.Company);

        // Updates the main virtual camera based on the player number
        playerCameraResizer.UpdateMainVirtualCameras(nextFillSlot);

        // Updates the icon virtual cameras based on the player number
        playerCameraResizer.UpdateIconVirtualCameras(nextFillSlot);

        // Relocates the customization menu based on player number
        playerCameraResizer.RelocateCustomizationMenu(nextFillSlot);

        // Update the naming scheme of the input reciever
        playerInput.gameObject.name = "P" + nextFillSlot.ToString();
        // Its scooter's top object (PlayerAvatar) gets the same name: the leaderboard and the results screen show it
        ballDriving.transform.parent.name = playerInput.gameObject.name;
        playerInput.gameObject.transform.parent = playerHolder.transform;
        // assign a company to each player
        playerInput.gameObject.GetComponentInChildren<OrderHandler>().CompanyInfo = slot.Company;

        // Pauses the match if this player's controller disconnects, and clears the reconnect message when it comes back
        playerInput.deviceLostEvent.AddListener(OnPlayerControllerLost);
        playerInput.deviceRegainedEvent.AddListener(OnPlayerControllerRegained);

        // Updates all the player's cameras due to this new player
        UpdatePlayerCameraRects();

        // Swaps the player's control scheme to UI
        SwapPlayerInputControlSchemeToUI();

        // Sets the player's spawn point above ground on map
        SetAllPlayerSpawn();

        // Unreadys up the player joining, which stops the countdown if it is currently counting
        UnreadyUp(nextFillSlot - 1);

        // Disables text for fillslot text based on player's fill slot
        PlayerSelectCanvas.Instance.TogglePressButtonTexts(nextFillSlot - 1, false);
    }

    ///<summary>
    /// Spawns each player at a set position
    ///</summary>
    public void SetAllPlayerSpawn()
    {
        // Loops for all spawned players
        foreach (PlayerSlot slot in roster.Players)
            PlaceOnPodium(slot);
    }

    ///<summary>
    /// Stands a player's scooter on their slot's podium
    ///</summary>
    private void PlaceOnPodium(PlayerSlot slot)
    {
        int i = slot.Index;
        Rigidbody ball = slot.Player.GetComponentInChildren<Rigidbody>();

        // Resets the velocity of the players (another machine's scooter is kinematic: nothing to reset)
        if (!ball.isKinematic)
            ball.velocity = Vector3.zero;

        // reset position and rotation of ball and controller
        ball.transform.position = menuSpawnPositions[i].transform.position;
        ball.transform.rotation = menuSpawnPositions[i].transform.rotation;

        slot.Player.GetComponentInChildren<BallDriving>(true).transform.position = menuSpawnPositions[i].transform.position;
        slot.Player.GetComponentInChildren<BallDriving>(true).transform.rotation = menuSpawnPositions[i].transform.rotation;
    }

    ///<summary>
    /// Updates the camera rects on all players in scenes
    ///</summary>
    private void UpdatePlayerCameraRects()
    {
        // Only players on this machine have a split-screen view
        cameraRects = SplitScreenLayout.CalculateRects(roster.LocalCount);

        int cameraRectCounter = 0;

        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.camera.rect = cameraRects[cameraRectCounter];
            cameraRectCounter++;
        }
    }

    ///<summary>
    /// Event to remove the player from references
    ///</summary>
    public void RemovePlayerRef(PlayerInput playerInput)
    {
        // If player spawn is disabled
        if (allowPlayerSpawn == false && Constants.SPAWN_MID_MATCH == false)
        {
            return;
        }

        LeaveLocal(playerInput);
    }

    ///<summary>
    /// A player on this machine leaves: their slot is free and their player object goes
    ///</summary>
    private void LeaveLocal(PlayerInput playerInput)
    {
        int position = roster.Leave(playerInput);

        //Enabled text for fillslot text based on player's removed position
        if (position >= 0)
            PlayerSelectCanvas.Instance.TogglePressButtonTexts(position, true);

        ScoreManager.Instance.UpdateOrderHandlers(roster);
        
        Destroy(playerInput.gameObject);

        UpdatePlayerCameraRects();

        // A player who leaves while disconnected takes their reconnect message with them, and the others' views moved
        UpdateControllerPrompts();

        CheckReadyUpCount();
    }

    ///<summary>
    /// Destroys all connected players
    ///</summary>
    public void ClearPlayerArray()
    {
        foreach (PlayerSlot slot in roster.Players)
            Destroy(slot.Player);
        roster.Clear();

        UpdateControllerPrompts();
    }

    ///<summary>
    /// The company a slot plays for
    ///</summary>
    public CompanyInformation CompanyForSlot(int slot)
    {
        return companies[slot];
    }

    ///<summary>
    /// Online: another machine's player takes their seat here. Their scooter (made with RemoteAvatar) stands on the
    /// seat's podium in the seat's company colours, named like a local player. Returns their slot, or null when the seat
    /// is taken
    ///</summary>
    public PlayerSlot AddRemotePlayer(GameObject avatar, int seat, ulong ownerClientId)
    {
        PlayerSlot slot = roster.JoinRemoteAt(avatar, ownerClientId, seat);
        if (slot == null)
            return null;

        slot.Company = companies[seat];
        BallDriving ballDriving = avatar.GetComponentInChildren<BallDriving>(true);
        ballDriving.Sphere.layer = 10 + seat; // their ball's player layer, as for a local player
        ballDriving.playerIndex = seat + 1;
        avatar.name = "P" + (seat + 1);
        avatar.transform.SetParent(playerHolder.transform);
        avatar.GetComponentInChildren<OrderHandler>(true).CompanyInfo = slot.Company;
        ScooterLook.ShowCompany(avatar, slot.Company);
        PlaceOnPodium(slot);

        // Like a local player joining: their seat isn't ready, which stops the countdown
        SetRemoteReady(seat, false);
        PlayerSelectCanvas.Instance.TogglePressButtonTexts(seat, false);
        return slot;
    }

    ///<summary>
    /// Online: another machine's player left. Their seat is free and the ready count is redone
    ///</summary>
    public void RemoveRemotePlayer(int seat)
    {
        PlayerSlot slot = roster[seat];
        if (slot == null || slot.IsLocal)
            return;

        roster.LeaveSlot(seat);
        playerReadyUp[seat] = false;
        PlayerSelectCanvas.Instance.TogglePressButtonTexts(seat, true);
        Destroy(slot.Player);
        ScoreManager.Instance.UpdateOrderHandlers(roster);
        CheckReadyUpCount();
    }

    ///<summary>
    /// Whether a slot's player is ready in player select
    ///</summary>
    public bool IsReady(int slot)
    {
        return playerReadyUp[slot];
    }

    ///<summary>
    /// Online: another machine's player readied up or stopped being ready (they have no menus here)
    ///</summary>
    public void SetRemoteReady(int slot, bool ready)
    {
        playerReadyUp[slot] = ready;
        if (ready)
            CheckReadyUpCount();
        else
            StopReadyUpCountdown();
    }

    ///<summary>
    /// Sets the index player to ready
    ///</summary>
    public void ReadyUp(int playerIndexToReadyUp)
    {
        playerReadyUp[playerIndexToReadyUp] = true;
        roster[playerIndexToReadyUp].Input.GetComponent<PlayerUIHandler>().customizationSelector.SetDisableOptionsCustomization(true);
        CheckReadyUpCount();
    }

    ///<summary>
    /// Sets the index player to unready
    ///</summary>
    public void UnreadyUp(int playerIndexToReadyUp)
    {
        playerReadyUp[playerIndexToReadyUp] = false;
        roster[playerIndexToReadyUp].Input.GetComponent<PlayerUIHandler>().customizationSelector.SetDisableOptionsCustomization(false);
        StopReadyUpCountdown();
    }

    ///<summary>
    /// Stops the ready-up countdown if it's counting
    ///</summary>
    private void StopReadyUpCountdown()
    {
        if (readyUpCountdown != null)
        {
            PlayerSelectCanvas.Instance.StopCountdown();
            StopCoroutine(readyUpCountdown);
            readyUpCountdown = null;
        }
    }

    ///<summary>
    /// Disables all player's bools of readied up
    ///</summary>
    public void DisableReadiedUp()
    {
        isAllReadedUp = false;

        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            playerReadyUp[i] = false;
        }
    }

    ///<summary>
    /// Checks for how many players are readied up
    ///</summary>
    public void CheckReadyUpCount()
    {
        // Resets and loops to count amount of ready ups
        readyUpCounter = 0;
        foreach (bool readyUpValue in playerReadyUp)
        {
            if (readyUpValue)
                readyUpCounter++;
        }

        // Checks if players are greater then 1 and all players are readied up
        if (readyUpCounter >= PlayerCount && PlayerCount >= 1)
        {
            if(readyUpCountdown == null)
                readyUpCountdown = StartCoroutine(ReadyUpCountdown());
        }
        else
        {
            return;
        }
    }

    ///<summary>
    /// The countdown ready-up coroutine that begins the game
    ///</summary>
    private IEnumerator ReadyUpCountdown()
    {
        if(PlayerSelectCanvas.Instance != null)
        {
            PlayerSelectCanvas.Instance.BeginCountdown();
        }
        yield return new WaitForSeconds(3f);

        isAllReadedUp = true;
        OnReadiedUp?.Invoke();
        readyUpCountdown = null;
    }

    ///<summary>
    /// Sets the player index for loading confirm
    ///</summary>
    public void LoadingConfirm(int playerIndexToReadyUp)
    {
        // Does not load confirm platy
        if (SceneFlow.Current.WaitingForConfirm == false)
            return;

        LoadingScreenManager.Instance.ConfirmButton(playerIndexToReadyUp);

        playerLoadingConfirm[playerIndexToReadyUp] = true;
        CheckLoadingConfirmCount();
    }

    ///<summary>
    /// Disables all player's bools of readied up
    ///</summary>
    public void DisableLoadingConfirmCount()
    {
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            playerLoadingConfirm[i] = false;
        }
    }

    ///<summary>
    /// Checks for how many players are readied up
    ///</summary>
    public void CheckLoadingConfirmCount()
    {
        // Resets and loops to count amount of ready ups
        readyUpCounter = 0;
        foreach (bool readyUpValue in playerLoadingConfirm)
        {
            if (readyUpValue)
                readyUpCounter++;
        }

        // Checks if players are greater then 1 and all players are readied up
        if (readyUpCounter >= PlayerCount && PlayerCount >= 1)
        {
            SceneFlow.Current.ConfirmLoad();
        }
        else
        {
            return;
        }
    }

    /// <summary>
    /// Swaps the menu control scheme for all players to main menu
    /// </summary>
    private void SwapForMainMenu()
    {
        SwapPlayerInputControlSchemeToUI();

        SwapMenuTypeForAllPlayers(MenuType.MainMenu);
    }

    /// <summary>
    /// Swaps the menu control scheme for all players to cutscenes
    /// </summary>
    private void SwapForCutscene()
    {
        SwapPlayerInputControlSchemeToUI();

        SwapMenuTypeForAllPlayers(MenuType.Cutscene);
    }

    /// <summary>
    /// Swaps the menu control scheme for all players to character select
    /// </summary>
    private void SwapForCharacterSelect()
    {
        SwapPlayerInputControlSchemeToUI();

        SwapMenuTypeForAllPlayers(MenuType.PlayerSelect);
    }

    /// <summary>
    /// Swaps the menu control scheme for all players to results
    /// </summary>
    private void SwapForResults()
    {
        SwapPlayerInputControlSchemeToUI();

        SwapMenuTypeForAllPlayers(MenuType.ResultsMenu);
    }

    /// <summary>
    /// Swaps the menu type for all players
    /// </summary>
    private void SwapMenuTypeForAllPlayers(MenuType menuType)
    {
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerUIHandler>().menuInteractions.SwapMenuType(menuType);

            // If swapping to pause menu, reparent menu ui to game-camera
            if (menuType == MenuType.PauseMenu)
            {
                slot.Input.GetComponent<PlayerCameraResizer>().ReparentMenuCameraStack(true);
            }
            // If swapping to other menu, reparent menu ui to player-camera on main menu
            else if (menuType == MenuType.MainMenu || menuType == MenuType.ResultsMenu)
            {
                slot.Input.GetComponent<PlayerCameraResizer>().ReparentMenuCameraStack(false);
            }
        }
    }

    ///<summary>
    /// Swaps all player's input control schemes to UI
    ///</summary>
    public void SwapPlayerInputControlSchemeToUI()
    {
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerCameraResizer>().SwapCanvas(true);

            slot.Input.actions.FindActionMap("UI").Enable();
            slot.Input.actions.FindActionMap("Player").Disable();
            slot.Input.actions.FindActionMap("Load").Disable();
        }
    }

    ///<summary>
    /// Swaps all player's input control schemes to "Driving" Player movement
    ///</summary>
    public void SwapPlayerInputControlSchemeToDrive()
    {
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.GetComponent<PlayerCameraResizer>().SwapCanvas(false);

            slot.Input.actions.FindActionMap("UI").Disable();
            slot.Input.actions.FindActionMap("Player").Enable();
            slot.Input.actions.FindActionMap("Load").Disable();
        }
    }

    ///<summary>
    /// Swaps all player's input control schemes to "NoInput" Player movement
    ///</summary>
    public void SwapPlayerInputControlSchemeToLoad()
    {
        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            slot.Input.actions.FindActionMap("UI").Disable();
            slot.Input.actions.FindActionMap("Player").Disable();
            slot.Input.actions.FindActionMap("Load").Enable();
        }
    }

    /// <summary>
    /// Swaps the control scheme for all players to driving
    /// </summary>
    private void SwapForDriving()
    {
        StartCoroutine(PlayerMoverCountdown());
    }

    private IEnumerator PlayerMoverCountdown()
    {
        if (cutsceneManager == null)
            cutsceneManager = CutsceneManager.Instance;

        cutsceneManager.BeginCountdownAnimation();

        yield return new WaitForSeconds(3f);

        SwapPlayerInputControlSchemeToDrive();
        SwapMenuTypeForAllPlayers(MenuType.PauseMenu);
    }

    ///<summary>
    /// Enables the ability to spawn players
    ///</summary>
    public void EnablePlayerSpawn() { allowPlayerSpawn = true;}

    ///<summary>
    /// Disables the ability to spawn players
    ///</summary>
    public void DisablePlayerSpawn() { allowPlayerSpawn = false; }

    ///<summary>
    /// Resets player canvas' when loading into scene
    ///</summary>
    public void ResetPlayerCanvas()
    {
        foreach (PlayerSlot slot in roster.LocalPlayers)
            slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().ResetCanvas();
    }

    ///<summary>
    /// Pause was triggered, must pause for all players
    ///</summary>
    public void PlayerPause(PlayerInput playerInput)
    {
        SwapPlayerInputControlSchemeToUI();

        GameAuthority.SetTimeScale(0f);

        foreach (PlayerSlot slot in roster.LocalPlayers)
        {
            MenuInteractions menu = slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>();

            // For one who paused
            if (playerInput == slot.Input)
            {
                menu.hostPause = true;
                menu.pauseMenu.OnPause(PauseMenu.PauseType.Host);
            }
            else
            {
                menu.hostPause = false;
                menu.pauseMenu.OnPause(PauseMenu.PauseType.Sub);
            }
        }
    }

    ///<summary>
    /// Tells whoever pressed a keyboard, mouse or unsupported controller that they need a controller
    ///</summary>
    private void ShowUnsupportedDeviceHint(InputDevice device)
    {
        ControllerPrompts.Instance.ShowHint(ControllerPrompts.HintForDevice(device), ControllerPrompts.HINT_SECONDS);
    }

    ///<summary>
    /// Pauses for everyone when a player's controller disconnects mid-race, with a player who still has a controller running the pause menu
    ///</summary>
    private void OnPlayerControllerLost(PlayerInput lostPlayer)
    {
        int lostSlot = roster.IndexOf(lostPlayer);
        if (lostSlot < 0)
            return;

        // Shows "reconnect" on their view and lets any controller take their place
        UpdateControllerPrompts();

        MenuInteractions lostPlayerMenu = lostPlayer.gameObject.GetComponent<PlayerUIHandler>().menuInteractions;
        if (!ControllerDisconnectPolicy.ShouldPause(lostPlayerMenu.curentMenuType == MenuType.PauseMenu, Time.timeScale == 0f))
            return;

        bool[] slotHasController = new bool[Constants.MAX_PLAYERS];
        for (int i = 0; i < slotHasController.Length; i++)
        {
            PlayerSlot slot = roster[i];
            slotHasController[i] = slot != null && slot.IsLocal && !IsMissingController(slot.Input.user);
        }

        int hostSlot = ControllerDisconnectPolicy.PickPauseHost(slotHasController, lostSlot);
        roster[hostSlot].Input.GetComponent<PlayerUIHandler>().menuInteractions.PauseGame(true);
    }

    ///<summary>
    /// Clears the reconnect message when a player's own controller comes back
    ///</summary>
    private void OnPlayerControllerRegained(PlayerInput player)
    {
        UpdateControllerPrompts();
    }

    ///<summary>
    /// A controller nobody was using was pressed while a player's controller is missing: it becomes that player's controller
    ///</summary>
    private void GiveControllerToMissingPlayer(Gamepad gamepad)
    {
        int slot = ControllerDisconnectPolicy.PickSlotForReplacement(SlotsMissingController());
        if (slot < 0)
            return;

        PlayerInput player = roster[slot].Input;

        // Replaces the lost controller too, so it won't also drive this player if it comes back later
        InputUser.PerformPairingWithDevice(gamepad, player.user, InputUserPairingOptions.UnpairCurrentDevicesFromUser);

        // Rumble follows the new controller
        playerGamepads[slot] = gamepad;
        player.GetComponentInChildren<BallDriving>().SetGamepad(gamepad);

        UpdateControllerPrompts();
    }

    ///<summary>
    /// True while a player's controller is lost (unplugged, battery died) and nothing has replaced it. Checks the lost
    /// devices, not hasMissingRequiredDevices: the Input System reports DeviceLost / DeviceRegained before updating that flag
    ///</summary>
    public static bool IsMissingController(InputUser user)
    {
        return user.valid && user.lostDevices.Count > 0;
    }

    ///<summary>
    /// Online: the seat this machine's one player sits in (-1 offline)
    ///</summary>
    public int OnlineSeat { get { return onlineSeat; } }

    ///<summary>
    /// Online: this machine's player sits in the seat the host gave them (-1 = offline again). A player already in
    /// another slot moves there: they leave and join again next frame with the same controller, so every slot-bound
    /// part (layers, cameras, company, podium) is set up as for any join. A player joining later takes the seat
    ///</summary>
    public void SetOnlineSeat(int seat)
    {
        onlineSeat = seat;
        if (seat < 0)
            return;

        foreach (PlayerSlot local in roster.LocalPlayers)
        {
            if (local.Index != seat)
                StartCoroutine(MoveToOnlineSeat(local.Input));
            return; // one player per machine online
        }
    }

    private IEnumerator MoveToOnlineSeat(PlayerInput player)
    {
        Gamepad pad = player.GetDevice<Gamepad>();
        LeaveLocal(player);

        // The old player lets go of the controller when it's destroyed, at the end of this frame
        yield return null;

        if (pad != null && pad.added)
            PlayerInputManager.instance.JoinPlayer(-1, -1, "Gamepad", pad);
    }

    ///<summary>
    /// Which joined players have no working controller right now
    ///</summary>
    private bool[] SlotsMissingController()
    {
        bool[] missing = new bool[Constants.MAX_PLAYERS];
        for (int i = 0; i < missing.Length; i++)
        {
            PlayerSlot slot = roster[i];
            missing[i] = slot != null && slot.IsLocal && IsMissingController(slot.Input.user);
        }
        return missing;
    }

    ///<summary>
    /// Shows "reconnect" over every player missing a controller and waits for a controller to replace theirs; clears it all once nobody is missing one
    ///</summary>
    private void UpdateControllerPrompts()
    {
        bool[] missing = SlotsMissingController();
        bool anyMissing = ControllerDisconnectPolicy.PickSlotForReplacement(missing) >= 0;

        // Nothing to show and nothing on screen to hide
        if (!anyMissing && !ControllerPrompts.Exists)
        {
            ReplacementListener.SetListening(false);
            return;
        }

        for (int i = 0; i < missing.Length; i++)
        {
            if (missing[i])
                ControllerPrompts.Instance.ShowReconnect(i, roster[i].Input.camera.rect);
            else
                ControllerPrompts.Instance.HideReconnect(i);
        }

        ReplacementListener.SetListening(anyMissing);
    }

    ///<summary>
    /// Unpause was triggered, must unpause for all players
    ///</summary>
    public void PlayerPlay()
    {
        SwapPlayerInputControlSchemeToDrive();

        GameAuthority.SetTimeScale(1f);

        foreach (PlayerSlot slot in roster.LocalPlayers)
            slot.Input.GetComponent<PlayerUIHandler>().MenuCanvas.GetComponent<MenuInteractions>().pauseMenu.OnPlay();
    }

    /// <summary>
    /// Updates driving indicators for all players
    /// </summary>
    public void PlayerUpdateDrivingIndicators()
    {
        // Every player's scooter carries the indicators that the views on this machine see
        foreach (PlayerSlot slot in roster.Players)
            slot.Player.GetComponentInChildren<DrivingIndicators>().UpdatePlayerReferencesForObjects();
    }

}
