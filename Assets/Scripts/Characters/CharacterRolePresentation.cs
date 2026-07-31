using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

public static class CharacterRolePresentation
{
    private static CharacterRoleCatalogSO _catalog;
    private static bool _loadAttempted;

    public static CharacterRoleCatalogSO Catalog
    {
        get
        {
            EnsureLoaded();
            return _catalog;
        }
    }

    public static IReadOnlyList<CharacterRoleSO> Roles =>
        Catalog != null
            ? Catalog.Roles
            : Array.Empty<CharacterRoleSO>();

    public static IReadOnlyList<CharacterArchetypeSO> Archetypes =>
        Catalog != null
            ? Catalog.Archetypes
            : Array.Empty<CharacterArchetypeSO>();

    public static bool UsesKoreanLocale =>
        LocalizationService.CurrentLocale?.StartsWith(
            "ko",
            StringComparison.OrdinalIgnoreCase) == true;

    public static string GetRoleName(CharacterRoleSO role)
    {
        return role != null
            ? role.GetDisplayName()
            : (UsesKoreanLocale ? "미지정" : "UNASSIGNED");
    }

    public static string GetArchetypeName(
        CharacterArchetypeSO archetype)
    {
        return archetype != null
            ? archetype.GetDisplayName()
            : (UsesKoreanLocale ? "미지정" : "UNASSIGNED");
    }

    public static bool IsValidCombination(
        CharacterRoleSO role,
        CharacterArchetypeSO archetype)
    {
        return archetype == null ||
               (role != null && archetype.ParentRole == role);
    }

    public static void Invalidate()
    {
        _catalog = null;
        _loadAttempted = false;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Invalidate();
    }

    private static void EnsureLoaded()
    {
        if (_loadAttempted)
            return;
        _catalog = Resources.Load<CharacterRoleCatalogSO>(
            CommonDef.CharacterRoleCatalogResourcePath);
        _loadAttempted = true;
    }
}
