using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HotKeys : SingletonMonobehaviour<HotKeys>
{
    public event Action onIncrementWave;
    public event Action onDecrementWave;
    private void Update()
    {
        if (DevTools.GetKeyDown(KeyCode.Alpha1)) // decrease wave
        {
            onDecrementWave?.Invoke();
        }
        
        if (DevTools.GetKeyDown(KeyCode.Alpha2)) // increase wave
        {
            onIncrementWave?.Invoke();
        }

        if (DevTools.GetKeyDown(KeyCode.Alpha4))
        {
            SceneFlow.Current.LoadGameScene();
        }

        // Test players: F1 adds one, F2 presses A on all of them, F3 unplugs / replugs the newest
        if (DevTools.GetKeyDown(KeyCode.F1))
        {
            TestPlayers.Add();
        }

        if (DevTools.GetKeyDown(KeyCode.F2))
        {
            TestPlayers.PressSouthOnAll();
        }

        if (DevTools.GetKeyDown(KeyCode.F3))
        {
            TestPlayers.ToggleNewest();
        }

        // F4 cycles graphics quality (not saved), to compare levels in the Profiler
        if (DevTools.GetKeyDown(KeyCode.F4))
        {
            GraphicsQuality.Apply((GraphicsQuality.CurrentLevel + 1) % GraphicsQuality.LEVEL_COUNT);
            Debug.Log("Graphics quality: " + GraphicsQuality.LevelNames[GraphicsQuality.CurrentLevel]);
        }
    }
}
