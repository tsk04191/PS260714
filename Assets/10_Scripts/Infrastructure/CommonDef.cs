using UnityEngine;

public static class CommonDef
{
    /// <summary>
    /// Test-build switch. Keep enabled while save compatibility is not
    /// guaranteed, and set to false before creating a release build.
    /// </summary>
    public static readonly bool ResetLocalDataOnNewBuild = true;

    public const string CharacterGradePaletteResourcePath =
        "Presentation/CharacterGradePalette";

    public const string CharacterRoleCatalogResourcePath =
        "Presentation/CharacterRoleCatalog";

    public const string DungeonHudPresentationResourcePath =
        "Presentation/DungeonHudPresentation";
}

public static class BattleStatusColors
{
    public static readonly Color32 Fire =
        new Color32(0xFF, 0x6A, 0x24, 0x9B);
}

public static class TimePrecision
{
    public const float Step = 0.1f;

    public static float FloorToTenth(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return 0f;

        value = Mathf.Max(0f, value);
        return Mathf.Floor(value * 10f + 0.0001f) * Step;
    }

    public static float Normalize(float value, float minimum = 0f)
    {
        minimum = FloorToTenth(minimum);
        return Mathf.Max(minimum, FloorToTenth(value));
    }
}
