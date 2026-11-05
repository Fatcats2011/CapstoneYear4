/// <summary>This class manages the order spawning system in Dead on Arrival. 
/// It will spawn easy, medium, and hard orders with a cooldown, and retains a reference to each order through a list.
/// It is a Singleton and extends the SingletonMonobehaviour class
/// </summary>

using JetBrains.Annotations;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class OrderManager : SingletonMonobehaviour<OrderManager>
{
    private bool canSpawnOrders = true; // determining if the current number of orders is below the wave value
    public bool CanSpawnOrders { get { return canSpawnOrders; } set { canSpawnOrders = value; } }

    [Header("Game Information")]
    [Tooltip("Maxium number of orders present in the scene at a time. Array represents different waves")]
    [SerializeField]
    internal int[] maxEasy, maxMedium, maxHard; // didn't think this variable name through

    [Tooltip("Time it takes in seconds for a wave to be completed")]
    [SerializeField] private float waveLengthInSeconds = 20f;

    // A wave's length in this match: the match rules' Length stretches or shortens it (HouseRules)
    // A mode without deliveries plays one wave as long as its Round Timer (ModeReferee)
    internal float WaveLength => HouseRules.Playing.Mode == GameMode.HotPotato ? TimedModeRules.POTATO_ROUND_LIMIT
        : TimedModeRules.IsTimed(HouseRules.Playing.Mode) ? HouseRules.Playing.RoundMinutes * 60f
        : waveLengthInSeconds * HouseRules.Playing.WaveFactor;
    internal float TotalGameTime => totalGameTime;

    // This wave's order quotas: the authored rows (maxEasy...) spread over the waves played (WaveTable)
    int EasyQuota => GoldRushRules.Quota(maxEasy[WaveTable.IndexFor(wave, HouseRules.Playing.WavesPlayed, maxEasy.Length)]);
    int MediumQuota => GoldRushRules.Quota(maxMedium[WaveTable.IndexFor(wave, HouseRules.Playing.WavesPlayed, maxMedium.Length)]);
    int HardQuota => GoldRushRules.Quota(maxHard[WaveTable.IndexFor(wave, HouseRules.Playing.WavesPlayed, maxHard.Length)]);
    internal int wave = 0;
    private float waveTimer = 0f;
    private float gameTimer = 0f;
    private float totalGameTime = 0f;
    private bool gameStarted = false;
    public bool GameStarted { get { return gameStarted; } }
    public float GameTimer { get { return gameTimer; } }
    public float WaveTimer { get { return waveTimer; } }
    private bool spawnNormalPackages = false;

    private int currEasy, currMedium, currHard; // the current number of easy/medium/hard orders in the game

    private bool cooledDown = true;

    [Tooltip("Reference to the total order list.")]
    [SerializeField] private GameObject totalOrderListGO;

    [Tooltip("Cooldown between order spawns")]
    [SerializeField] private float cooldownTime = 5f;

    [Tooltip("Time between main game ending and golden cutscene playing.")]
    [SerializeField] private float postGameLinger = 3f;

    [Tooltip("Final order increment. 1 will have it add a dollar to the value.")]
    [SerializeField] private float goldIncrementValue = 1f;

    [Tooltip("Timer for when value is added to the golden package. 1 will have it add money every second")]
    [SerializeField] private float goldValueTimer = 1f;
    internal float goldTimer = 0f;

    [Tooltip("The master list of all non-golden orders in the game.")]
    [SerializeField] internal List<Order> normalOrders;
    [Tooltip("The final order in the game.")]
    [SerializeField] internal Order finalOrder;
    [Tooltip("The orders used for tutorialization.")]
    [SerializeField] internal Order[] tutorialOrders = new Order[4];

    [Tooltip("The audio source of the clocktower for the bell chimes on new wave.")]
    [SerializeField] internal AudioSource clockSource;

    // lists for each of the types of orders in each game (minus golden ofc)
    private List<Order> easy = new List<Order>();
    private List<Order> medium = new List<Order>();
    private List<Order> hard = new List<Order>();

    internal bool finalOrderActive = false;
    public bool FinalOrderActive { get { return finalOrderActive; } }
    private float finalOrderValue = (float)Constants.OrderValue.Golden;
    public int FinalOrderValue { get { return (int)finalOrderValue; } set { finalOrderValue = (float)value; } }

    private List<Order> activeOrders = new List<Order>(); // list of all the orders in the game at any time
    /// <summary>Every order in the game now (a CPU driver looks for one to take: BotDriver)</summary>
    internal IReadOnlyList<Order> ActiveOrders => activeOrders;

    private IEnumerator easySpawnCoroutine, mediumSpawnCoroutine, hardSpawnCoroutine; // coroutines for managing cooldowns of the order spawns

    // wave event stuff
    public event Action OnDeleteActiveOrders;
    public event Action OnMainGameFinishes;

    [Header("Debug")]
    [Tooltip("When checked will loop through waves so you can play forever")]
    [SerializeField] private bool waveResets;
    [Tooltip("When true will randomize initial spawn order of all orders.")]
    [SerializeField] private bool randomizeWaves;

    // stop timer
    private bool countdown = true;

    private void OnEnable()
    {
        GameManager.Instance.OnSwapTutorial += InitTutorial;
        GameManager.Instance.OnSwapBegin += InitGame;
        GameManager.Instance.OnSwapGoldenCutscene += DisableSpawning;
        HotKeys.Instance.onIncrementWave += IncrementWave;
        HotKeys.Instance.onDecrementWave += DecrementWave;

        GameManager.Instance.OnSwapMenu += ResetForNextGame;
    }

    private void OnDisable()
    {
        GameManager.Instance.OnSwapTutorial -= InitTutorial;
        GameManager.Instance.OnSwapBegin -= InitGame;
        GameManager.Instance.OnSwapGoldenCutscene -= DisableSpawning;
        HotKeys.Instance.onIncrementWave -= IncrementWave;
        HotKeys.Instance.onDecrementWave -= DecrementWave;

        GameManager.Instance.OnSwapMenu -= ResetForNextGame;
    }

    private void Start()
    {
        this.transform.parent = GameManager.Instance.transform;
    }

    internal void Update()
    {
        WatchRushHour(); // every machine, from its own clock
        WatchGoldenRound();

        // Online, the host runs the waves, the order spawns and the golden order's value; clients show what it sends
        if (!GameAuthority.IsAuthority)
            return;

        // HOTKEY
        if(DevTools.GetKeyDown(KeyCode.Y))
        {
            OnDeleteActiveOrders?.Invoke();
        }

        // HOTKEY
        if (DevTools.GetKeyDown(KeyCode.Alpha5))
        {
            wave = HouseRules.Playing.WavesPlayed;
            InitWave();
        }

        if (finalOrder != null)
        {
            if (finalOrderActive && finalOrder.PlayerHolding != null)
            {

                if(goldTimer >= goldValueTimer)
                {
                    goldTimer = 0;
                    if (finalOrderValue <= 999)
                        finalOrderValue += goldIncrementValue;
                }
                else
                {
                    goldTimer += Time.deltaTime;
                }
            }
        }
        if (RoundRunning && ModeReferee.Tick() && !finalOrderActive)
        {
            // Kings Crown is never decided early; Infection and Hot Potato are once nobody's left to catch or explode
            wave = HouseRules.Playing.WavesPlayed;
            InitWave();
        }

        if (!spawnNormalPackages) { return; }

        if (waveTimer > 0 && !finalOrderActive)
        {
            // A mode without deliveries spawns none: its round only runs the clock
            if (canSpawnOrders && ModeRules.HasDeliveries(HouseRules.Playing.Mode))
            {
                if (activeOrders.Count >= 0)
                {
                    if (cooledDown)
                    {
                        StopEasySpawn();
                        StopMediumSpawn();
                        StopHardSpawn();
                        if (easy.Count > 0 && (currEasy < EasyQuota || currEasy == 0)) // if the number of easy orders is below the quota
                        {
                            StartEasySpawn();
                        }
                        else if (medium.Count > 0 && (currMedium < MediumQuota || currMedium == 0)) // same for the medium orders
                        {
                            StartMediumSpawn();
                        }
                        else if (hard.Count > 0 && (currHard < HardQuota || currHard == 0)) // and the hard orders
                        {
                            StartHardSpawn();
                        }
                    }
                }
            }
            else
            {
                StopEasySpawn();
                StopMediumSpawn();
                StopHardSpawn();
                cooledDown = true;
            }
            canSpawnOrders = currEasy < EasyQuota || currMedium < MediumQuota || currHard < HardQuota;
        }
        else if(!finalOrderActive && waveTimer <= 0)
        {
            ResetWave();
            InitWave();
        }

        if (countdown)
        {
            waveTimer -= Time.deltaTime;
            gameTimer -= Time.deltaTime;
        }

        // HOTKEY
        if(DevTools.GetKeyDown(KeyCode.P))
        {
            countdown = !countdown;
        }
    }

    /// <summary>
    /// This method populates the order lists and inits any orders that should be active on runtime.
    /// </summary>
    private void GetOrders()
    {
        for (int i = 0; i < normalOrders.Count(); i++)
        {
            if (normalOrders[i].IsActive)
            {
                IncrementCounters(normalOrders[i].Value, 1);
                normalOrders[i].InitOrder();
            }
            else
            {
                switch (normalOrders[i].Value)
                {
                    case Constants.OrderValue.Easy:
                        easy.Add(normalOrders[i]);
                        break;
                    case Constants.OrderValue.Medium:
                        medium.Add(normalOrders[i]);
                        break;
                    case Constants.OrderValue.Hard:
                        hard.Add(normalOrders[i]);
                        break;
                    default:
                        break;
                }
            }
        }
        if (randomizeWaves)
        {
            ShuffleList(easy);
            ShuffleList(medium);
            ShuffleList(hard);
        }
    }

    internal void InitTutorial()
    {
        // A rematch, or online, has no tutorial orders: it goes straight on to the first wave
        if (!GameAuthority.IsAuthority || TutorialManager.Skipping)
            return;

        TutorialManager.Instance.ShouldTutorialize = true;
        foreach (PlayerSlot player in PlayerInstantiate.Instance.Roster.Players)
        {
            tutorialOrders[player.Index].InitOrder(false);
            OnDeleteActiveOrders += tutorialOrders[player.Index].EraseOrder;
            IncrementCounters(tutorialOrders[player.Index].Value, 1);
        }
    }

    public void DeleteActiveOrders()
    {
        OnDeleteActiveOrders?.Invoke();
    }

    /// <summary>
    /// Calls when the Begin/MainLoop sequence begins
    /// </summary>
    internal void InitGame()
    {
        // Online, the host picks and spawns the orders
        if (!GameAuthority.IsAuthority)
            return;

        OnDeleteActiveOrders?.Invoke();

        // This match's rules: the host's, or normal in a public lobby (HouseRules)
        HouseRules.Playing = HouseRules.Current.ForMatch(PublicLobby());
        // A public lobby plays normal: the host's own picks weren't played, so they stay
        if (!PublicLobby())
            HouseRules.MatchBegan();
        MatchPhysics.Apply(HouseRules.Playing);
        ModeReferee.Begin(HouseRules.Playing.Mode);

        GetOrders();
        finalOrderActive = false;
        spawnNormalPackages = true;
        waveTimer = WaveLength;
        totalGameTime = WaveLength * HouseRules.Playing.WavesPlayed;
        wave = 0;
        canSpawnOrders = true;
        gameStarted = true;
        EnableSpawning();
    }

    /// <summary>The golden round ends this long after the players can drive, in seconds: a package nobody can deliver mustn't hold a match up forever</summary>
    internal const float GOLDEN_LIMIT = 180f;
    /// <summary>Everyone's told this long before that, in seconds</summary>
    internal const float GOLDEN_WARNING = 20f;
    internal float goldenEnds = -1f;
    bool goldenWarned;

    /// <summary>
    /// The golden round's time limit. Time up counts as a delivery by whoever holds it: they keep what it's grown to, without
    /// its base value, and it's the results. With nobody holding it, it's only the results. Every machine warns from its own
    /// clock; the host ends it
    /// </summary>
    void WatchGoldenRound()
    {
        if (GameManager.Instance == null || GameManager.Instance.MainState != GameState.FinalPackage || finalOrder == null || !finalOrder.IsActive)
        {
            goldenEnds = -1f;
            goldenWarned = false;
            return;
        }
        if (Time.time < PlayerInstantiate.DrivingFrom)
            return;
        if (goldenEnds < 0f)
            goldenEnds = Time.time + GOLDEN_LIMIT;
        if (!goldenWarned && Time.time >= goldenEnds - GOLDEN_WARNING)
        {
            goldenWarned = true;
            if (ControllerPrompts.Instance != null)
                ControllerPrompts.Instance.ShowHint(Loc.Get("golden.ending"), 5f);
        }
        if (GameAuthority.IsAuthority && Time.time >= goldenEnds)
            finalOrder.EraseOrder();
    }

    bool rushHourShown;

    /// <summary>Whether the waves' clock is running in the match (not the tutorial, not past the last wave)</summary>
    internal bool RoundRunning
    {
        get
        {
            if (!gameStarted || finalOrderActive || !countdown || GameManager.Instance == null)
                return false;
            GameState state = GameManager.Instance.MainState;
            return state == GameState.Begin || state == GameState.MainLoop;
        }
    }

    /// <summary>Gold Rush: says so on every machine as rush hour starts (each steal counts twice: GoldRushRules)</summary>
    void WatchRushHour()
    {
        bool rush = GoldRushRules.IsRushHour(HouseRules.Playing.Mode, gameTimer, finalOrderActive) && GameManager.Instance != null
            && (GameManager.Instance.MainState == GameState.Begin || GameManager.Instance.MainState == GameState.MainLoop);
        if (rush && !rushHourShown && ControllerPrompts.Instance != null)
            ControllerPrompts.Instance.ShowHint(Loc.Get("goldrush.rushhour"), 4f);
        rushHourShown = rush;
    }

    /// <summary>
    /// Gold Rush: sends the next free order out as the Mother Lode (MotherLode), from the farthest-travelling tier that has
    /// one free. False with none free: it tries again. It goes back to its own tier's pool like any order
    /// </summary>
    internal bool SpawnLode()
    {
        foreach (List<Order> pool in new[] { hard, medium, easy })
        {
            if (pool.Count == 0)
                continue;
            Order order = pool[0];
            pool.RemoveAt(0);
            IncrementCounters(order.Value, 1);
            order.InitOrder(true, true);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Initializes the wave.
    /// </summary>
    public void InitWave()
    {
        // Online, the host runs the waves
        if (!GameAuthority.IsAuthority)
            return;

        waveTimer = WaveLength;
        // Past the last wave: the final order should spawn
        if (wave >= HouseRules.Playing.WavesPlayed)
        {
            if (!finalOrderActive) // this is where post game clarity happens
            {
                TutorialManager.Instance.ShouldTutorialize = false;
                finalOrderActive = true;
                OnMainGameFinishes?.Invoke();

                DisableSpawning();

                MasterSkywalker();
                StartCoroutine(PostGameClarity(false));
                gameStarted = true;

                // sounds
                SoundManager.Instance.PlaySFX("timeout", clockSource);
                SoundManager.Instance.ChangeSnapshot("paused");
                CueSync.Ring(ClockCue.TimeUp); // online, time is up on every client too
            }
        }

        if (!finalOrderActive)
        {
            gameTimer = totalGameTime - (WaveLength * wave);
            if (wave > 0)
            {
                SoundManager.Instance.PlaySFX("bells", clockSource);
                CueSync.Ring(ClockCue.WaveBells); // online, every client's bells ring too
            }
        }
    }

    /// <summary>
    /// Unparents all the children.
    /// </summary>
    private void MasterSkywalker()
    {
        foreach(Transform child in transform)
        {
            ReparentOrder(child.gameObject);
        }
    }

    /// <summary>
    /// Enables the spawning of pacakges
    /// </summary>
    private void EnableSpawning()
    {
        InitWave();
        spawnNormalPackages = true;
    }

    /// <summary>
    /// Disables the spawning of pacakges
    /// </summary>
    private void DisableSpawning()
    {
        ResetWave();
        spawnNormalPackages = false;
    }

    /// <summary>
    /// This method adds and order to the list of existing orders if that order isn't already on the list. The golden
    /// order out means the golden round is on (RemoveOrder turns it off): it's out again after a player who held it left
    /// </summary>
    /// <param name="order">Order to be added to the list</param>
    public void AddOrder(Order order)
    {
        if(order.Value == Constants.OrderValue.Golden)
        {
            finalOrder = order;
            finalOrderActive = true;
        }

        if(!activeOrders.Contains(order))
        {
            activeOrders.Add(order);
            OnDeleteActiveOrders += order.EraseOrder;
        }
    }

    /// <summary>
    /// This method removes an order from the order list if it's in the list.
    /// </summary>
    /// <param name="order">Order to be removed</param>
    public void RemoveOrder(Order order)
    {
        if(activeOrders.Contains(order))
        {
            switch(order.Value)
            {
                case Constants.OrderValue.Easy:
                    easy.Add(order);
                    break;
                case Constants.OrderValue.Medium:
                    medium.Add(order); 
                    break;
                case Constants.OrderValue.Hard:
                    hard.Add(order);
                    break;
                default:
                    break;
            }
            activeOrders.Remove(order);
        }
        if(order.Value == Constants.OrderValue.Golden)
        {
            finalOrderActive = false;
        }
        // Back into its own scene, not under this manager: it outlives scenes, and the order would outlive its dropoff
        order.ReturnHome();
        OnDeleteActiveOrders -= order.EraseOrder;
    }

    /// <summary>
    /// This method attempts to spawn an order based on a random pickup waypoint and its proximity to other dropoff waypoints. If it fails it will return without doing anything.
    /// </summary>
    /// <param name="value">Value of the package to be spawned (Easy, Medium, or Hard)</param>
    private void SpawnOrder(Constants.OrderValue value)
    {
        Order nextOrder;
        switch (value)
        {
            case (Constants.OrderValue.Easy):
                if (easy.Count == 0)
                {
                    return;
                }
                nextOrder = easy.ElementAt(0);
                easy.Remove(nextOrder);
                break;
            case (Constants.OrderValue.Medium):
                if (medium.Count == 0)
                {
                    return;
                }
                nextOrder = medium.ElementAt(0);
                medium.Remove(nextOrder);
                break;
            case (Constants.OrderValue.Hard):
                if(hard.Count == 0)
                {
                    return;
                }
                nextOrder = hard.ElementAt(0);
                hard.Remove(nextOrder);
                break;
            default: // placeholder ?
                return;
        }
        IncrementCounters(value, 1);
        nextOrder.InitOrder();
    }

    /// <summary>
    /// This method increments one of three counters depending on the value passed in, and based on the amount given.
    /// </summary>
    /// <param name="value">Value of the counter you want to increment (Easy, Medium, Hard)</param>
    /// <param name="amount">Amount you want to increment by (typically +1 or -1)</param>
    public void IncrementCounters(Constants.OrderValue value, int amount)
    {
        switch(value)
        {
            case Constants.OrderValue.Easy:
                currEasy+=amount;
                break;
            case Constants.OrderValue.Medium:
                currMedium+=amount;
                break;
            case Constants.OrderValue.Hard:
                currHard+=amount;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Reparents an order to the total order list.
    /// </summary>
    /// <param name="inOrder"></param>
    public void ReparentOrder(GameObject inOrder)
    {
        if (GameManager.Instance.MainState == GameState.FinalPackage) // nothing to reparent to in the final scene
            return;

        inOrder.transform.parent = totalOrderListGO.transform;
    }

    /// <summary>
    /// Starts the next wave.
    /// </summary>
    private void ResetWave(bool inc = true)
    {
        StopEasySpawn();
        StopMediumSpawn();
        StopHardSpawn();
        cooledDown = true;
        if(inc)
            wave++;
    }

    private void IncrementWave()
    {
        ResetWave();
        InitWave();
    }
    private void DecrementWave()
    {
        ResetWave(false);
        wave--;
        InitWave();
    }

    public void SetFinalOrder(Order finalOrderIn)
    {
        finalOrder = finalOrderIn;
    }

    /// <summary>
    /// Called from the golden order GO when the it's been delivered.
    /// </summary>
    public void GoldOrderDelivered()
    {
        GameManager.Instance.InvokeFinalOrderDelivered();
        finalOrderActive = false;
        gameStarted = false;
        StartCoroutine(PostGameClarity(true));
        SoundManager.Instance.ChangeSnapshot("paused");
    }

    /// <summary>
    /// Online client: shows the host's match clock (this machine's waves don't run: the host's do)
    /// </summary>
    public void FollowHostClock(float timeLeft, bool started, bool finalOrder)
    {
        gameTimer = timeLeft;
        gameStarted = started;
        finalOrderActive = finalOrder;
    }

    /// <summary>
    /// Online client: the host's match clock rang (this machine's waves don't run: the host's do). A new wave's bells ring
    /// here. At time up this machine's players stop (their orders wait for the host's erases), the whistle blows and the
    /// sound dips, as on the host
    /// </summary>
    public void FollowHostClockCue(ClockCue cue)
    {
        switch (cue)
        {
            case ClockCue.WaveBells:
                SoundManager.Instance.PlaySFX("bells", clockSource);
                break;
            case ClockCue.TimeUp:
                OnMainGameFinishes?.Invoke();
                SoundManager.Instance.PlaySFX("timeout", clockSource);
                SoundManager.Instance.ChangeSnapshot("paused");
                break;
        }
    }

    /// <summary>
    /// Ensures next game will run smoothly.
    /// </summary>
    public void ResetForNextGame()
    {
        TutorialManager.Instance.ShouldTutorialize = true;
        Destroy(this.gameObject);
    }
    /// <summary>
    /// Shuffles the elements in a list.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="inList"></param>
    private void ShuffleList<T>(List<T> inList)
    {
        for(int i=0;i<inList.Count; i++)
        {
            T holder = inList[i];
            int r = UnityEngine.Random.Range(i, inList.Count);
            inList[i] = inList[r];
            inList[r] = holder;
        }
    }

    // methods to start/stop coroutines
    private void StartEasySpawn()
    {
        if(easySpawnCoroutine == null)
        {
            easySpawnCoroutine = EasySpawn();
            StartCoroutine(easySpawnCoroutine);
        }
    }
    private void StopEasySpawn()
    {
        if(easySpawnCoroutine != null)
        {
            StopCoroutine(easySpawnCoroutine);
            easySpawnCoroutine = null;
        }
    }

    private void StartMediumSpawn()
    {
        if (mediumSpawnCoroutine == null)
        {
            mediumSpawnCoroutine = MediumSpawn();
            StartCoroutine(mediumSpawnCoroutine);
        }
    }
    private void StopMediumSpawn()
    {
        if (mediumSpawnCoroutine != null)
        {
            StopCoroutine(mediumSpawnCoroutine);
            mediumSpawnCoroutine = null;
        }
    }

    private void StartHardSpawn()
    {
        if(hardSpawnCoroutine == null)
        {
            hardSpawnCoroutine = HardSpawn();
            StartCoroutine(hardSpawnCoroutine);
        }
    }
    private void StopHardSpawn()
    {
        if(hardSpawnCoroutine != null)
        {
            StopCoroutine(hardSpawnCoroutine);
            hardSpawnCoroutine = null;
        }
    }

    /// <summary>
    /// This coroutine attempts to spawn an easy order after the cooldown period has elapsed.
    /// </summary>
    /// <returns></returns>
    private IEnumerator EasySpawn()
    {
        cooledDown = false;
        yield return new WaitForSeconds(GoldRushRules.Cooldown(cooldownTime));
        SpawnOrder(Constants.OrderValue.Easy);
        cooledDown = true;
    }

    /// <summary>
    /// This coroutine attempts to spawn a medium order after the cooldown period has elapsed.
    /// </summary>
    /// <returns></returns>
    private IEnumerator MediumSpawn()
    {
        cooledDown = false;
        yield return new WaitForSeconds(GoldRushRules.Cooldown(cooldownTime));
        SpawnOrder(Constants.OrderValue.Medium);
        cooledDown = true;
    }

    /// <summary>
    /// This coroutine attempts to spawn a hard order after the cooldown period has elapsed.
    /// </summary>
    /// <returns></returns>
    private IEnumerator HardSpawn()
    {
        cooledDown = false;
        yield return new WaitForSeconds(GoldRushRules.Cooldown(cooldownTime));
        SpawnOrder(Constants.OrderValue.Hard);
        cooledDown = true;
    }

    /// <summary>Online, whether this is a public lobby (matchmaking, or the host's open lobby): those play the normal rules</summary>
    internal static bool PublicLobby()
    {
        OnlinePlay play = OnlinePlay.Instance;
        return GameAuthority.IsOnline && play != null
            && ((play.Lobby != null && play.Lobby.Filling) || (play.Match != null && play.Match.Searching));
    }

    /// <summary>
    /// Called when the main game ends. Lingers for a set amout of time before switching to the final order sequence.
    /// </summary>
    /// <returns></returns>
    internal IEnumerator PostGameClarity(bool isFinal)
    {
        GameAuthority.SetTimeScale(0.5f);
        yield return new WaitForSeconds(postGameLinger/2);
        GameAuthority.SetTimeScale(1.0f);
        if (!isFinal)
        {
            // The main round's highlight reel, if it had moments worth showing; the golden round loads after it (not if
            // the reel was cut short by another scene load)
            if (HighlightDirector.Instance != null)
                yield return HighlightDirector.Instance.ReelThen(() => SceneFlow.Current.LoadFinalOrderScene());
            else
                SceneFlow.Current.LoadFinalOrderScene();
        }
        else
        {
            GameManager.Instance.SetGameState(GameState.Results);
        }
    }
}
