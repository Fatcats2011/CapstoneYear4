using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;

public class OptionsMenu : SingletonMonobehaviour<OptionsMenu>, IMouseMenu
{
    const string SETTINGS_FILE_NAME = "settings.cfg";

    [Header("Options Numbers")]
    [SerializeField] float[] volumeValues;

    [Header("BGM Info")]
    [SerializeField] GameObject bgmSelector;
    [SerializeField] GameObject[] bgmSelectorPositions;
    [SerializeField] float bgmValue;
    [SerializeField] int bgmPosition;

    [Header("SFX Info")]
    [SerializeField] GameObject sfxSelector;
    [SerializeField] GameObject[] sfxSelectorPositions;
    [SerializeField] float sfxValue;
    [SerializeField] int sfxPosition;

    [Header("Fullscreen Info")]
    [SerializeField] GameObject fullscreenSelector;
    [SerializeField] GameObject[] fullscreenSelectorPositions;
    [SerializeField] int fullscreenPosition;

    [Header("Graphics Quality Info")]
    [Tooltip("Optional row: until the options screen has a Quality row, quality stays at its saved or default level")]
    [SerializeField] GameObject qualitySelector;
    [SerializeField] GameObject[] qualitySelectorPositions = new GameObject[0];
    int qualityPosition;

    bool QualityRowExists => qualitySelector != null && qualitySelectorPositions != null && qualitySelectorPositions.Length >= GraphicsQuality.LEVEL_COUNT;

    public enum OptionSelected
    {
        BGM = 0,
        SFX = 1,
        FULLSCREEN = 2,
        QUALITY = 3,
        RESOLUTION = 4,
        LANGUAGE = 5
    }
    public OptionSelected optionSelected;

    // What each selector spot is: the scene's rows in OptionSelected's order, then the Resolution row code adds (RowsFor)
    List<OptionSelected> rows;
    OptionSelected firstRow = OptionSelected.BGM; // GENERAL's first drawn row: Fullscreen once Music and Effects move to AUDIO
    ResolutionRow resolutionRow;

    /// <summary>The Resolution row code added (null until Start)</summary>
    internal ResolutionRow ResolutionRow => resolutionRow;

    [Header("Selector Objects")]
    [SerializeField] GameObject selector;
    [SerializeField] GameObject[] selectorObjects;
    int selectorPos;
    [SerializeField] MainMenu menu;

    SoundManager soundManager;
    GameSettings current; // the settings loaded or saved last, display keys included

    // The pages after GENERAL (the drawn screen), built by code, turned with the shoulders. See
    // docs/superpowers/specs/2026-10-24-options-pages-design.md
    const float HEADER_BELOW_TOP = 60f;
    static readonly int[] FRAME_CAPS = { 0, 30, 60, 120, 144, 165, 240 };
    OptionsBook book;
    readonly List<List<OptionRow>> pageRows = new List<List<OptionRow>>();
    readonly List<GameObject> pageObjects = new List<GameObject>();
    OptionsPageHeader header;
    readonly List<GameObject> drawnPage = new List<GameObject>(); // GENERAL's rows, their selectors and Resolution

    /// <summary>The pages (null until Start)</summary>
    internal OptionsBook Book => book;

    /// <summary>The code-built rows on a page (none on GENERAL)</summary>
    internal IReadOnlyList<OptionRow> RowsOn(int page) => page > 0 && page <= pageRows.Count ? pageRows[page - 1] : new List<OptionRow>();

    private void Start()
    {
        soundManager = SoundManager.Instance;
        LoadOptions();
        MoveVolumesOffGeneral();
        AddResolutionRow();
        AddLanguageRow();
        BuildPages();
    }

