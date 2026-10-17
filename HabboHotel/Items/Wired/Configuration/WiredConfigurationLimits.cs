namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>Outer payload bounds; concrete boxes impose their own smaller field and selection limits.</summary>
public static class WiredConfigurationLimits
{
    // Neighborhood selectors save six fields followed by all 81 editor tile pairs.
    public const int NeighborhoodTiles = 81;
    public const int IntParams = 6 + NeighborhoodTiles * 2;
    // The server keeps accepting up to 100 picks so boxes saved with more than the editor's limit still load and save.
    public const int SelectedItems = 100;
    // The pick limit the editor frame advertises. Observed on hhus (AIR WIN63-202610131318-860371703): every box sends 20.
    public const int EditorFurniLimit = 20;
    public const int TextCharacters = 32768;
    public const int DelayPulses = 3600;
    // The October action delay section's precise mode: 0 to 1200 steps of 50 ms, saved as minus the milliseconds.
    public const int PreciseDelayStepMilliseconds = 50;
    public const int PreciseDelaySteps = 1200;

    /// <summary>A saved action delay: pulses (0 or more) or a precise delay as minus whole 50 ms steps.</summary>
    public static bool IsDelay(int delay) => delay is >= 0 and <= DelayPulses
        || delay < 0 && delay >= -PreciseDelaySteps * PreciseDelayStepMilliseconds && delay % PreciseDelayStepMilliseconds == 0;

    /// <summary>How long a saved action delay waits.</summary>
    public static long DelayMilliseconds(int delay) => delay < 0 ? -(long)delay : delay * 500L;
    public const int SelectionCode = 2;
}
