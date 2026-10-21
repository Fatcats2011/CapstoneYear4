using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Tools → Dead on Arrival → Dispatch: sets up the Dispatch room's scene (Multiplayerlobby) from the model's named
/// empties, and builds and wires the board's UI in the menu scene. Both can be run again: the room is rewired, and the
/// board is rebuilt in place of the old one. See docs/online.md
/// </summary>
public static class DispatchLobbyBuilder
{
    const string MENU = "Tools/Dead on Arrival/Dispatch/";
    public const string ROOM_SCENE = "Assets/Scenes/Multiplayerlobby.unity";
    public const string MENU_SCENE = "Assets/Scenes/Alex Player Testing.unity";
    /// <summary>The camera the room setup makes and frames (any other camera in the room's scene is turned off)</summary>
    public const string ROOM_CAMERA = "Dispatch Room Camera";
    /// <summary>The room's directional light, dimmed: the board sits back and the spotlit podiums stand out</summary>
    public const float ROOM_FILL = 0.45f;
    /// <summary>The room's size against the scooters: the model is 1:1 Blender units, which leaves them tiny on its podiums</summary>
    public const float ROOM_SCALE = 0.4f;
    /// <summary>The band the seats and tickets are framed in (viewport heights): above the prompts, below the top row</summary>
    public const float FRAME_BOTTOM = 0.12f;
    public const float FRAME_TOP = 0.84f;
    const float SPOT_HEIGHT = 0.9f;     // above each seat, in seat gaps
    const float SPOT_ANGLE = 75f;
    const float SPOT_BRIGHTNESS = 3f; // at the seat: URP's falloff is inverse-square, so intensity grows with height²
    static readonly Color SpotColour = new Color(1f, 0.95f, 0.85f);
    // Blender's startup objects, exported along with the room
    static readonly string[] Strays = { "Cube", "Light" };
    const float ROOM_FOV = 40f;
    const float ROOM_ASPECT = 16f / 9f;
    const string TITLE_CANVAS = "Main Menu Canvas";
    static readonly string[] OldLobby = { "Player Backgrounds", "Player Icons", "Dividor X", "Dividor Y" };
    static readonly string[] SolidNames = { "Depot_Floor", "Podium1", "Podium2", "Podium3", "Podium4",
        "Podium1_Base", "Podium2_Base", "Podium3_Base", "Podium4_Base" };
    const string SPRITES = "Assets/Sprites/UI/Dispatch/";
    const string BUTTONS = "Assets/Sprites/UI/MenUI_Buttons.png";
    const string FONT = "Assets/Plugins/TextMesh Pro/Examples & Extras/Resources/Fonts & Materials/Bangers SDF.asset";
    const string FLASH_FOLDER = "Assets/Animations/Dispatch";
    const string FLASH_CONTROLLER = FLASH_FOLDER + "/TicketFlash.controller";
    const string FLASH_CLIP = FLASH_FOLDER + "/TicketFlash.anim";

    static readonly Color Ink = new Color32(0x2A, 0x1A, 0x3A, 0xFF);
    static readonly Color Rule = new Color32(0xBB, 0xBB, 0xBB, 0xFF);
    static readonly Color StampRed = new Color32(0xD8, 0x1B, 0x3C, 0xFF);

    // The layers the room camera leaves to the menus' own cameras
    static readonly string[] HiddenFromRoom =
    {
        "UI", "Minimap", "MenuUI", "Player1Cam", "Player2Cam", "Player3Cam", "Player4Cam", "CutsceneCam",
        "Player 1 UI", "Player 2 UI", "Player 3 UI", "Player 4 UI",
    };

    // ---------------------------------------------------------------- The room scene

    [MenuItem(MENU + "Set Up Room Scene", false, 60)]
    static void SetUpRoomScene()
    {
        bool wasOpen = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(ROOM_SCENE).isLoaded;
        Scene scene = OpenScene(ROOM_SCENE);
        try
        {
            DispatchRoom room = SetUpRoom(scene.GetRootGameObjects());
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuild(ROOM_SCENE);
            Debug.Log("Dispatch room set up on " + room.name + ": its own camera framed on the seats and board, colliders on " +
                "the floor and podiums. " + ROOM_SCENE + " is in the build.");
        }
        catch (InvalidOperationException e)
        {
            EditorUtility.DisplayDialog("Dispatch room", e.Message, "OK");
        }

        // The game loads the room alongside the menu itself: left open here, it would be up from the moment Play starts
        if (!wasOpen && UnityEngine.SceneManagement.SceneManager.sceneCount > 1)
            EditorSceneManager.CloseScene(scene, true);
    }

