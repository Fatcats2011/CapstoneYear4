using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class OptionsMenu : SingletonMonobehaviour<OptionsMenu>
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
        QUALITY = 3
    }
    public OptionSelected optionSelected;

    [Header("Selector Objects")]
    [SerializeField] GameObject selector;
    [SerializeField] GameObject[] selectorObjects;
    int selectorPos;
    [SerializeField] MainMenu menu;

    SoundManager soundManager;

    private void Start()
    {
        soundManager = SoundManager.Instance;
        LoadOptions();
    }

    string SettingsFilePath => Path.Combine(Application.persistentDataPath, SETTINGS_FILE_NAME);

    ///<summary>
    /// Saves the game's option settings
    ///</summary>
    public void SaveOptions()
    {
        GameSettings settings = new GameSettings(bgmPosition, sfxPosition, fullscreenPosition, qualityPosition);

        try
        {
            File.WriteAllText(SettingsFilePath, settings.ToText());
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

        //UpdateSelectors();
    }


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

        optionSelected = (OptionSelected)selectorPos;

        // Updates selector for current slider selected
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    ///<summary>
    /// Main method which allows users to scroll 
    ///</summary>
    public void ScrollMenuLeftRight(bool direction)
    {
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
        SaveOptions();

        menu.SwapToMainMenu();
        selectorPos = 0;
        optionSelected = 0;
        selector.transform.position = new Vector3(selector.transform.position.x, selectorObjects[selectorPos].transform.position.y, selector.transform.position.z);
    }

    /// <summary>
    /// Updates fullscreen based on input bool
    /// </summary>
    /// <param name="fullscreenValue"></param>
    public void UpdateFullscreen(int fullscreenValue)
    {
        if (fullscreenValue == 0)
        {
            Screen.fullScreen = false;
        }
        else
        {
            Screen.fullScreen = true;
        }
    }
}