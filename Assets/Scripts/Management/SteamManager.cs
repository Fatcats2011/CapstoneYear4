#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX || STEAMWORKS_WIN || STEAMWORKS_LIN_OSX)
#define DISABLESTEAMWORKS
#endif

using System;
using System.Text;
using UnityEngine;
#if !DISABLESTEAMWORKS
using Steamworks;
#endif

/// <summary>
/// Starts Steam when the game launches and keeps its callbacks running.
/// Creates itself before the first scene loads; if Steam isn't running the game carries on without it.
/// </summary>
public class SteamManager : MonoBehaviour
{
    static SteamManager instance;
    bool initialized;

    /// <summary>
    /// True once the Steam API is running; check this before calling any Steamworks function
    /// </summary>
    public static bool Initialized => instance != null && instance.initialized;

    /// <summary>
    /// True on a Steam Deck (only known once Steam is running)
    /// </summary>
    public static bool IsSteamDeck
    {
        get
        {
#if !DISABLESTEAMWORKS
            return Initialized && SteamUtils.IsSteamRunningOnSteamDeck();
#else
            return false;
#endif
        }
    }

#if !DISABLESTEAMWORKS
    SteamAPIWarningMessageHook_t warningMessageHook;

    ///<summary>
    /// Clears the static instance before entering Play Mode, in case domain reload is disabled
    ///</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        instance = null;
    }

    ///<summary>
    /// Creates the manager before the first scene loads, so no scene needs to contain it
    ///</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateOnLaunch()
    {
        if (instance != null)
            return;

        GameObject steam = new GameObject(nameof(SteamManager));
        DontDestroyOnLoad(steam);
        steam.AddComponent<SteamManager>();
    }

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        if (!Packsize.Test())
            Debug.LogError("Steamworks.NET: the wrong Steamworks.NET assembly is being used for this platform.", this);

        if (!DllCheck.Test())
            Debug.LogError("Steamworks.NET: one or more of the Steamworks binaries seems to be the wrong version.", this);

        try
        {
            // A release build started from its .exe relaunches through Steam so the overlay and ownership check work
            if (SteamStartup.ShouldRestartThroughSteam(SteamStartup.APP_ID, Application.isEditor)
                && SteamAPI.RestartAppIfNecessary(new AppId_t(SteamStartup.APP_ID)))
            {
                Application.Quit();
                return;
            }

            initialized = SteamAPI.Init();
        }
        catch (DllNotFoundException e) // steam_api64.dll missing next to the game
        {
            Debug.LogWarning("Steam API library not found; continuing without Steam features.\n" + e.Message, this);
            return;
        }

        // Steam not running, or the account doesn't own the game: local play still works
        if (!initialized)
            Debug.LogWarning("Steam isn't available; continuing without Steam features.", this);
    }

    private void OnEnable()
    {
        if (!initialized || warningMessageHook != null)
            return;

        // Routes Steam's own warnings into the Unity log
        warningMessageHook = SteamAPIDebugTextHook;
        SteamClient.SetWarningMessageHook(warningMessageHook);
    }

    [AOT.MonoPInvokeCallback(typeof(SteamAPIWarningMessageHook_t))]
    static void SteamAPIDebugTextHook(int severity, StringBuilder debugText)
    {
        Debug.LogWarning(debugText);
    }

    private void Update()
    {
        if (initialized)
            SteamAPI.RunCallbacks();
    }

    private void OnDestroy()
    {
        if (instance != this)
            return;

        instance = null;

        if (initialized)
            SteamAPI.Shutdown();
    }
#endif
}