    /// <summary>
    /// Puts a DispatchRoom on the model (the root holding Seat1_Spawn) and wires its seats, ticket anchors, rims and
    /// camera: the model's own camera if it has one, else the scene's. Every other camera is turned off and every audio
    /// listener removed (the menu has both). Throws when a seat or ticket anchor is missing
    /// </summary>
    public static DispatchRoom SetUpRoom(GameObject[] roots)
    {
        GameObject model = roots.FirstOrDefault(r => Find(r.transform, "Seat1_Spawn") != null);
        if (model == null)
            throw new InvalidOperationException("No Seat1_Spawn in the scene: drag the lobby model (MultiplayerLobby.fbx) in first.");

        if (model.transform.localScale != Vector3.one * ROOM_SCALE)
        {
            Undo.RecordObject(model.transform, "Dispatch room scale");
            model.transform.localScale = Vector3.one * ROOM_SCALE;
        }

        GameObject[] seats = new GameObject[Constants.MAX_PLAYERS];
        Transform[] anchors = new Transform[Constants.MAX_PLAYERS];
        Renderer[] rims = new Renderer[Constants.MAX_PLAYERS];
        for (int i = 0; i < Constants.MAX_PLAYERS; i++)
        {
            int n = i + 1;
            Transform seat = Find(model.transform, "Seat" + n + "_Spawn");
            Transform anchor = Find(model.transform, "Seat" + n + "_Ticket");
            if (seat == null || anchor == null)
                throw new InvalidOperationException("The model has no Seat" + n + "_Spawn or Seat" + n + "_Ticket.");
            seats[i] = seat.gameObject;
            anchors[i] = anchor;
            Transform rim = Find(model.transform, "Podium" + n + "_Rim");
            rims[i] = rim == null ? null : rim.GetComponent<Renderer>();
        }

        Camera cam = RoomCamera(model);
        SetUpCamera(cam, roots);
        Frame(cam, seats.Select(s => s.transform.position).ToArray(), anchors.Select(a => a.position).ToArray());
        AddColliders(model.transform);
        HideStrays(model.transform);
        Spotlight(model.transform, seats);
        foreach (Light sun in roots.SelectMany(r => r.GetComponentsInChildren<Light>(true)).Where(l => l.type == LightType.Directional))
            sun.intensity = ROOM_FILL;

        DispatchRoom room = model.GetComponent<DispatchRoom>();
        if (room == null)
            room = Undo.AddComponent<DispatchRoom>(model);

        SerializedObject so = new SerializedObject(room);
        SetArray(so.FindProperty("seats"), seats);
        SetArray(so.FindProperty("ticketAnchors"), anchors);
        SetArray(so.FindProperty("rims"), rims);
        SetArray(so.FindProperty("dimmed"), model.GetComponentsInChildren<Renderer>(true)
            .Where(r => !r.name.StartsWith("Podium")).ToArray());
        so.FindProperty("roomCamera").objectReferenceValue = cam;
        so.ApplyModifiedPropertiesWithoutUndo();
        return room;
    }

