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

    public static CharacterGradeIconStrip GetOrCreate(
        Transform parent,
        string objectName,
        float iconSize,
        float spacing)
    {
        Transform existing = parent != null
            ? parent.Find(objectName)
            : null;
        GameObject root;
        if (existing != null)
        {
            root = existing.gameObject;
            if (root.GetComponent<RectTransform>() == null)
                root.AddComponent<RectTransform>();
        }
        else
        {
            root = new GameObject(
                objectName,
                typeof(RectTransform));
            root.transform.SetParent(parent, false);
        }

        CharacterGradeIconStrip strip = new(root);
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
        RectTransform.sizeDelta =
            new Vector2(PreferredWidth, _iconSize);
        _root.SetActive(iconCount > 0);
        LayoutRebuilder.MarkLayoutForRebuild(RectTransform);
    }

    private void Configure(float iconSize, float spacing)
    {
        _iconSize = Mathf.Max(1f, iconSize);
        _spacing = Mathf.Max(0f, spacing);
        _layout = _root.GetComponent<HorizontalLayoutGroup>();
        if (_layout == null)
            _layout = _root.AddComponent<HorizontalLayoutGroup>();
        _layout.padding = new RectOffset();
        _layout.spacing = _spacing;
        _layout.childAlignment = TextAnchor.MiddleLeft;
        _layout.childControlWidth = true;
        _layout.childControlHeight = true;
        _layout.childForceExpandWidth = false;
        _layout.childForceExpandHeight = false;
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
        while (_icons.Count < count)
        {
            int index = _icons.Count;
            GameObject iconObject = new(
                $"imgCharacterGrade_{index}",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement));
            iconObject.transform.SetParent(_root.transform, false);

            Image icon = iconObject.GetComponent<Image>();
            icon.raycastTarget = false;
            LayoutElement layout =
                iconObject.GetComponent<LayoutElement>();
            layout.minWidth = _iconSize;
            layout.preferredWidth = _iconSize;
            layout.minHeight = _iconSize;
            layout.preferredHeight = _iconSize;
            layout.flexibleWidth = 0f;
            layout.flexibleHeight = 0f;
            _icons.Add(icon);
        }

        foreach (Image icon in _icons)
        {
            LayoutElement layout =
                icon.GetComponent<LayoutElement>();
            if (layout == null)
                layout = icon.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = _iconSize;
            layout.preferredWidth = _iconSize;
            layout.minHeight = _iconSize;
            layout.preferredHeight = _iconSize;
        }
    }
}
