using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// X's pop-up on Dispatch, in the game's own card (GameCard: a purple panel in a dark rounded frame, Bangers, gold for
/// what's picked): CUSTOMIZE, the player's ghost from their preview (cropped to it, DispatchRules.CustomizeCrop) in a
/// dark rounded inset, a COLOUR and a HAT row each showing "&lt; icon NAME &gt;" for the choice now (the row the d-pad
/// changes in gold), and DONE with the back button of the player's device. Over the room dimmed. Built by code at
/// runtime on the board's canvas (DispatchBoard), so the scene needs no rebuild. See docs/online.md
/// </summary>
public class CustomizeCard : MonoBehaviour
{
    /// <summary>The ghost's picture's width on the card (canvas units, 1920 x 1080); its height follows the crop</summary>
    public const float PICTURE_WIDTH = 440f;

    const float INSET_EDGE = 6f;
    const float ROW_WIDTH = 460f;
    const float ROW_HEIGHT = 64f;
    const float ICON_SIZE = 50f;
    const float NAME_WIDTH = 190f;
    const float LABEL_WIDTH = 120f;
    static readonly Color DIM = new Color(0f, 0f, 0f, 0.6f);
    static readonly Color ROW = new Color(0f, 0f, 0f, 0.18f);
    static readonly Color ROW_PICKED = new Color(GameCard.GOLD.r, GameCard.GOLD.g, GameCard.GOLD.b, 0.22f);
    static readonly Color ARROW_IDLE = new Color(1f, 1f, 1f, 0.3f);

    // One row: COLOUR or HAT, then < icon NAME >
    class Row
    {
        public Image Back;
        public TMP_Text Label, Left, Name, Right;
        public Image Icon;
    }

    RawImage picture;
    Row colourRow, hatRow;
    ButtonGlyph doneGlyph;
    InputDevice glyphFor;

    /// <summary>The words each row shows now (tests)</summary>
    internal string ColourShown { get { return colourRow.Name.text; } }
    internal string HatShown { get { return hatRow.Name.text; } }

    /// <summary>Whether the hat row is the picked one (in gold) rather than the colour row (tests)</summary>
    internal bool HatPicked { get; private set; }

    /// <summary>The ghost's picture (tests)</summary>
    internal RawImage Picture { get { return picture; } }

    /// <summary>Builds the pop-up, closed, over everything else under parent (the board's canvas root)</summary>
    public static CustomizeCard Build(Transform parent)
    {
        GameObject holder = new GameObject("Customize Card", typeof(RectTransform));
        RectTransform root = (RectTransform)holder.transform;
        root.SetParent(parent, false);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        Image dim = holder.AddComponent<Image>();
        dim.color = DIM;
        dim.raycastTarget = false;

        CustomizeCard card = holder.AddComponent<CustomizeCard>();
        card.BuildCard(root);
        holder.SetActive(false);
        return card;
    }

    void BuildCard(RectTransform root)
    {
        RectTransform box = GameCard.Box(root, "Card", out RectTransform content);
        box.anchorMin = box.anchorMax = box.pivot = new Vector2(0.5f, 0.5f);
        box.anchoredPosition = Vector2.zero;
        VerticalLayoutGroup column = content.GetComponent<VerticalLayoutGroup>();
        column.spacing = 10f;
        column.padding = new RectOffset(26, 26, 10, 16);

        TMP_Text title = GameCard.Words(content, "Title", 56f, Color.white);
        LocalizedText.On(title, "dispatch.customize");

        // The ghost, in a dark rounded inset: the frame's colour round a rounded window that clips the picture
        Rect crop = DispatchRules.CustomizeCrop(0);
        Vector2 size = PictureSize(crop);
        Image inset = Rounded(content, "Inset", GameCard.FRAME);
        LayoutElement insetSize = inset.gameObject.AddComponent<LayoutElement>();
        insetSize.preferredWidth = size.x + INSET_EDGE * 2f;
        insetSize.preferredHeight = size.y + INSET_EDGE * 2f;
        Image window = Rounded(inset.rectTransform, "Window", Color.white);
        Stretch(window.rectTransform, INSET_EDGE);
        window.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        picture = new GameObject("Ghost", typeof(RectTransform)).AddComponent<RawImage>();
        picture.rectTransform.SetParent(window.rectTransform, false);
        Stretch(picture.rectTransform, 0f);
        picture.raycastTarget = false;
        picture.uvRect = crop;

        // In the sliders' order: the d-pad's up and down move between them
        colourRow = BuildRow(content, "Colour", "dispatch.colour");
        hatRow = BuildRow(content, "Hat", "dispatch.hat");

        BuildDone(content);
    }

    /// <summary>The ghost's picture's size on the card for a crop of the 16:9 preview: PICTURE_WIDTH wide, as tall as its shape</summary>
    public static Vector2 PictureSize(Rect crop)
    {
        float aspect = crop.height <= 0f ? 1f : (crop.width * 16f) / (crop.height * 9f);
        return new Vector2(PICTURE_WIDTH, Mathf.Round(PICTURE_WIDTH / aspect));
    }

