using UnityEngine;

/// <summary>
/// Developers (DevTools): F7 hands Player 1's scooter to a CPU driver (BotDriver), and F7 again hands it back. For
/// watching bots phase G1 drive the town. Made once the first scene has loaded
/// </summary>
public class BotAutopilot : MonoBehaviour
{
    BotDriver bot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        GameObject go = new GameObject(nameof(BotAutopilot));
        DontDestroyOnLoad(go);
        go.AddComponent<BotAutopilot>();
    }

    void Update()
    {
        if (!DevTools.GetKeyDown(KeyCode.F7) || PlayerInstantiate.Instance == null)
            return;

        if (bot != null)
        {
            Destroy(bot);
            bot = null;
            return;
        }

        PlayerSlot first = PlayerInstantiate.Instance.Roster[0];
        BallDriving scooter = first != null && first.Player != null ? first.Player.GetComponentInChildren<BallDriving>() : null;
        if (scooter == null)
            return;
        bot = scooter.gameObject.AddComponent<BotDriver>();
        bot.Drive(scooter);
    }
}
