using UnityEngine;

public static class CharacterGradePresentation
{
    private const string ResourcePath =
        "Presentation/CharacterGradePalette";

    private static CharacterGradePaletteSO _palette;
    private static bool _loadAttempted;

    public static CharacterGradePaletteSO Palette
    {
        get
        {
            EnsureLoaded();
            return _palette;
        }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        _palette = null;
        _loadAttempted = false;
    }

    public static CharacterGrade Clamp(CharacterGrade grade)
    {
        return (CharacterGrade)Mathf.Clamp(
            (int)grade,
            (int)CharacterGrade.Grade0,
            (int)CharacterGrade.Grade3);
    }

    public static CharacterGradeStyle GetStyle(CharacterGrade grade)
    {
        EnsureLoaded();
        return _palette != null
            ? _palette.GetStyle(Clamp(grade))
            : CreateFallbackStyle(grade);
    }

    public static Color GetPrimaryColor(CharacterGrade grade)
    {
        return GetStyle(grade).PrimaryColor;
    }

    public static Color GetBackgroundColor(CharacterGrade grade)
    {
        return GetStyle(grade).BackgroundColor;
    }

    public static Color GetOutlineColor(CharacterGrade grade)
    {
        return GetStyle(grade).OutlineColor;
    }

    public static Color GetTextColor(CharacterGrade grade)
    {
        return GetStyle(grade).TextColor;
    }

    public static string GetLabel(CharacterGrade grade)
    {
        return $"{(int)Clamp(grade)}등급";
    }

    public static void Invalidate()
    {
        _palette = null;
        _loadAttempted = false;
    }

    internal static CharacterGradeStyle CreateFallbackStyle(
        CharacterGrade grade)
    {
        return Clamp(grade) switch
        {
            CharacterGrade.Grade3 => new CharacterGradeStyle(
                new Color(1f, 0.56f, 0.12f, 1f),
                new Color(0.22f, 0.10f, 0.03f, 0.96f),
                new Color(1f, 0.66f, 0.24f, 1f),
                Color.white),
            CharacterGrade.Grade2 => new CharacterGradeStyle(
                new Color(0.96f, 0.78f, 0.20f, 1f),
                new Color(0.20f, 0.16f, 0.04f, 0.96f),
                new Color(1f, 0.86f, 0.34f, 1f),
                Color.white),
            CharacterGrade.Grade1 => new CharacterGradeStyle(
                new Color(0.68f, 0.48f, 0.94f, 1f),
                new Color(0.13f, 0.08f, 0.20f, 0.96f),
                new Color(0.78f, 0.60f, 1f, 1f),
                Color.white),
            _ => new CharacterGradeStyle(
                new Color(0.38f, 0.68f, 0.96f, 1f),
                new Color(0.08f, 0.14f, 0.20f, 0.96f),
                new Color(0.50f, 0.78f, 1f, 1f),
                Color.white),
        };
    }

    private static void EnsureLoaded()
    {
        if (_loadAttempted)
            return;
        _palette = Resources.Load<CharacterGradePaletteSO>(ResourcePath);
        _loadAttempted = true;
    }
}
