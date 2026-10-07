using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpawnManager : SingletonMonobehaviour<SpawnManager>
{
    GameManager gameManager;
    internal PlayerInstantiate playerInstantiate;

    [Tooltip("The spawn positions of the players as they start the game: the start of each seat's tutorial lane")]
    [SerializeField] internal GameObject[] gameSpawnPositions = new GameObject[Constants.MAX_PLAYERS];
    [SerializeField] internal GameObject[] goldenPackageSpawnPositions = new GameObject[Constants.MAX_PLAYERS];

    ///<summary>
    /// On Enable of Script
    ///</summary>
    private void OnEnable()
    {
        gameManager = GameManager.Instance;
        playerInstantiate = PlayerInstantiate.Instance;

        gameManager.OnSwapStartingCutscene += SpawnPlayersStartOfGame;
        gameManager.OnSwapGoldenCutscene += SpawnPlayersFinalPackage;
    }

    ///<summary>
    /// On Disable of Script
    ///</summary>
    private void OnDisable()
    {
        gameManager.OnSwapStartingCutscene -= SpawnPlayersStartOfGame;
        gameManager.OnSwapGoldenCutscene -= SpawnPlayersFinalPackage;
    }

    private void Start()
    {
        // A scene shown only on the way back to the menu (the session ended during its load) starts nothing
        ISceneFlow flow = SceneFlow.Current;
        if (flow != null && flow.LeavingForMenu)
            return;

        // Set game to begin upon loading into scene
        if (TutorialManager.Instance.ShouldTutorialize)
        {
            gameManager.SetGameState(GameState.StartingCutscene);
        }
        else
        {
            gameManager.SetGameState(GameState.GoldenCutscene);
        }
    }

    ///<summary>
    /// This is executed when the OnSwapStartingCutscene event is called
    ///</summary>
    private void SpawnPlayersStartOfGame()
    {
        PlacePlayersAtStart();

        // Every scooter shows on this machine's compasses
        foreach (PlayerSlot player in playerInstantiate.Roster.Players)
            player.Player.GetComponentInChildren<CompassMarker>().InitalizeCompassUIOnAllPlayers();

        // After players have been placed, begin main loop
        gameManager.SetGameState(GameState.MainLoop);
    }

    ///<summary>
    /// Puts this machine's players on their starting spawn points: the start of their seat's tutorial lane
    ///</summary>
    public void PlacePlayersAtStart()
    {
        // Each machine places its own players: online, another machine's scooter is where its owner puts it
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            PlacePlayer(player, SpawnPoint(player.Index, gameSpawnPositions, null));
    }

    ///<summary>
    /// This is executed when the OnSwapGoldenCutscene event is called
    ///</summary>
    public void SpawnPlayersFinalPackage()
    {
        // The golden round lists 3 spawn points; a 4th player starts on their normal spawn point, in line with the others.
        // Each machine places its own players (online, another machine's scooter is where its owner puts it)
        foreach (PlayerSlot player in playerInstantiate.Roster.LocalPlayers)
            PlacePlayer(player, SpawnPoint(player.Index, goldenPackageSpawnPositions, gameSpawnPositions));
    }

    ///<summary>
    /// A slot's spawn point from the preferred list, or from the fallback list when the preferred one has none for it; null when neither has one
    ///</summary>
    public static GameObject SpawnPoint(int slot, GameObject[] preferred, GameObject[] fallback)
    {
        if (preferred != null && slot < preferred.Length && preferred[slot] != null)
            return preferred[slot];

        if (fallback != null && slot < fallback.Length && fallback[slot] != null)
            return fallback[slot];

        return null;
    }

    ///<summary>
    /// Stops a player and puts their ball and scooter on a spawn point
    ///</summary>
    private static void PlacePlayer(PlayerSlot player, GameObject spawnPoint)
    {
        if (spawnPoint == null)
        {
            Debug.LogWarning("No spawn point for player " + (player.Index + 1));
            return;
        }

        Rigidbody ball = player.Player.GetComponentInChildren<Rigidbody>();
        Transform scooter = player.Player.GetComponentInChildren<BallDriving>().transform;

        // Resets the velocity of the players
        ball.velocity = Vector3.zero;

        // reset position and rotation of ball and controller
        ball.transform.position = spawnPoint.transform.position;
        ball.transform.rotation = spawnPoint.transform.rotation;

        scooter.position = spawnPoint.transform.position;
        scooter.rotation = spawnPoint.transform.rotation;
    }
}