    // DISPLAY, AUDIO and ACCESSIBILITY: rows at the drawn rows' spacing, in the screen's font, hidden until turned to
    void BuildPages()
    {
        if (selectorObjects == null || selectorObjects.Length == 0 || !(selectorObjects[0].transform is RectTransform firstRow)
            || !(firstRow.parent is RectTransform positions) || !(positions.parent is RectTransform screen))
            return;

        book = new OptionsBook(
            OptionsPage.Keyed("options.page.general"),
            OptionsPage.Keyed("options.page.display", FullscreenStyleSetting(), VSyncSetting(), FrameCapSetting(), QualitySetting(), SplitScreenSetting(), UnitsSetting()),
            OptionsPage.Keyed("options.page.audio", MasterSetting(), MusicSetting(), EffectsSetting(), BackgroundSetting()),
            OptionsPage.Keyed("options.page.accessibility", ReduceMotionSetting()));

        TMP_Text style = positions.GetComponentInChildren<TMP_Text>(true);
        float rowX = resolutionRow != null ? ((RectTransform)resolutionRow.transform).anchoredPosition.x : firstRow.anchoredPosition.x;
        for (int page = 1; page < book.Pages.Count; page++)
        {
            GameObject holder = new GameObject("Page " + book.Pages[page].Name, typeof(RectTransform));
            RectTransform rect = holder.GetComponent<RectTransform>();
            rect.SetParent(screen, false);
            rect.anchorMin = positions.anchorMin;
            rect.anchorMax = positions.anchorMax;
            rect.pivot = positions.pivot;
            rect.sizeDelta = positions.sizeDelta;
            rect.anchoredPosition = positions.anchoredPosition;

            List<OptionRow> rowsHere = new List<OptionRow>();
            IReadOnlyList<OptionSetting> settings = book.Pages[page].Settings;
            for (int i = 0; i < settings.Count; i++)
                rowsHere.Add(OptionRow.Create(rect, style, settings[i], new Vector2(rowX, firstRow.anchoredPosition.y - i * ResolutionRow.ROW_SPACING)));
            pageRows.Add(rowsHere);
            pageObjects.Add(holder);
            holder.SetActive(false);
        }

        drawnPage.Add(positions.gameObject);
        if (bgmSelector != null && bgmSelector.transform.parent != null && bgmSelector.transform.parent != screen)
            drawnPage.Add(bgmSelector.transform.parent.gameObject);

        string[] names = new string[book.Pages.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = book.Pages[i].Name;
        header = OptionsPageHeader.Create(screen, style, names, HEADER_BELOW_TOP);
        Loc.Changed += OnLanguage;
    }

    // The language changed: the page names and every built row show it
    void OnLanguage()
    {
        if (book == null)
            return;
        string[] names = new string[book.Pages.Count];
        for (int i = 0; i < names.Length; i++)
            names[i] = book.Pages[i].Name;
        if (header != null)
            header.Rename(names);
        foreach (List<OptionRow> rowsHere in pageRows)
            foreach (OptionRow row in rowsHere)
                row.Refresh();
        if (languageRow != null)
            languageRow.Refresh();
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLanguage;
    }

    OptionSetting FullscreenStyleSetting()
    {
        OptionSetting setting = OptionSetting.KeyedToggle("options.fullscreenstyle", "options.borderless", "options.exclusive", current.exclusiveFullscreen);
        setting.Dimmed = () => current.fullscreenPosition == 0; // windowed: no fullscreen to style
        setting.Changed += value => { current.exclusiveFullscreen = value; DisplaySettings.Apply(current); };
        return setting;
    }

    OptionSetting VSyncSetting()
    {
        OptionSetting setting = OptionSetting.KeyedToggle("options.vsync", "word.off", "word.on", current.vsync);
        setting.Changed += value => { current.vsync = value; DisplaySettings.Apply(current); };
        return setting;
    }

    OptionSetting FrameCapSetting()
    {
        OptionSetting setting = new OptionSetting("FRAME CAP", FRAME_CAPS, cap => cap == 0 ? Loc.Get("word.off") : cap.ToString(), current.frameCap) { LabelKey = "options.framecap" };
        setting.Dimmed = () => current.vsync == 1; // the cap only applies with VSync off (docs/controls.md)
        setting.Changed += value => { current.frameCap = value; DisplaySettings.Apply(current); };
        return setting;
    }

    OptionSetting QualitySetting()
    {
        int[] levels = new int[GraphicsQuality.LEVEL_COUNT];
        for (int i = 0; i < levels.Length; i++)
            levels[i] = i;
        OptionSetting setting = OptionSetting.Keyed("options.quality", levels, level => "options.quality." + GraphicsQuality.LevelNames[level].ToLowerInvariant(), qualityPosition);
        setting.Changed += value => { qualityPosition = value; GraphicsQuality.Apply(value); };
        return setting;
    }

    OptionSetting SplitScreenSetting()
    {
        string[] words = { "options.split.boxed", "options.split.stacked", "options.split.sidebyside" };
        OptionSetting setting = OptionSetting.Keyed("options.split", new[] { 0, 1, 2 }, layout => words[layout], current.splitLayout);
        setting.Changed += value =>
        {
            current.splitLayout = value;
            SplitScreenLayout.TwoPlayers = (SplitLayout)value;
            if (PlayerInstantiate.Instance != null)
                PlayerInstantiate.Instance.RefreshCameraRects();
        };
        return setting;
    }

    OptionSetting MasterSetting()
    {
        int[] steps = new int[GameSettings.MAX_MASTER_POSITION + 1];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = i;
        OptionSetting setting = new OptionSetting("MASTER", steps, step => step * 10 + "%", current.masterPosition) { LabelKey = "options.master" };
        setting.Changed += value => { current.masterPosition = value; ApplyVolume(); };
        return setting;
    }

    // Music and Effects, at the drawn sliders' steps (volumeValues), shown as a share of the loudest
    OptionSetting MusicSetting()
    {
        OptionSetting setting = new OptionSetting("MUSIC", VolumeSteps(), VolumeWords, bgmPosition) { LabelKey = "options.music" };
        setting.Changed += value => { bgmPosition = value; bgmValue = volumeValues[value]; soundManager.SetMusic(bgmValue); };
        return setting;
    }

    OptionSetting EffectsSetting()
    {
        OptionSetting setting = new OptionSetting("EFFECTS", VolumeSteps(), VolumeWords, sfxPosition) { LabelKey = "options.effects" };
        setting.Changed += value => { sfxPosition = value; sfxValue = volumeValues[value]; soundManager.SetSFX(sfxValue); };
        return setting;
    }

    int[] VolumeSteps()
    {
        int[] steps = new int[volumeValues.Length];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = i;
        return steps;
    }

    string VolumeWords(int step)
    {
        return Mathf.RoundToInt(100f * step / (volumeValues.Length - 1)) + "%";
    }

    // Music and Effects live on AUDIO (playtest, 2026-10-24: audio settings weren't under Audio). GENERAL's drawn sliders
    // hide, and Fullscreen (with Resolution and Language, built under it) moves up into their place
    void MoveVolumesOffGeneral()
    {
        if (selectorObjects == null || selectorObjects.Length < 3 || bgmSelector == null || sfxSelector == null)
            return;

        Transform music = selectorObjects[0].transform, effects = selectorObjects[1].transform;
        // In local units: the canvas is saved at scale 0 and only scaled once it draws, so world distances are 0 at Start.
        // The rows' holder (Positions) and the sliders' (Audio Sliders) share the canvas at scale 1
        Vector3 up = Vector3.up * (music.localPosition.y - selectorObjects[2].transform.localPosition.y);

        music.gameObject.SetActive(false);
        effects.gameObject.SetActive(false);
        bgmSelector.SetActive(false);
        sfxSelector.SetActive(false);
        if (bgmSelectorPositions.Length > 0)
            bgmSelectorPositions[0].transform.parent.gameObject.SetActive(false);
        if (sfxSelectorPositions.Length > 0)
            sfxSelectorPositions[0].transform.parent.gameObject.SetActive(false);

        // The rest of the drawn rows: their labels and Off / On, the Fullscreen selector and its points
        foreach (Transform label in music.parent)
        {
            if (label != music && label != effects)
                label.localPosition += up;
        }
        if (fullscreenSelector != null)
            fullscreenSelector.transform.localPosition += up;
        if (fullscreenSelectorPositions.Length > 0)
            fullscreenSelectorPositions[0].transform.parent.localPosition += up;
        if (QualityRowExists)
        {
            qualitySelector.transform.localPosition += up;
            qualitySelectorPositions[0].transform.parent.localPosition += up;
        }

        GameObject[] spots = new GameObject[selectorObjects.Length - 2];
        System.Array.Copy(selectorObjects, 2, spots, 0, spots.Length);
        selectorObjects = spots;
        firstRow = OptionSelected.FULLSCREEN;
        optionSelected = firstRow; // the ghost selector stays put: Fullscreen moved up to the row it's on
    }

    OptionSetting BackgroundSetting()
    {
        OptionSetting setting = OptionSetting.Keyed("options.background", new[] { 1, 0 }, mute => mute == 1 ? "options.background.mute" : "options.background.keep", current.backgroundMute);
        setting.Changed += value => { current.backgroundMute = value; ApplyVolume(); };
        return setting;
    }

    OptionSetting UnitsSetting()
    {
        OptionSetting setting = OptionSetting.Keyed("options.units", new[] { 0, 1 }, imperial => imperial == 1 ? "options.units.imperial" : "options.units.metric", current.units);
        setting.Changed += value => { current.units = value; Units.Imperial = value == 1; };
        return setting;
    }

    OptionSetting ReduceMotionSetting()
    {
        OptionSetting setting = OptionSetting.KeyedToggle("options.reducemotion", "word.off", "word.on", current.reduceMotion);
        setting.Changed += value => { current.reduceMotion = value; Comfort.ReduceMotion = value == 1; };
        return setting;
    }

    void ApplyVolume()
    {
        AudioVolume.Set(current.masterPosition, current.backgroundMute == 1);
    }

    ///<summary>
    /// LB / RB (F / R): the previous or next page, round from the last to the first
    ///</summary>
    public void TurnPage(bool right)
    {
        if (book == null)
            return;
        book.TurnPage(right);
        ShowPage();
    }

    // The page turned to: its rows shown, the others hidden, the selector on its top row
    void ShowPage()
    {
        bool general = book.Page == 0;
        foreach (GameObject drawn in drawnPage)
            drawn.SetActive(general);
        for (int i = 0; i < pageObjects.Count; i++)
            pageObjects[i].SetActive(book.Page == i + 1);
        if (header != null)
            header.Show(book.Page);

        if (general)
        {
            selectorPos = 0;
            optionSelected = rows != null ? rows[0] : 0;
            MoveSelectorTo(selectorObjects[0].transform);
        }
        else
        {
            RefreshRows();
            MoveSelectorTo(pageRows[book.Page - 1][book.Row].transform);
        }
    }

    // A row on a code page changed: every row shows its value (one can dim another, as VSync does Frame cap)
    void RefreshRows()
    {
        if (book == null || book.Page == 0)
            return;
        foreach (OptionRow row in pageRows[book.Page - 1])
            row.Refresh();
    }

    // The mouse (MouseMenus): the page's rows, the width of the screen each (the values sit far right of the labels). A
    // click on a row steps its value towards the click; on the Resolution row's size it applies it, as A does
    IReadOnlyList<RectTransform> IMouseMenu.MouseRows
    {
        get
        {
            List<RectTransform> list = new List<RectTransform>();
            if (OnCodePage)
                foreach (OptionRow row in pageRows[book.Page - 1])
                    list.Add((RectTransform)row.transform);
            else if (selectorObjects != null)
                foreach (GameObject row in selectorObjects)
                    list.Add(row != null ? row.transform as RectTransform : null);
            return list;
        }
    }

    int IMouseMenu.MouseSelected => OnCodePage ? book.Row : selectorPos;
    bool IMouseMenu.MouseRowsSpanTheScreen => true;
    Camera IMouseMenu.MouseCamera => null;

    void IMouseMenu.MouseSelect(int row)
    {
        if (OnCodePage)
        {
            book.Select(row);
            MoveSelectorTo(pageRows[book.Page - 1][book.Row].transform);
            return;
        }
        selectorPos = row;
        optionSelected = rows != null ? rows[selectorPos] : (OptionSelected)selectorPos;
        MoveSelectorTo(selectorObjects[selectorPos].transform);
    }

    void IMouseMenu.MouseClick(int row, Vector2 point, Camera camera)
    {
        // A drawn row: straight to its point nearest the click (Off / On, a volume), one step at a time as the pad does
        GameObject[] points = OnCodePage ? null
            : optionSelected == OptionSelected.BGM ? bgmSelectorPositions
            : optionSelected == OptionSelected.SFX ? sfxSelectorPositions
            : optionSelected == OptionSelected.FULLSCREEN ? fullscreenSelectorPositions : null;
        if (points != null)
        {
            float[] xs = new float[points.Length];
            for (int i = 0; i < points.Length; i++)
                xs[i] = RectTransformUtility.WorldToScreenPoint(camera, points[i].transform.position).x;
            int target = MouseMenuRules.Nearest(xs, point.x);
            for (int guard = 0; guard < points.Length && target >= 0 && DrawnPosition() != target; guard++)
                ScrollMenuLeftRight(target > DrawnPosition());
            return;
        }

        // The language: a step towards the click
        if (!OnCodePage && optionSelected == OptionSelected.LANGUAGE && languageRow != null)
        {
            float languageX = RectTransformUtility.WorldToScreenPoint(camera, languageRow.Value.transform.position).x;
            ScrollMenuLeftRight(MouseMenuRules.StepsRight(point.x, languageX));
            return;
        }

        // The Resolution size: a click on it applies it, either side of it steps
        if (!OnCodePage && optionSelected == OptionSelected.RESOLUTION && resolutionRow != null)
        {
            int zone = MouseMenuRules.Zone(point.x, RectTransformUtility.WorldToScreenPoint(camera, resolutionRow.Value.transform.position).x);
            if (zone == 0)
                Confirm();
            else
                ScrollMenuLeftRight(zone > 0);
            return;
        }

        // A built row: a step towards the click
        if (OnCodePage)
        {
            float valueX = RectTransformUtility.WorldToScreenPoint(camera, pageRows[book.Page - 1][book.Row].Value.transform.position).x;
            ScrollMenuLeftRight(MouseMenuRules.StepsRight(point.x, valueX));
        }
    }

    // Where the selected drawn row's selector is
    int DrawnPosition()
    {
        return optionSelected == OptionSelected.BGM ? bgmPosition
            : optionSelected == OptionSelected.SFX ? sfxPosition
            : fullscreenPosition;
    }

    void IMouseMenu.MouseBack()
    {
        ExitMenu();
    }

    void MoveSelectorTo(Transform row)
    {
        selector.transform.position = new Vector3(selector.transform.position.x, row.position.y, selector.transform.position.z);
    }

    /// <summary>
    /// What each selector spot is: the scene's rows (in OptionSelected's order: Music, SFX, Fullscreen, then Quality once
    /// the screen has one), then Resolution
    /// </summary>
    public static List<OptionSelected> RowsFor(int sceneRows, OptionSelected first = OptionSelected.BGM)
    {
        List<OptionSelected> list = new List<OptionSelected>();
        for (int i = (int)first; i < (int)first + sceneRows && i <= (int)OptionSelected.QUALITY; i++)
            list.Add((OptionSelected)i);
        list.Add(OptionSelected.RESOLUTION);
        return list;
    }

    // The Resolution row, built under the screen's last row and joined to the up/down selector
    void AddResolutionRow()
    {
        if (selectorObjects == null || selectorObjects.Length == 0 || !(selectorObjects[selectorObjects.Length - 1].transform is RectTransform rowAbove))
            return;

        List<ScreenSize> listed = new List<ScreenSize>();
        foreach (Resolution resolution in Screen.resolutions)
            listed.Add(new ScreenSize(resolution.width, resolution.height));

        // The screen's own text (Fullscreen's Off and On) gives the row its font and colour
        TMP_Text style = rowAbove.parent != null ? rowAbove.parent.GetComponentInChildren<TMP_Text>(true) : null;
        resolutionRow = ResolutionRow.Create(rowAbove, style, listed, new ScreenSize(current.width, current.height),
            new ScreenSize(Screen.width, Screen.height));

        rows = RowsFor(selectorObjects.Length, firstRow);
        GameObject[] spots = new GameObject[selectorObjects.Length + 1];
        selectorObjects.CopyTo(spots, 0);
        spots[spots.Length - 1] = resolutionRow.gameObject;
        selectorObjects = spots;
    }

    // GENERAL's LANGUAGE row, under Resolution, with the flag of the language spoken: left and right change the game's
    // language at once, saved at once (docs/superpowers/specs/2026-10-24-localization-design.md)
    OptionRow languageRow;
    OptionSetting languageSetting;
    const float FLAG_X = 1045f; // right of the value, clear of the longest name
    const float FLAG_HEIGHT = 44f;

    /// <summary>The LANGUAGE row (null until Start)</summary>
    internal OptionRow LanguageRow => languageRow;

    void AddLanguageRow()
    {
        if (resolutionRow == null || rows == null)
            return;
        RectTransform above = (RectTransform)resolutionRow.transform;
        TMP_Text style = above.parent != null ? above.parent.GetComponentInChildren<TMP_Text>(true) : null;

        int[] languages = new int[LocaleCodes.All.Length];
        for (int i = 0; i < languages.Length; i++)
            languages[i] = i;
        languageSetting = new OptionSetting("LANGUAGE", languages, i => LanguageRules.Name(LocaleCodes.All[i]),
            Mathf.Max(0, System.Array.IndexOf(LocaleCodes.All, Loc.Language))) { LabelKey = "options.language" };
        languageSetting.Changed += i =>
        {
            current.language = LocaleCodes.All[i];
            Loc.SetLanguage(current.language);
            SaveOptions();
        };

        languageRow = OptionRow.Create((RectTransform)above.parent, style, languageSetting,
            above.anchoredPosition - new Vector2(0f, ResolutionRow.ROW_SPACING));
        RectTransform rect = (RectTransform)languageRow.transform;
        rect.anchorMin = above.anchorMin;
        rect.anchorMax = above.anchorMax;
        rect.anchoredPosition = above.anchoredPosition - new Vector2(0f, ResolutionRow.ROW_SPACING);
        LanguageFlag.Create(rect, new Vector2(0.5f, 0.5f), new Vector2(FLAG_X, 0f), FLAG_HEIGHT);

        rows.Add(OptionSelected.LANGUAGE);
        GameObject[] spots = new GameObject[selectorObjects.Length + 1];
        selectorObjects.CopyTo(spots, 0);
        spots[spots.Length - 1] = languageRow.gameObject;
        selectorObjects = spots;
    }

    ///<summary>
    /// A in Options: on the Resolution row, applies the size it shows and saves it
    ///</summary>
    public void Confirm()
    {
        if (OnCodePage || optionSelected != OptionSelected.RESOLUTION || resolutionRow == null)
            return;

        ScreenSize size = resolutionRow.Apply();
        current.width = size.Width;
        current.height = size.Height;
        DisplaySettings.Apply(current);
        SaveOptions();
    }

    string SettingsFilePath => Path.Combine(Application.persistentDataPath, SETTINGS_FILE_NAME);

    ///<summary>
    /// Saves the game's option settings
    ///</summary>
    public void SaveOptions()
    {
        // Keeps the display keys this menu has no rows for yet (DisplaySettings)
        current = (current ?? new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition))
            .WithMenuPositions(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition);

        try
        {
            File.WriteAllText(SettingsFilePath, current.ToText());
        }
        catch (Exception e) // a full disk or read-only folder shouldn't break the menu
        {
            Debug.LogWarning($"Could not save settings to {SettingsFilePath}: {e.Message}");
        }
    }

