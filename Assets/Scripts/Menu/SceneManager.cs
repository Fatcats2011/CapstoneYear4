using System;
using System.Collections;
using System.Threading;
using TMPro;
using Udar.SceneManager;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneManager : SingletonMonobehaviour<SceneManager>, ISceneFlow
{
    [SerializeField] PlayerInstantiate playerInstantiate;
    public event Action OnReturnToMenu;
    public event Action OnConfirmToLoad;

    [Header("Loading Screen")]
    [SerializeField] bool loadingScreenEnabled;
    [SerializeField] bool enableConfirm;
    public bool LoadingScreenEnabled { get { return loadingScreenEnabled; } }
    public bool EnableConfirm { get { return enableConfirm; }}

    [SerializeField] float loadingScreenDelay;
    [SerializeField] Image UIRender;
    Material UIRenderMaterial;
    [SerializeField] Animator loadingScreenAnimation;

    [Header("Leaderboard")]
    [SerializeField] private GameObject leaderboardGO;
    [SerializeField] private TextMeshProUGUI[] playerNames;
    [SerializeField] private TextMeshProUGUI[] playerScores;

    [Header("Scene References")]
    [SerializeField] SceneField PlayerSelectScene;
    [SerializeField] SceneField GameScene;
    [SerializeField] SceneField FinalOrderScene;

    [SerializeField] private Sprite mainTut;
    [SerializeField] private Sprite finalTut;
    [SerializeField] private Image tutorialImage;

    AsyncOperation sceneLoad;
    Coroutine sceneLoadCoroutune;
    bool spawnMenuBool;

    private void Start()
    {
        playerInstantiate = PlayerInstantiate.Instance;
        playerInstantiate.OnReadiedUp += StartMatch;

        OnReturnToMenu += LoadMenuScene;
        OnConfirmToLoad += SwapToSceneAfterConfirm;

        GameManager.Instance.OnSwapMenu += HideLoadingScreen;
        GameManager.Instance.OnSwapStartingCutscene += HideLoadingScreen;
        GameManager.Instance.OnSwapGoldenCutscene += HideLoadingScreen;
    }

    private void OnDisable()
    {
        playerInstantiate.OnReadiedUp -= StartMatch;
        
        OnReturnToMenu -= LoadMenuScene;
        OnConfirmToLoad -= SwapToSceneAfterConfirm;

        GameManager.Instance.OnSwapMenu -= HideLoadingScreen;
        GameManager.Instance.OnSwapStartingCutscene -= HideLoadingScreen;
        GameManager.Instance.OnSwapGoldenCutscene -= HideLoadingScreen;
    }

    public void InvokeMenuSceneEvent() 
    {
        OnReturnToMenu?.Invoke(); 
    }

    // The local scene flow (ISceneFlow): LoadGameScene, LoadFinalOrderScene, ConfirmLoad and OnReturnToMenu are this
    // class's own public members
    void ISceneFlow.ReturnToMenu() { InvokeMenuSceneEvent(); }
    bool ISceneFlow.WaitingForConfirm { get { return enableConfirm; } }

    ///<summary>
    /// Everyone is ready in player select: the match starts through the scene flow (this loader offline; online, the
    /// online flow)
    ///</summary>
    private void StartMatch()
    {
        SceneFlow.Current.LoadGameScene();
    }

    ///<summary>
    /// Main method that loads the menu
    ///</summary>
    private void LoadMenuScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != PlayerSelectScene.BuildIndex)
        {
            // Stops Corutine
            if(sceneLoadCoroutune != null)
            {
                StopCoroutine(sceneLoadCoroutune);
                sceneLoadCoroutune = null;
            }
            HideLeaderboard();
            tutorialImage.sprite = mainTut; // change this if we have a different loading screen for after results
            ShowLoadingScreen();
            sceneLoadCoroutune = StartCoroutine(LoadSceneAsync(PlayerSelectScene.BuildIndex, loadingScreenDelay, false, true));
        }
    }

    ///<summary>
    /// Main method that loads the game
    ///</summary>
    public void LoadGameScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != GameScene.BuildIndex && PlayerInstantiate.Instance.PlayerCount >= 1)
        {
            // Stops Corutine
            if (sceneLoadCoroutune != null)
            {
                StopCoroutine(sceneLoadCoroutune);
                sceneLoadCoroutune = null;
            }

            HideLeaderboard();
            tutorialImage.sprite = mainTut;
            ShowLoadingScreen();
            sceneLoadCoroutune = StartCoroutine(LoadSceneAsync(GameScene.BuildIndex, loadingScreenDelay, true, false));
            
            if(OrderManager.Instance != null)
                OrderManager.Instance.ResetForNextGame();
        }
    }

    public void LoadFinalOrderScene()
    {
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex != FinalOrderScene.BuildIndex)
        {
            // Stops Corutine
            if (sceneLoadCoroutune != null)
            {
                StopCoroutine(sceneLoadCoroutune);
                sceneLoadCoroutune = null;
            }

            tutorialImage.sprite = finalTut;
            ShowLoadingScreen();
            ShowLeaderboard();
            sceneLoadCoroutune = StartCoroutine(LoadSceneAsync(FinalOrderScene.BuildIndex, loadingScreenDelay, true, false));
        }
    }

    ///<summary>
    /// Loads the scene async
    ///</summary>
    private IEnumerator LoadSceneAsync(int sceneToLoad, float delayTime, bool waitForConfirm, bool spawnMenu)
    {
        // Sets gamestate to loading
        GameManager.Instance.SetGameState(GameState.Loading);

        // Sets all players to loading input during loading screen
        playerInstantiate.SwapPlayerInputControlSchemeToLoad();

        // Loads the first scene asynchronously
        AsyncOperation asyncLoad = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        asyncLoad.allowSceneActivation = false;

        // Wait until the asynchronous scene is allowed to be activated
        while (!asyncLoad.allowSceneActivation)
        {
            if (asyncLoad.progress >= 0.90f)
            {
                sceneLoad = asyncLoad;
                spawnMenuBool = spawnMenu;

                // Wait for player confirm
                if (waitForConfirm)
                {
                    yield return new WaitForSeconds(5f);
                    // Sets up the loading scene with the amount of players
                    LoadingScreenManager.Instance.InitalizeButtonGameobjects(PlayerInstantiate.Instance.Roster);
                    enableConfirm = true;
                    break;
                }
                // Auto Load
                else if (!waitForConfirm)
                {
                    yield return new WaitForSeconds(delayTime);
                    ConfirmLoad();
                }

                break;
            }
            yield return null;
        }
    }

    public void ConfirmLoad()
    {
        enableConfirm = false;
        OnConfirmToLoad?.Invoke();
    }

    ///<summary>
    /// After all players are confirmed, swap to scene
    ///</summary>
    public void SwapToSceneAfterConfirm()
    {
        if (sceneLoad == null)
            return;

        sceneLoad.allowSceneActivation = true;

        if (spawnMenuBool)
        {
            // Once menu scene is loaded, set players to spawn, if there are any
            playerInstantiate.SetAllPlayerSpawn();
        }

        // Resets button objects after scene swaps
        StartCoroutine(LoadingScreenManager.Instance.DisableButtonGameobjects());
    }

    private void ShowLoadingScreen()
    {
        // Checks if loading screen is set, if is, return
        if (loadingScreenEnabled == true)
            return;

        // Sets loading screen to true
        loadingScreenEnabled = true;

        // Caches screen material
        UIRenderMaterial = new Material(UIRender.material);
        UIRender.material = UIRenderMaterial;

        LoadIn();
        loadingScreenAnimation.SetTrigger("LoadIn");
    }

    private void ShowLeaderboard()
    {
        leaderboardGO.SetActive(true);
        int topScore = ScoreManager.Instance.GetHandlerOfIndex(0).Score;
        for(int i=0;i<PlayerInstantiate.Instance.PlayerCount;i++)
        {
            OrderHandler oh = ScoreManager.Instance.GetHandlerOfIndex(i);
            playerNames[i].gameObject.SetActive(true);
            playerScores[i].gameObject.SetActive(true);
            playerNames[i].text = $"{oh.Placement}. ({oh.transform.parent.name}) {oh.CompanyInfo.name}";
            playerScores[i].text = $"${oh.Score}";
        }
    }

    private void HideLeaderboard()
    {
        for (int i=0;i<playerNames.Length;i++)
        {
            playerNames[i].gameObject.SetActive(false);
            playerScores[i].gameObject.SetActive(false);
        }
        leaderboardGO.SetActive(false);
    }

    private void HideLoadingScreen()
    {
        // Checks if loading screen is not set, if is, return
        if(loadingScreenEnabled == false) 
            return;

        loadingScreenEnabled = false;
        LoadOut();
        loadingScreenAnimation.SetTrigger("LoadOut");
    }

    void LoadIn() { StartCoroutine(AnimateMaterial(false)); }
    void LoadOut() { StartCoroutine(AnimateMaterial(true)); }
    private IEnumerator AnimateMaterial(bool type)
    {
        yield return null;

        // Increase value until 1
        if (type)
        {
            float value = 0;

            while(value < 1)
            {
                yield return null;
                value += Time.deltaTime;
                UIRenderMaterial.SetFloat("_Value", value);
            }
        }
        // Decrease value unitl 0
        else
        {
            float value = 1;

            while (value > 0)
            {
                yield return null;
                value -= Time.deltaTime;
                UIRenderMaterial.SetFloat("_Value", value);
            }
        }
    }
}
