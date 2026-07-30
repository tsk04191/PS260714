using System;
using UnityEngine;

[Serializable]
public sealed class CharacterGradeStyle
{
    [SerializeField] private Color primaryColor = Color.white;
    [SerializeField] private Color backgroundColor =
        new(0.08f, 0.10f, 0.11f, 0.96f);
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private Color textColor = Color.white;

    public Color PrimaryColor => primaryColor;
    public Color BackgroundColor => backgroundColor;
    public Color OutlineColor => outlineColor;
    public Color TextColor => textColor;

    public CharacterGradeStyle()
    {
    }

    public CharacterGradeStyle(
        Color primary,
        Color background,
        Color outline,
        Color text)
    {
        primaryColor = primary;
        backgroundColor = background;
        outlineColor = outline;
        textColor = text;
    }
}