    ///<summary>
    /// Loads the game's option settings
    ///</summary>
    public void LoadOptions()
    {
        // The values set in the inspector are the defaults
        GameSettings settings = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, GraphicsQuality.DefaultLevel(SteamManager.IsSteamDeck));

        try
        {
            if (File.Exists(SettingsFilePath))
                settings = GameSettings.Parse(File.ReadAllText(SettingsFilePath), settings);
        }
        catch (Exception e) // an unreadable file falls back to the defaults
        {
            Debug.LogWarning($"Could not load settings from {SettingsFilePath}: {e.Message}");
        }

        current = settings;
        bgmPosition = settings.bgmPosition;
        sfxPosition = settings.sfxPosition;
        fullscreenPosition = settings.fullscreenPosition;
        qualityPosition = settings.qualityLevel;

        // Loads values as either loaded values or defualt values
        bgmValue = volumeValues[bgmPosition];
        soundManager.SetMusic(bgmValue);

        sfxValue = volumeValues[sfxPosition];
        soundManager.SetSFX(sfxValue);

        UpdateFullscreen(fullscreenPosition);

        GraphicsQuality.Apply(qualityPosition);

        // The Options pages' settings
        SplitScreenLayout.TwoPlayers = (SplitLayout)settings.splitLayout;
        AudioVolume.Set(settings.masterPosition, settings.backgroundMute == 1);
        Comfort.ReduceMotion = settings.reduceMotion == 1;
        Units.Imperial = settings.units == 1;
        HouseRules.Current = HouseRules.From(settings);

