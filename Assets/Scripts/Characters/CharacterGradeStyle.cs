using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public sealed class CharacterGradeStyle
{
    [SerializeField] private Sprite gradeIcon;
    [SerializeField] private Color primaryColor = Color.white;
    [SerializeField] private Color backgroundColor =
        new(0.08f, 0.10f, 0.11f, 0.96f);
    [SerializeField] private Color outlineColor = Color.white;
    [SerializeField] private Color textColor = Color.white;

    public Sprite GradeIcon => gradeIcon;
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
        Color text,
        Sprite icon = null)
    {
        gradeIcon = icon;
        primaryColor = primary;
        backgroundColor = background;
        outlineColor = outline;
        textColor = text;
    }
}

public sealed class CharacterGradeIconStrip
{
    private readonly GameObject _root;
    private readonly RectTransform _rectTransform;
    private readonly List<Image> _icons = new();
    private HorizontalLayoutGroup _layout;
    private float _iconSize = 18f;
    private float _spacing = 4f;

    private CharacterGradeIconStrip(GameObject root)
    {
        _root = root;
        _rectTransform = root.transform as RectTransform;
    }

    public RectTransform RectTransform => _rectTransform;
    public float PreferredWidth { get; private set; }
    public int VisibleIconCount { get; private set; }

    public static CharacterGradeIconStrip Bind(
        Transform parent,
        string objectName,
        float iconSize,
        float spacing)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        if (existing == null ||
            existing.GetComponent<RectTransform>() == null ||
            existing.GetComponent<HorizontalLayoutGroup>() == null)
        {
            throw new InvalidOperationException(
                $"The authored grade icon strip '{objectName}' is incomplete.");
        }

        CharacterGradeIconStrip strip = new(existing.gameObject);
        strip.Configure(iconSize, spacing);
        return strip;
    }

    public void SetGrade(CharacterGrade grade)
    {
        CharacterGrade clamped =
            CharacterGradePresentation.Clamp(grade);
        CharacterGradeStyle style =
            CharacterGradePresentation.GetStyle(clamped);
        int iconCount =
            CharacterGradePresentation.GetIconCount(clamped);
        EnsureIconCount(iconCount);

        for (int index = 0; index < _icons.Count; index++)
        {
            Image icon = _icons[index];
            bool visible = index < iconCount;
            icon.gameObject.SetActive(visible);
            if (!visible)
                continue;

            icon.sprite = style.GradeIcon;
            icon.color = CharacterGradePresentation.GradeIconColor;
            icon.preserveAspect = true;
            icon.transform.localRotation = style.GradeIcon != null
                ? Quaternion.identity
                : Quaternion.Euler(0f, 0f, 45f);
        }

        VisibleIconCount = iconCount;
        PreferredWidth = iconCount > 0
            ? iconCount * _iconSize +
              (iconCount - 1) * _spacing
            : 0f;
        _root.SetActive(iconCount > 0);
        LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
    }

    private void Configure(float iconSize, float spacing)
    {
        _iconSize = Mathf.Max(1f, iconSize);
        _spacing = Mathf.Max(0f, spacing);
        _layout = _root.GetComponent<HorizontalLayoutGroup>();
        CaptureExistingIcons();
    }

    private void CaptureExistingIcons()
    {
        _icons.Clear();
        for (int index = 0;
             index < _root.transform.childCount;
             index++)
        {
            Transform child = _root.transform.GetChild(index);
            if (child == null ||
                !child.name.StartsWith("imgCharacterGrade_"))
            {
                continue;
            }

            Image icon = child.GetComponent<Image>();
            if (icon != null)
                _icons.Add(icon);
        }
    }

    private void EnsureIconCount(int count)
    {
        if (_icons.Count < count)
        {
            throw new InvalidOperationException(
                $"Grade icon strip '{_root.name}' requires {count} authored icons, " +
                $"but only {_icons.Count} are available.");
        }

        foreach (Image icon in _icons)
        {
            LayoutElement layout =
                icon.GetComponent<LayoutElement>();
            if (layout == null)
            {
                throw new InvalidOperationException(
                    $"Grade icon '{icon.name}' requires an authored LayoutElement.");
            }
        }
    }
}
