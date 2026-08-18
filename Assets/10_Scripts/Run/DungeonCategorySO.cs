using System;
using System.Collections.Generic;
using UnityEngine;

public enum EDungeonCategoryMode
{
    Explicit = 0,
    AllPlayable = 1,
    Theme = 2,
}

[CreateAssetMenu(
    fileName = "DungeonCategory",
    menuName = "Dungeon/Category")]
public sealed class DungeonCategorySO : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string categoryId;
    [SerializeField] private int displayOrder;

    [Header("Presentation")]
    [SerializeField] private string titleLocalizationKey;
    [SerializeField] private string fallbackTitle;
    [SerializeField] private string descriptionLocalizationKey;
    [SerializeField, TextArea(2, 5)] private string fallbackDescription;
    [SerializeField] private Sprite cardSprite;
    [SerializeField] private UiArtworkFraming cardFraming = new();
    [SerializeField] private Sprite backdropSprite;
    [SerializeField] private UiArtworkFraming backdropFraming = new();
    [SerializeField] private Color accentColor =
        new(0.72f, 0.88f, 0.74f, 1f);

    [Header("Contents")]
    [SerializeField] private EDungeonCategoryMode categoryMode;
    [SerializeField] private DungeonDefinition[] explicitDungeons =
        Array.Empty<DungeonDefinition>();
    [SerializeField] private DungeonThemeDefinition themeFilter;

    public string CategoryId => (categoryId ?? string.Empty).Trim();
    public int DisplayOrder => displayOrder;
    public string TitleLocalizationKey =>
        (titleLocalizationKey ?? string.Empty).Trim();
    public string FallbackTitle => (fallbackTitle ?? string.Empty).Trim();
    public string DescriptionLocalizationKey =>
        (descriptionLocalizationKey ?? string.Empty).Trim();
    public string FallbackDescription =>
        (fallbackDescription ?? string.Empty).Trim();
    public Sprite CardSprite => cardSprite != null
        ? cardSprite
        : ResolveFallbackSprite();
    public UiArtworkFraming CardFraming => cardFraming ?? new();
    public Sprite BackdropSprite => backdropSprite != null
        ? backdropSprite
        : CardSprite;
    public UiArtworkFraming BackdropFraming => backdropFraming ?? CardFraming;
    public Color AccentColor => accentColor;
    public EDungeonCategoryMode CategoryMode => categoryMode;
    public DungeonThemeDefinition ThemeFilter => themeFilter;

    public IReadOnlyList<DungeonDefinition> ResolveDungeons()
    {
        IReadOnlyList<DungeonDefinition> listed =
            DungeonDefinitionCatalog.GetStageSelectDefinitions();
        List<DungeonDefinition> result = new();
        HashSet<string> ids = new(StringComparer.OrdinalIgnoreCase);

        switch (categoryMode)
        {
            case EDungeonCategoryMode.Explicit:
                AddExplicitDungeons(result, ids);
                return result;
            case EDungeonCategoryMode.AllPlayable:
                for (int index = 0; index < listed.Count; index++)
                {
                    DungeonDefinition definition = listed[index];
                    if (definition == null || IsDebugUtility(definition))
                        continue;
                    AddDefinition(result, ids, definition);
                }
                break;
            case EDungeonCategoryMode.Theme:
                for (int index = 0; index < listed.Count; index++)
                {
                    DungeonDefinition definition = listed[index];
                    if (definition != null &&
                        ReferenceEquals(definition.Theme, themeFilter))
                    {
                        AddDefinition(result, ids, definition);
                    }
                }
                break;
        }

        result.Sort(CompareDungeons);
        return result;
    }

    public bool TryValidate(out string error)
    {
        if (string.IsNullOrWhiteSpace(CategoryId))
        {
            error = "Dungeon category id is required.";
            return false;
        }
        if (!Enum.IsDefined(typeof(EDungeonCategoryMode), categoryMode))
        {
            error = "Dungeon category mode is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(TitleLocalizationKey) &&
            string.IsNullOrWhiteSpace(FallbackTitle))
        {
            error = "Dungeon category title is required.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(DescriptionLocalizationKey) &&
            string.IsNullOrWhiteSpace(FallbackDescription))
        {
            error = "Dungeon category description is required.";
            return false;
        }
        if (categoryMode == EDungeonCategoryMode.Explicit &&
            (explicitDungeons == null || explicitDungeons.Length == 0))
        {
            error = "An explicit dungeon category requires at least one " +
                    "dungeon.";
            return false;
        }
        if (categoryMode == EDungeonCategoryMode.Theme &&
            themeFilter == null)
        {
            error = "A theme dungeon category requires a theme filter.";
            return false;
        }
        if (!CardFraming.TryValidate(out error))
        {
            error = "Dungeon category card framing is invalid: " + error;
            return false;
        }
        if (!BackdropFraming.TryValidate(out error))
        {
            error = "Dungeon category backdrop framing is invalid: " + error;
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void AddExplicitDungeons(
        List<DungeonDefinition> result,
        HashSet<string> ids)
    {
        if (explicitDungeons == null)
            return;
        for (int index = 0; index < explicitDungeons.Length; index++)
        {
            DungeonDefinition definition = explicitDungeons[index];
            if (definition == null || !definition.IsListedInStageSelect)
                continue;
            AddDefinition(result, ids, definition);
        }
    }

    private Sprite ResolveFallbackSprite()
    {
        if (themeFilter != null && themeFilter.BackgroundSprite != null)
            return themeFilter.BackgroundSprite;
        IReadOnlyList<DungeonDefinition> dungeons = ResolveDungeons();
        return dungeons.Count > 0
            ? dungeons[0].StageCoverSprite
            : null;
    }

    private static void AddDefinition(
        List<DungeonDefinition> result,
        HashSet<string> ids,
        DungeonDefinition definition)
    {
        if (definition == null ||
            string.IsNullOrWhiteSpace(definition.DungeonId) ||
            !ids.Add(definition.DungeonId))
        {
            return;
        }
        result.Add(definition);
    }

    private static bool IsDebugUtility(DungeonDefinition definition)
    {
        return string.Equals(
                   definition.DungeonId,
                   DungeonDefinitionCatalog.TutorialFieldId,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   definition.DungeonId,
                   DungeonDefinitionCatalog.PracticeBattleId,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareDungeons(
        DungeonDefinition left,
        DungeonDefinition right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left == null)
            return 1;
        if (right == null)
            return -1;
        int order = left.StageOrder.CompareTo(right.StageOrder);
        return order != 0
            ? order
            : string.Compare(
                left.DungeonId,
                right.DungeonId,
                StringComparison.Ordinal);
    }
}