        // The language: the one picked here before, else Steam's, else the PC's, else English (LanguageRules)
        Loc.SetLanguage(LanguageRules.Pick(settings.language, SteamNames.Language(), Application.systemLanguage));
    }

    ///<summary>
    /// MATCH RULES changed (RulesPanel): kept in settings.cfg at once
    ///</summary>
    public void SetRules(HouseRules rules)
    {
        if (current == null)
            current = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition);
        rules.Into(current);
        SaveOptions();
    }

    // On DISPLAY, AUDIO or ACCESSIBILITY: the book's rows, not the drawn ones
    bool OnCodePage => book != null && book.Page > 0;


    ///<summary>
    /// Updates the selector graphics for the options
    ///</summary>
    public void UpdateSelectors()
    {
        bgmSelector.transform.position = new Vector3(bgmSelectorPositions[bgmPosition].transform.position.x, bgmSelector.transform.position.y, bgmSelector.transform.position.z);
        sfxSelector.transform.position = new Vector3(sfxSelectorPositions[sfxPosition].transform.position.x, sfxSelector.transform.position.y, sfxSelector.transform.position.z);

        fullscreenSelector.transform.position = new Vector3(fullscreenSelectorPositions[fullscreenPosition].transform.position.x,
            fullscreenSelector.transform.position.y, fullscreenSelector.transform.position.z);

        if (QualityRowExists)
            qualitySelector.transform.position = new Vector3(qualitySelectorPositions[qualityPosition].transform.position.x,
                qualitySelector.transform.position.y, qualitySelector.transform.position.z);
    }

    ///<summary>
    /// Main method which allows users to scroll up/ down
    ///</summary>
    public void ScrollMenuUpDown(bool direction)
    {
        if (OnCodePage)
        {
            book.Move(direction);
            MoveSelectorTo(pageRows[book.Page - 1][book.Row].transform);
            return;
        }

        // Positive Scroll
        if (direction)
        {
            if (selectorPos == selectorObjects.Length - 1)
            {
                selectorPos = 0;
            }
            else
            {
                selectorPos = selectorPos + 1;
            }
        }
        // Negative Scroll
        else
        {
            if (selectorPos == 0)
            {
                selectorPos = selectorObjects.Length - 1;
            }
            else
            {
                selectorPos = selectorPos - 1;
            }
        }

        optionSelected = rows != null ? rows[selectorPos] : (OptionSelected)selectorPos;

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    ///<summary>
    /// Main method which allows users to scroll 
    ///</summary>
    public void ScrollMenuLeftRight(bool direction)
    {
        if (OnCodePage)
        {
            // Saved at once: Options can be left other ways than B (an invite accepted), or the game quit from it
            if (book.Step(direction))
            {
                RefreshRows();
                SaveOptions();
            }
            return;
        }

        if(optionSelected == OptionSelected.BGM)
        {
            // Positive Scroll
            if (direction)
            {
                if (bgmPosition == bgmSelectorPositions.Length - 1)
                {
                    //bgmPosition = 0;
                }
                else
                {
                    bgmPosition = bgmPosition + 1;
                }
            }
            // Negative Scroll
            else
            {
                if (bgmPosition == 0)
                {
                    //bgmPosition = bgmSelectorPositions.Length - 1;
                }
                else
                {
                    bgmPosition = bgmPosition - 1;
                }
            }

            bgmSelector.transform.position = new Vector3(bgmSelectorPositions[bgmPosition].transform.position.x, bgmSelector.transform.position.y, bgmSelector.transform.position.z);
            bgmValue = volumeValues[bgmPosition];
            soundManager.SetMusic(bgmValue);
        }
        else if (optionSelected == OptionSelected.SFX)
        {
            // Positive Scroll
            if (direction)
            {
                if (sfxPosition == sfxSelectorPositions.Length - 1)
                {
                    //sfxPosition = 0;
                }
                else
                {
                    sfxPosition = sfxPosition + 1;
                }
            }
            // Negative Scroll
            else
            {
                if (sfxPosition == 0)
                {
                    //sfxPosition = sfxSelectorPositions.Length - 1;
                }
                else
                {
                    sfxPosition = sfxPosition - 1;
                }
            }

            sfxSelector.transform.position = new Vector3(sfxSelectorPositions[sfxPosition].transform.position.x, sfxSelector.transform.position.y, sfxSelector.transform.position.z);
            sfxValue = volumeValues[sfxPosition];
            soundManager.SetSFX(sfxValue);
        }
        else if(optionSelected == OptionSelected.FULLSCREEN)
        {
            // Positive Scroll
            if (direction)
            {
                if (fullscreenPosition == fullscreenSelectorPositions.Length - 1)
                {
                    //fullscreenPosition = 0;
                }
                else
                {
                    fullscreenPosition = fullscreenPosition + 1;
                }
            }
            // Negative Scroll
            else
            {
                if (fullscreenPosition == 0)
                {
                    //fullscreenPosition = fullscreenSelectorPositions.Length - 1;
                }
                else
                {
                    fullscreenPosition = fullscreenPosition - 1;
                }
            }

            fullscreenSelector.transform.position = new Vector3(fullscreenSelectorPositions[fullscreenPosition].transform.position.x,
                fullscreenSelector.transform.position.y, fullscreenSelector.transform.position.z);

            UpdateFullscreen(fullscreenPosition);
        }
        else if (optionSelected == OptionSelected.RESOLUTION && resolutionRow != null)
        {
            resolutionRow.Step(direction);
        }
        else if (optionSelected == OptionSelected.LANGUAGE && languageRow != null)
        {
            if (languageSetting.Step(direction))
                languageRow.Refresh();
        }
        else if (optionSelected == OptionSelected.QUALITY && QualityRowExists)
        {
            // Positive Scroll
            if (direction)
            {
                if (qualityPosition < GraphicsQuality.LEVEL_COUNT - 1)
                    qualityPosition = qualityPosition + 1;
            }
            // Negative Scroll
            else
            {
                if (qualityPosition > 0)
                    qualityPosition = qualityPosition - 1;
            }

            qualitySelector.transform.position = new Vector3(qualitySelectorPositions[qualityPosition].transform.position.x,
                qualitySelector.transform.position.y, qualitySelector.transform.position.z);

            GraphicsQuality.Apply(qualityPosition);
        }
    }

    ///<summary>
    /// Exits the options of the main menu
    ///</summary>
    public void ExitMenu()
    {
        // A size picked without A isn't kept
        if (resolutionRow != null)
            resolutionRow.Revert();
        SaveOptions();

        // Options opens on GENERAL next time
        if (book != null)
        {
            book.Reset();
            ShowPage();
        }

        menu.SwapToMainMenu();
        selectorPos = 0;
        optionSelected = rows != null ? rows[0] : firstRow;
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    /// <summary>
    /// Switches windowed (0) or fullscreen (1), applying the rest of the display settings with it: size, exclusive or
    /// borderless, VSync and the frame cap (DisplaySettings)
    /// </summary>
    public void UpdateFullscreen(int fullscreenValue)
    {
        if (current == null)
            current = new GameSettings(bgmPosition, sfxPosition, fullscreenValue, qualityPosition);
        current.fullscreenPosition = fullscreenValue;

        DisplaySettings.Apply(current);
    }
}