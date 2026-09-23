using System;

/// <summary>
/// Moves the game between the menu, the match and the golden-order scene. In a local match SceneManager loads each scene
/// behind the loading screen, which waits for every player to press A. Online play (Phase 3) swaps in a flow that
/// follows the host's scene loads
/// </summary>
public interface ISceneFlow
{
    /// <summary>Loads the match once everyone is ready in player select</summary>
    void LoadGameScene();

    /// <summary>Loads the golden-order scene after the last wave</summary>
    void LoadFinalOrderScene();

    /// <summary>Takes everyone back to the menu (from the pause menu or the results)</summary>
    void ReturnToMenu();

    /// <summary>Raised when the game heads back to the menu</summary>
    event Action OnReturnToMenu;

    /// <summary>Whether the loading screen is waiting for every player to press A</summary>
    bool WaitingForConfirm { get; }

    /// <summary>Everyone pressed A: shows the loaded scene</summary>
    void ConfirmLoad();
}

/// <summary>
/// The scene flow the game uses: the local loader (SceneManager) unless online play sets another
/// </summary>
public static class SceneFlow
{
    static ISceneFlow current;

    /// <summary>
    /// The flow every script loads scenes through. Setting null goes back to the local loader
    /// </summary>
    public static ISceneFlow Current
    {
        get { return current ?? SceneManager.Instance; }
        set { current = value; }
    }
}
