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
        // 세부 직군의 ParentRole은 카탈로그 분류 정보일 뿐,
        // 캐릭터가 선택할 수 있는 직군 조합을 제한하지 않는다.
        return true;
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