    Row BuildRow(RectTransform parent, string name, string labelKey)
    {
        Row row = new Row();
        row.Back = Rounded(parent, name + " Row", ROW);
        LayoutElement size = row.Back.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = ROW_WIDTH;
        size.preferredHeight = ROW_HEIGHT;
        HorizontalLayoutGroup line = row.Back.gameObject.AddComponent<HorizontalLayoutGroup>();
        line.padding = new RectOffset(16, 12, 4, 4);
        line.spacing = 8f;
        line.childAlignment = TextAnchor.MiddleLeft;
        line.childControlWidth = line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;

        row.Label = Fixed(GameCard.Words(row.Back.rectTransform, "Label", 30f, Color.white), LABEL_WIDTH);
        row.Label.alignment = TextAlignmentOptions.Left;
        LocalizedText.On(row.Label, labelKey);
        row.Left = Fixed(GameCard.Words(row.Back.rectTransform, "Left", 40f, Color.white), 26f);
        row.Left.text = "<";

        row.Icon = new GameObject("Icon", typeof(RectTransform)).AddComponent<Image>();
        row.Icon.rectTransform.SetParent(row.Back.rectTransform, false);
        row.Icon.preserveAspect = true;
        row.Icon.raycastTarget = false;
        LayoutElement icon = row.Icon.gameObject.AddComponent<LayoutElement>();
        icon.preferredWidth = icon.preferredHeight = ICON_SIZE;

        row.Name = Fixed(GameCard.Words(row.Back.rectTransform, "Name", 34f, Color.white), NAME_WIDTH);
        row.Name.richText = false;
        row.Name.enableWordWrapping = false;
        row.Name.overflowMode = TextOverflowModes.Ellipsis;
        row.Right = Fixed(GameCard.Words(row.Back.rectTransform, "Right", 40f, Color.white), 26f);
        row.Right.text = ">";
        return row;
    }

    // DONE, after the back button of the customizing player's device (B / circle / Backspace)
    void BuildDone(RectTransform parent)
    {
        GameObject done = new GameObject("Done", typeof(RectTransform));
        done.transform.SetParent(parent, false);
        HorizontalLayoutGroup line = done.AddComponent<HorizontalLayoutGroup>();
        line.spacing = 10f;
        line.childAlignment = TextAnchor.MiddleCenter;
        line.childControlWidth = line.childControlHeight = true;
        line.childForceExpandWidth = line.childForceExpandHeight = false;

        ButtonArtSet art = ButtonArtSet.Results();
        if (art != null && art.backXbox != null)
        {
            Image button = new GameObject("Button", typeof(RectTransform)).AddComponent<Image>();
            button.rectTransform.SetParent(done.transform, false);
            button.sprite = art.backXbox;
            button.preserveAspect = true;
            button.raycastTarget = false;
            LayoutElement size = button.gameObject.AddComponent<LayoutElement>();
            size.preferredWidth = size.preferredHeight = 48f;
            doneGlyph = button.gameObject.AddComponent<ButtonGlyph>();
            doneGlyph.xbox = art.backXbox;
            doneGlyph.playStation = art.backPlayStation;
            doneGlyph.keyboard = art.backKeyboard;
            doneGlyph.Show(LastDevice.Current);
        }

        TMP_Text word = GameCard.Words(done.transform, "Word", 36f, Color.white);
        LocalizedText.On(word, "dispatch.done");
    }

    /// <summary>
    /// Opens the pop-up on a player's preview and choices (preview null: closes it). device is the customizing player's,
    /// for DONE's button art. Only what changed is touched
    /// </summary>
    public void Show(Texture preview, CustomizationSelector choices, InputDevice device)
    {
        bool open = preview != null && choices != null;
        if (gameObject.activeSelf != open)
            gameObject.SetActive(open);
        if (!open)
        {
            glyphFor = null;
            return;
        }

        if (picture.texture != preview)
            picture.texture = preview;

        PlayerColorInformationSO colour = OnlineGame.Pick(choices.Colours, choices.ColourIndex);
        PlayerHatInformationSO hat = OnlineGame.Pick(choices.Hats, choices.HatIndex);
        ShowChoices(colour == null ? null : colour.colorIcon, colour == null ? "" : NameOf(colour.displayName, colour.name),
            hat == null ? null : hat.hatIcon, hat == null ? "" : NameOf(hat.displayName, hat.name), choices.ChangingHat);

        if (doneGlyph != null && device != glyphFor)
        {
            glyphFor = device;
            doneGlyph.Show(device ?? LastDevice.Current);
        }
    }

    /// <summary>The rows' choices now, and which row the d-pad changes (in gold)</summary>
    internal void ShowChoices(Sprite colourIcon, string colourName, Sprite hatIcon, string hatName, bool hatPicked)
    {
        Fill(colourRow, colourIcon, colourName, !hatPicked);
        Fill(hatRow, hatIcon, hatName, hatPicked);
        HatPicked = hatPicked;
    }

    static void Fill(Row row, Sprite icon, string name, bool picked)
    {
        if (row.Icon.sprite != icon)
            row.Icon.sprite = icon;
        bool iconShown = icon != null;
        if (row.Icon.enabled != iconShown)
            row.Icon.enabled = iconShown;
        if (row.Name.text != name)
            row.Name.text = name;

        // Setting a colour it already has rebuilds nothing
        Color words = picked ? GameCard.GOLD : Color.white;
        Color arrows = picked ? GameCard.GOLD : ARROW_IDLE;
        row.Name.color = words;
        row.Label.color = words;
        row.Left.color = arrows;
        row.Right.color = arrows;
        row.Back.color = picked ? ROW_PICKED : ROW;
    }

    static string NameOf(string displayName, string fallback)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallback.ToUpperInvariant() : displayName.ToUpperInvariant();
    }

    static Image Rounded(RectTransform parent, string name, Color colour)
    {
        Image image = new GameObject(name, typeof(RectTransform)).AddComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.sprite = GameCard.Rounded();
        image.type = Image.Type.Sliced;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect, float edge)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(edge, edge);
        rect.offsetMax = new Vector2(-edge, -edge);
    }

    static TMP_Text Fixed(TMP_Text text, float width)
    {
        LayoutElement size = text.gameObject.AddComponent<LayoutElement>();
        size.preferredWidth = width;
        size.minWidth = width;
        return text;
    }
}
