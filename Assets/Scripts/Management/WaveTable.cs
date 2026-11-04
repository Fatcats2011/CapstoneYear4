using UnityEngine;

/// <summary>
/// Which of the scene's authored wave rows (OrderManager.maxEasy / maxMedium / maxHard) each wave uses when the match
/// plays more or fewer waves than were authored (the Waves mutator): the rows are spread over the waves so the match
/// still ramps from easy to hard. One wave plays the last (hardest) row
/// </summary>
public static class WaveTable
{
    public static int IndexFor(int wave, int waves, int authored)
    {
        if (authored <= 1)
            return 0;
        if (waves <= 1)
            return authored - 1;
        int index = Mathf.RoundToInt(wave * (authored - 1) / (float)(waves - 1));
        return Mathf.Clamp(index, 0, authored - 1);
    }
}