    // The room's own camera, under the model so it moves with it. A camera in the model or scene isn't trusted: Blender's
    // default one comes along with an export, aimed at the origin
    static Camera RoomCamera(GameObject model)
    {
        Transform existing = model.transform.Find(ROOM_CAMERA);
        Camera cam = existing == null ? null : existing.GetComponent<Camera>();
        if (cam != null)
            return cam;

        GameObject go = new GameObject(ROOM_CAMERA, typeof(Camera));
        go.transform.SetParent(model.transform, false);
        Undo.RegisterCreatedObjectUndo(go, "Dispatch room camera");
        cam = go.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x2A, 0x0B, 0x55, 0xFF);
        return cam;
    }

    /// <summary>
    /// Aims the camera from in front of the seats at the board behind them, level with seat 1 on the left (seat 1 to seat
    /// 4 is its right), far enough back that every seat and ticket fits, a little from above
    /// </summary>
    static void Frame(Camera cam, Vector3[] seats, Vector3[] tickets)
    {
        Vector3[] points = seats.Concat(tickets).ToArray();
        Vector3 centre = points.Aggregate(Vector3.zero, (a, p) => a + p) / points.Length;
        Vector3 right = Vector3.ProjectOnPlane(seats[seats.Length - 1] - seats[0], Vector3.up).normalized;
        Vector3 forward = Vector3.Cross(right, Vector3.up);
        Vector3 seatsToBoard = tickets.Aggregate(Vector3.zero, (a, p) => a + p) / tickets.Length
                             - seats.Aggregate(Vector3.zero, (a, p) => a + p) / seats.Length;
        if (Vector3.Dot(forward, seatsToBoard) < 0)
            Debug.LogWarning("Dispatch room: the board is in front of the seats as seat 1 → 4 runs, so the camera sees seat 1 on the right. Is the model mirrored?");

        // The margin is the scooters' and the ticket cards' room around the points: a seat gap's worth
        float margin = Vector3.Distance(seats[0], seats[1]) * 0.5f;
        float halfWidth = points.Max(p => Mathf.Abs(Vector3.Dot(p - centre, right))) + margin;
        float halfHeight = points.Max(p => Mathf.Abs(p.y - centre.y)) + margin;
        float tan = Mathf.Tan(ROOM_FOV * 0.5f * Mathf.Deg2Rad);
        float band = FRAME_TOP - FRAME_BOTTOM;
        float depth = points.Max(p => Mathf.Abs(Vector3.Dot(p - centre, forward)));
        float distance = Mathf.Max(halfHeight / (tan * band), halfWidth / (tan * ROOM_ASPECT * 0.9f)) + depth;

        // Aimed so the points' centre sits at the band's centre, not the screen's
        Vector3 aim = centre + Vector3.up * (0.5f - (FRAME_TOP + FRAME_BOTTOM) * 0.5f) * 2 * tan * distance;
        cam.fieldOfView = ROOM_FOV;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = distance * 4;
        cam.transform.position = aim - forward * distance + Vector3.up * distance * 0.12f;
        cam.transform.rotation = Quaternion.LookRotation(aim - cam.transform.position, Vector3.up);
    }

    // Blender's default cube and light, which came along with the export
    static void HideStrays(Transform model)
    {
        foreach (Transform child in model)
        {
            if (Strays.Contains(child.name))
                child.gameObject.SetActive(false);
        }
    }

    // A spotlight above each seat, aimed straight down at it (made once, then kept up to date)
    static void Spotlight(Transform model, GameObject[] seats)
    {
        float height = Vector3.Distance(seats[0].transform.position, seats[1].transform.position) * SPOT_HEIGHT;
        for (int i = 0; i < seats.Length; i++)
        {
            string name = "Seat" + (i + 1) + "_Spot";
            Transform existing = model.Find(name);
            Light spot = existing == null ? null : existing.GetComponent<Light>();
            if (spot == null)
            {
                GameObject go = new GameObject(name, typeof(Light));
                go.transform.SetParent(model, false);
                Undo.RegisterCreatedObjectUndo(go, "Dispatch spotlight");
                spot = go.GetComponent<Light>();
            }

            Vector3 seat = seats[i].transform.position;
            spot.type = LightType.Spot;
            spot.spotAngle = SPOT_ANGLE;
            spot.range = height * 2;
            spot.intensity = SPOT_BRIGHTNESS * height * height;
            spot.color = SpotColour;
            spot.shadows = LightShadows.Soft;
            spot.transform.position = seat + Vector3.up * height;
            spot.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
        }
    }

    // The scooters stand on the podiums, so the floor and the podiums need colliders (the model imports without)
    static void AddColliders(Transform model)
    {
        foreach (string name in SolidNames)
        {
            Transform solid = Find(model, name);
            if (solid == null || solid.GetComponent<Collider>() != null)
                continue;
            MeshFilter mesh = solid.GetComponent<MeshFilter>();
            if (mesh == null || mesh.sharedMesh == null)
                continue;
            Undo.AddComponent<MeshCollider>(solid.gameObject).sharedMesh = mesh.sharedMesh;
        }
    }

    // The room's camera draws over the players' views (CameraBudget turns theirs off), sees none of the menus' layers,
    // and is the only camera on in the room's scene
    static void SetUpCamera(Camera cam, GameObject[] roots)
    {
        foreach (Camera other in roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)))
        {
            if (other != cam)
                other.enabled = false;
        }
        foreach (AudioListener listener in roots.SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).ToList())
            UnityEngine.Object.DestroyImmediate(listener);

        cam.gameObject.SetActive(true);
        cam.enabled = true;
        cam.targetTexture = null;
        cam.rect = new Rect(0, 0, 1, 1);
        cam.depth = Mathf.Max(cam.depth, CameraBudget.COVER_DEPTH);
        if (cam.clearFlags != CameraClearFlags.Skybox && cam.clearFlags != CameraClearFlags.SolidColor)
            cam.clearFlags = CameraClearFlags.SolidColor;

        int mask = cam.cullingMask;
        foreach (string layer in HiddenFromRoom)
        {
            int index = LayerMask.NameToLayer(layer);
            if (index >= 0)
                mask &= ~(1 << index);
        }
        cam.cullingMask = mask;
    }

    // ---------------------------------------------------------------- The board

    [MenuItem(MENU + "Build Board In Menu Scene", false, 61)]
    static void BuildBoardInMenuScene()
    {
        Scene scene = OpenScene(MENU_SCENE);
        DispatchBoard[] old = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<DispatchBoard>(true)).ToArray();
        if (old.Length > 0 && !EditorUtility.DisplayDialog("Dispatch board",
                "The menu scene already has a Dispatch board. Replace it?", "Replace", "Cancel"))
            return;
        foreach (DispatchBoard b in old)
            Undo.DestroyObjectImmediate(b.GetComponent<Canvas>() != null ? b.gameObject : b.transform.root.gameObject);

        DispatchBoard board = BuildBoard();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(board.gameObject, scene);
        Undo.RegisterCreatedObjectUndo(board.gameObject, "Build Dispatch board");

        PlayerSelectCanvas canvas = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerSelectCanvas>(true)).FirstOrDefault();
        string views = "no Player Select Canvas found";
        if (canvas != null)
        {
            WirePlayerViews(canvas);
            EditorUtility.SetDirty(canvas);
            views = canvas.playerViews.Count(v => v != null) + " of 4 player views and " + canvas.dispatchHides.Length + " old lobby parts wired";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = board.gameObject;
        Debug.Log("Dispatch board built in " + MENU_SCENE + " (" + views + ").");
    }

    /// <summary>
    /// The board's canvas, all its parts and its 4 tickets, wired, with the Dispatch art. The root and the banner start
    /// hidden; the board shows them
    /// </summary>
    public static DispatchBoard BuildBoard()
    {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FONT);

        GameObject canvasObject = new GameObject("Dispatch Board", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.layer = LayerMask.NameToLayer("UI");
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        DispatchBoard board = canvasObject.AddComponent<DispatchBoard>();

        RectTransform root = Stretch(Ui("Dispatch Root", canvasObject.transform));

        Image wordmark = Picture("Wordmark", root, "Dispatch_Wordmark");
        wordmark.preserveAspect = true;
        Place(wordmark.rectTransform, new Vector2(0, 1), new Vector2(40, -28), new Vector2(560, 113));

        TMP_Text readyLine = Pill("Ready", root, font, new Vector2(-40, -40), new Vector2(340, 64), 36, "0 / 0 READY");
        TMP_Text matchmakingLine = Pill("Matchmaking", root, font, new Vector2(-40, -116), new Vector2(560, 54), 28, "FRIENDS ONLY");

        RectTransform area = Stretch(Ui("Tickets", root));
        TicketView[] tickets = new TicketView[Constants.MAX_PLAYERS];
        AnimatorController flash = FlashController();
        for (int i = 0; i < tickets.Length; i++)
            tickets[i] = Ticket(i, area, font, flash);

        Image banner = Picture("Banner", root, "Banner");
        banner.type = Image.Type.Sliced;
        Place(banner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(1150, 210));
        Label("Words", banner.transform, font, 76, Color.white, "ALL ORDERS DISPATCHED");
        banner.gameObject.SetActive(false);

        RectTransform prompts = Ui("Prompts", root);
        Place(prompts, new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(1400, 64));
        HorizontalLayoutGroup row = prompts.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.spacing = 48;
        row.childAlignment = TextAnchor.MiddleCenter;
        row.childControlWidth = row.childControlHeight = false;
        row.childForceExpandWidth = row.childForceExpandHeight = false;
        Prompt(prompts, font, "MenUI_Buttons_7", "CLOCK IN");
        Prompt(prompts, font, "MenUI_Buttons_2", "CUSTOMIZE");
        Prompt(prompts, font, "MenUI_Buttons_3", "INVITE");
        Prompt(prompts, font, "MenUI_Buttons_4", "LEAVE");

        TMP_Text fillPrompt = Label("Fill Prompt", root, font, 30, Color.white, "RB  OPEN TO ANYONE");
        fillPrompt.alignment = TextAlignmentOptions.Right;
        Place(fillPrompt.rectTransform, new Vector2(1, 0), new Vector2(-40, 40), new Vector2(520, 50));
        fillPrompt.gameObject.SetActive(false);

        root.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(board);
        so.FindProperty("root").objectReferenceValue = root.gameObject;
        SetArray(so.FindProperty("tickets"), tickets);
        so.FindProperty("readyLine").objectReferenceValue = readyLine;
        so.FindProperty("banner").objectReferenceValue = banner.gameObject;
        so.FindProperty("matchmakingLine").objectReferenceValue = matchmakingLine;
        so.FindProperty("fillPrompt").objectReferenceValue = fillPrompt;
        so.ApplyModifiedPropertiesWithoutUndo();
        return board;
    }

    // One ticket (300 × 375, the paper's shape), centre-anchored so the board can set it over its seat's anchor
    static TicketView Ticket(int seat, RectTransform area, TMP_FontAsset font, AnimatorController flash)
    {
        RectTransform t = Ui("Ticket " + (seat + 1), area);
        Place(t, new Vector2(0.5f, 0.5f), new Vector2(-540 + 360 * seat, 170), new Vector2(300, 375));
        t.pivot = new Vector2(0.5f, 0.5f);
        TicketView view = t.gameObject.AddComponent<TicketView>();
        Animator animator = t.gameObject.AddComponent<Animator>();
        animator.runtimeAnimatorController = flash;

        Image glow = Picture("Glow", t, "Ticket_Glow");
        Place(glow.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(375, 450));
        glow.color = new Color(1, 1, 1, 0);

        Image paper = Picture("Paper", t, "Ticket_Paper");
        Stretch(paper.rectTransform);

        Image pin = Picture("Pin", t, "Ticket_Pin");
        Place(pin.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 14), new Vector2(48, 48));

        RectTransform filled = Stretch(Ui("Filled", t));

        Image header = Picture("Header", filled, "Ticket_Header");
        header.type = Image.Type.Sliced;
        Place(header.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(250, 46));
        TMP_Text company = Label("Company", header.transform, font, 26, Color.white, "COMPANY");

        TMP_Text playerName = Label("Name", filled, font, 40, Ink, "PLAYER");
        Place(playerName.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -96), new Vector2(240, 48));
        playerName.enableAutoSizing = true;
        playerName.fontSizeMin = 22;
        playerName.fontSizeMax = 40;

        Image mine = Picture("Mine", filled, "Ticket_Mine");
        Place(mine.rectTransform, new Vector2(1, 1), new Vector2(-14, -84), new Vector2(40, 40));

        Image rule = new GameObject("Rule", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        rule.transform.SetParent(filled, false);
        rule.color = Rule;
        rule.raycastTarget = false;
        Place(rule.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(240, 3));

        Image swatch = Row("Colour", filled, font, -166, out TMP_Text colourName);
        Image hatIcon = Row("Hat", filled, font, -204, out TMP_Text hatName);

        TMP_Text gettingReady = Label("Getting Ready", filled, font, 28, Ink, TicketView.GETTING_READY);
        Place(gettingReady.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(260, 44));

        RectTransform stamp = Ui("Stamp", filled);
        // Below the colour and hat lines, even tilted
        Place(stamp, new Vector2(0.5f, 0), new Vector2(0, 52), new Vector2(232, 70));
        stamp.localEulerAngles = new Vector3(0, 0, -10);
        Image frame = Picture("Frame", stamp, "Ticket_Stamp");
        frame.type = Image.Type.Sliced;
        frame.color = StampRed;
        Stretch(frame.rectTransform);
        Label("Word", stamp, font, 40, StampRed, TicketView.DISPATCHED);
        Image ink = Picture("Ink", stamp, "Ticket_StampInk");
        Stretch(ink.rectTransform);
        ink.color = new Color(1, 1, 1, 0.45f); // patchy, but the word reads

        RectTransform hiring = Stretch(Ui("Hiring", t));
        Image blank = Picture("Blank", hiring, "Ticket_Hiring");
        Stretch(blank.rectTransform);
        Label("Words", hiring, font, 44, Color.white, TicketView.HIRING);
        hiring.gameObject.SetActive(false);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("header").objectReferenceValue = header;
        so.FindProperty("company").objectReferenceValue = company;
        so.FindProperty("playerName").objectReferenceValue = playerName;
        so.FindProperty("colourName").objectReferenceValue = colourName;
        so.FindProperty("hatName").objectReferenceValue = hatName;
        so.FindProperty("colourSwatch").objectReferenceValue = swatch;
        so.FindProperty("hatIcon").objectReferenceValue = hatIcon;
        so.FindProperty("filled").objectReferenceValue = filled.gameObject;
        so.FindProperty("hiring").objectReferenceValue = hiring.gameObject;
        so.FindProperty("stamp").objectReferenceValue = stamp.gameObject;
        so.FindProperty("gettingReady").objectReferenceValue = gettingReady.gameObject;
        so.FindProperty("mineMarker").objectReferenceValue = mine.gameObject;
        so.FindProperty("highlight").objectReferenceValue = animator;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    // An icon and its name, on one line of the ticket
    static Image Row(string name, RectTransform parent, TMP_FontAsset font, float y, out TMP_Text text)
    {
        Image icon = new GameObject(name + " Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        icon.transform.SetParent(parent, false);
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        Place(icon.rectTransform, new Vector2(0.5f, 1), new Vector2(-100, y), new Vector2(38, 38));

        text = Label(name + " Name", parent, font, 26, Ink, name.ToUpperInvariant());
        text.font = TMP_Settings.defaultFontAsset != null ? TMP_Settings.defaultFontAsset : font;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Left;
        Place(text.rectTransform, new Vector2(0.5f, 1), new Vector2(26, y), new Vector2(190, 38));
        return icon;
    }

    static TMP_Text Pill(string name, RectTransform root, TMP_FontAsset font, Vector2 position, Vector2 size, float fontSize, string text)
    {
        Image pill = Picture(name + " Pill", root, "Pill");
        pill.type = Image.Type.Sliced;
        Place(pill.rectTransform, new Vector2(1, 1), position, size);
        pill.rectTransform.pivot = new Vector2(1, 1);
        return Label(name + " Line", pill.transform, font, fontSize, Color.white, text);
    }

    static void Prompt(RectTransform row, TMP_FontAsset font, string buttonSprite, string word)
    {
        RectTransform prompt = Ui(word, row);
        prompt.sizeDelta = new Vector2(68 + word.Length * 22, 64);

        Image button = new GameObject("Button", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        button.transform.SetParent(prompt, false);
        button.sprite = ButtonSprite(buttonSprite);
        button.preserveAspect = true;
        button.raycastTarget = false;
        Place(button.rectTransform, new Vector2(0, 0.5f), Vector2.zero, new Vector2(56, 56));

        TMP_Text label = Label("Label", prompt, font, 36, Color.white, word);
        label.alignment = TextAlignmentOptions.Left;
        label.rectTransform.anchorMin = new Vector2(0, 0);
        label.rectTransform.anchorMax = new Vector2(1, 1);
        label.rectTransform.offsetMin = new Vector2(68, 0);
        label.rectTransform.offsetMax = Vector2.zero;
    }

    /// <summary>The ticket's Flash: its glow up to full and back over 0.6 s (made once, under Assets/Animations/Dispatch)</summary>
    public static AnimatorController FlashController()
    {
        AnimatorController existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(FLASH_CONTROLLER);
        if (existing != null)
            return existing;

        if (!AssetDatabase.IsValidFolder(FLASH_FOLDER))
            AssetDatabase.CreateFolder("Assets/Animations", "Dispatch");

        AnimationClip clip = new AnimationClip { name = "TicketFlash" };
        AnimationCurve alpha = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.12f, 1), new Keyframe(0.6f, 0));
        clip.SetCurve("Glow", typeof(Image), "m_Color.a", alpha);
        AssetDatabase.CreateAsset(clip, FLASH_CLIP);

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(FLASH_CONTROLLER);
        controller.AddParameter("Flash", AnimatorControllerParameterType.Trigger);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState idle = machine.AddState("Idle");
        AnimatorState flash = machine.AddState("Flash");
        flash.motion = clip;
        machine.defaultState = idle;
        AnimatorStateTransition into = machine.AddAnyStateTransition(flash);
        into.AddCondition(AnimatorConditionMode.If, 0, "Flash");
        into.duration = 0;
        into.canTransitionToSelf = true;
        AnimatorStateTransition back = flash.AddTransition(idle);
        back.hasExitTime = true;
        back.exitTime = 1;
        back.duration = 0;
        AssetDatabase.SaveAssets();
        return controller;
    }

    /// <summary>The Player Select Canvas's preview panels (Player 1 Cam … Player 4 Cam), by seat</summary>
    public static void WirePlayerViews(PlayerSelectCanvas canvas)
    {
        GameObject[] views = new GameObject[Constants.MAX_PLAYERS];
        for (int i = 0; i < views.Length; i++)
        {
            Transform view = Find(canvas.transform, "Player " + (i + 1) + " Cam");
            views[i] = view == null ? null : view.gameObject;
        }

        // The old lobby's look, which the canvas draws over the room. Not the countdown: Dispatch's 3-2-1 is on it
        GameObject[] hides = OldLobby.Select(n => Find(canvas.transform, n)).Where(t => t != null).Select(t => t.gameObject).ToArray();

        // The title screen's canvas, under player select's until now: its drawing stops (its scripts keep running)
        Canvas[] canvases = canvas.gameObject.scene.GetRootGameObjects()
            .Where(r => r.name == TITLE_CANVAS && r.GetComponent<Canvas>() != null)
            .Select(r => r.GetComponent<Canvas>()).ToArray();

        SerializedObject so = new SerializedObject(canvas);
        SetArray(so.FindProperty("playerViews"), views);
        SetArray(so.FindProperty("dispatchHides"), hides);
        SetArray(so.FindProperty("dispatchHideCanvases"), canvases);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------- Helpers

    static Scene OpenScene(string path)
    {
        Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
        if (scene.isLoaded)
            return scene;
        return EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
    }

    static void AddToBuild(string path)
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == path))
            return;
        scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    static Transform Find(Transform root, string name)
    {
        if (root.name == name)
            return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null)
                return found;
        }
        return null;
    }

    static void SetArray<T>(SerializedProperty array, T[] values) where T : UnityEngine.Object
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static RectTransform Ui(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(anchor.x == 0.5f ? 0.5f : anchor.x, anchor.y == 0.5f ? 0.5f : anchor.y);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static Image Picture(string name, Transform parent, string sprite)
    {
        Image image = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        image.gameObject.layer = LayerMask.NameToLayer("UI");
        image.transform.SetParent(parent, false);
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SPRITES + sprite + ".png");
        image.raycastTarget = false;
        return image;
    }

    static Sprite ButtonSprite(string name)
    {
        return AssetDatabase.LoadAllAssetsAtPath(BUTTONS).OfType<Sprite>().FirstOrDefault(s => s.name == name);
    }

    static TMP_Text Label(string name, Transform parent, TMP_FontAsset font, float size, Color colour, string text)
    {
        TextMeshProUGUI label = new GameObject(name, typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
        label.gameObject.layer = LayerMask.NameToLayer("UI");
        label.transform.SetParent(parent, false);
        Stretch(label.rectTransform);
        if (font != null)
            label.font = font;
        label.fontSize = size;
        label.color = colour;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }
}
