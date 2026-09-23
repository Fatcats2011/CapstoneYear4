using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Controller messages drawn on top of every player's view: a "reconnect" panel over a player's viewport while their
/// controller is missing, and a short hint along the bottom when someone presses a key or an unsupported controller.
/// Builds its own canvas the first time it's used, so no scene or prefab has to contain it.
/// </summary>
public class ControllerPrompts : MonoBehaviour
{
    public const string CONNECT_CONTROLLER_HINT = "Connect a controller to play";
    public const string UNSUPPORTED_CONTROLLER_HINT = "That controller isn't supported - try an Xbox, PlayStation or Switch Pro controller";
    public const float HINT_SECONDS = 3f;

    static ControllerPrompts instance;

    readonly GameObject[] reconnectPanels = new GameObject[Constants.MAX_PLAYERS];
    readonly TMP_Text[] reconnectTexts = new TMP_Text[Constants.MAX_PLAYERS];
    GameObject hintPanel;
    TMP_Text hintText;
    float hintHideTime;

    /// <summary>
    /// True once the prompts canvas has been built
    /// </summary>
    public static bool Exists => instance != null;

    /// <summary>
    /// The prompts canvas, built the first time it's needed
    /// </summary>
    public static ControllerPrompts Instance
    {
        get
        {
            if (instance == null)
                instance = Create();
            return instance;
        }
    }

    public bool IsHintShown => hintPanel.activeSelf;
    public string HintText => hintText.text;

    ///<summary>
    /// The message shown over a player's view while their controller is missing
    ///</summary>
    public static string ReconnectMessage(int slot)
    {
        return $"P{slot + 1} controller disconnected\nPress a button on any controller to continue";
    }

    ///<summary>
    /// What to tell someone who pressed a device that can't play: keyboards and mice need a controller, anything else isn't supported
    ///</summary>
    public static string HintForDevice(InputDevice device)
    {
        return device == null || device is Keyboard || device is Mouse ? CONNECT_CONTROLLER_HINT : UNSUPPORTED_CONTROLLER_HINT;
    }

    ///<summary>
    /// Covers a player's viewport with the reconnect message
    ///</summary>
    public void ShowReconnect(int slot, Rect viewport)
    {
        RectTransform area = (RectTransform)reconnectPanels[slot].transform;
        area.anchorMin = viewport.min;
        area.anchorMax = viewport.max;

        reconnectTexts[slot].text = ReconnectMessage(slot);
        reconnectPanels[slot].SetActive(true);
    }

    ///<summary>
    /// Hides a player's reconnect message
    ///</summary>
    public void HideReconnect(int slot)
    {
        reconnectPanels[slot].SetActive(false);
    }

    public bool IsReconnectShown(int slot)
    {
        return reconnectPanels[slot].activeSelf;
    }

    ///<summary>
    /// The part of the screen a reconnect message covers, as a 0-1 rect like a camera viewport
    ///</summary>
    public Rect ReconnectArea(int slot)
    {
        RectTransform area = (RectTransform)reconnectPanels[slot].transform;
        return Rect.MinMaxRect(area.anchorMin.x, area.anchorMin.y, area.anchorMax.x, area.anchorMax.y);
    }

    ///<summary>
    /// Shows a message along the bottom of the screen for a few seconds
    ///</summary>
    public void ShowHint(string message, float seconds)
    {
        hintText.text = message;
        hintPanel.SetActive(true);
        hintHideTime = Time.unscaledTime + seconds;
    }

    private void Update()
    {
        // Unscaled time, so hints still go away while the game is paused
        if (hintPanel.activeSelf && Time.unscaledTime >= hintHideTime)
            hintPanel.SetActive(false);
    }

    ///<summary>
    /// Builds the overlay canvas: one reconnect panel per player and the hint bar, all hidden
    ///</summary>
    static ControllerPrompts Create()
    {
        GameObject root = new GameObject(nameof(ControllerPrompts), typeof(Canvas), typeof(CanvasScaler));
        if (Application.isPlaying)
            DontDestroyOnLoad(root);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // above every menu and HUD canvas

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        ControllerPrompts prompts = root.AddComponent<ControllerPrompts>();
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            prompts.reconnectPanels[i] = prompts.CreatePanel("Reconnect P" + (i + 1), 0.75f, 40f, out prompts.reconnectTexts[i]);
        }

        prompts.hintPanel = prompts.CreatePanel("Hint", 0.6f, 36f, out prompts.hintText);
        RectTransform hintArea = (RectTransform)prompts.hintPanel.transform;
        hintArea.anchorMin = new Vector2(0f, 0.05f);
        hintArea.anchorMax = new Vector2(1f, 0.15f);

        return prompts;
    }

    ///<summary>
    /// A hidden, dark, full-screen panel with centred white text
    ///</summary>
    GameObject CreatePanel(string panelName, float backgroundAlpha, float fontSize, out TMP_Text text)
    {
        GameObject panel = new GameObject(panelName, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(transform, false);
        Stretch((RectTransform)panel.transform);

        Image background = panel.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, backgroundAlpha);
        background.raycastTarget = false;

        GameObject label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(panel.transform, false);
        Stretch((RectTransform)label.transform);

        text = label.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;

        panel.SetActive(false);
        return panel;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
