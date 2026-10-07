using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A line of text along the top of the screen: in player select it says what Y and B do online (OnlinePlay). Builds its
/// own canvas the first time it's used, like ControllerPrompts, so no scene or prefab has to contain it
/// </summary>
public class LobbyPrompt : MonoBehaviour
{
    static LobbyPrompt instance;

    GameObject panel;
    TMP_Text text;

    /// <summary>
    /// True once the line has been built
    /// </summary>
    public static bool Exists => instance != null;

    /// <summary>
    /// The line, built the first time it's needed
    /// </summary>
    public static LobbyPrompt Instance
    {
        get
        {
            if (instance == null)
                instance = Create();
            return instance;
        }
    }

    public bool IsShown => panel.activeSelf;
    public string Text => text.text;

    ///<summary>
    /// Shows a line of text, or hides the line for nothing ("" or null)
    ///</summary>
    public void Show(string message)
    {
        bool shown = !string.IsNullOrEmpty(message);
        text.text = shown ? message : "";
        panel.SetActive(shown);
    }

    ///<summary>
    /// Builds the overlay canvas with the line, hidden
    ///</summary>
    /// <summary>Above every menu and HUD canvas, below ControllerPrompts' hints (why a session ended must stay readable)</summary>
    internal const int SORTING_ORDER = 999;

    static LobbyPrompt Create()
    {
        GameObject root = new GameObject(nameof(LobbyPrompt), typeof(Canvas), typeof(CanvasScaler));
        if (Application.isPlaying)
            DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        LobbyPrompt prompt = root.AddComponent<LobbyPrompt>();

        // A dark strip with centred white text, like the controller hints along the bottom
        prompt.panel = new GameObject("Line", typeof(RectTransform), typeof(Image));
        prompt.panel.transform.SetParent(root.transform, false);
        RectTransform area = (RectTransform)prompt.panel.transform;
        area.anchorMin = new Vector2(0f, 0.9f);
        area.anchorMax = new Vector2(1f, 0.97f);
        area.offsetMin = Vector2.zero;
        area.offsetMax = Vector2.zero;

        Image background = prompt.panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.6f);
        background.raycastTarget = false;

        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(prompt.panel.transform, false);
        RectTransform labelArea = (RectTransform)label.transform;
        labelArea.anchorMin = Vector2.zero;
        labelArea.anchorMax = Vector2.one;
        labelArea.offsetMin = Vector2.zero;
        labelArea.offsetMax = Vector2.zero;

        prompt.text = label.GetComponent<TextMeshProUGUI>();
        prompt.text.fontSize = 36f;
        prompt.text.color = Color.white;
        prompt.text.alignment = TextAlignmentOptions.Center;
        prompt.text.raycastTarget = false;

        prompt.panel.SetActive(false);
        return prompt;
    }
}
