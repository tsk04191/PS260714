using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterRoleCatalog",
    menuName = "PS260714/Characters/Role Catalog")]
public sealed class CharacterRoleCatalogSO : ScriptableObject
{
    [SerializeField] private List<CharacterRoleSO> roles = new();
    [SerializeField] private List<CharacterArchetypeSO> archetypes = new();

    public IReadOnlyList<CharacterRoleSO> Roles =>
        roles != null ? roles : Array.Empty<CharacterRoleSO>();
    public IReadOnlyList<CharacterArchetypeSO> Archetypes =>
        archetypes != null
            ? archetypes
            : Array.Empty<CharacterArchetypeSO>();

    public CharacterRoleSO FindRole(string roleId)
    {
        if (string.IsNullOrWhiteSpace(roleId))
            return null;
        foreach (CharacterRoleSO role in Roles)
        {
            if (role != null && string.Equals(
                    role.RoleId,
                    roleId,
                    StringComparison.Ordinal))
            {
                return role;
            }
        }
        return null;
    }

    public CharacterArchetypeSO FindArchetype(string archetypeId)
    {
        if (string.IsNullOrWhiteSpace(archetypeId))
            return null;
        foreach (CharacterArchetypeSO archetype in Archetypes)
        {
            if (archetype != null && string.Equals(
                    archetype.ArchetypeId,
                    archetypeId,
                    StringComparison.Ordinal))
            {
                return archetype;
            }
        }
        return null;
    }

    public IReadOnlyList<string> GetValidationIssues()
    {
        List<string> issues = new();
        HashSet<CharacterRoleSO> registeredRoles = new();
        HashSet<string> roleIds = new(StringComparer.Ordinal);
        foreach (CharacterRoleSO role in Roles)
        {
            if (role == null)
            {
                issues.Add("직군 목록에 비어 있는 참조가 있습니다.");
                continue;
            }

            registeredRoles.Add(role);
            if (string.IsNullOrWhiteSpace(role.RoleId))
            {
                issues.Add($"{role.name}: 직군 ID가 비어 있습니다.");
            }
            else if (!roleIds.Add(role.RoleId))
            {
                issues.Add($"{role.name}: 직군 ID '{role.RoleId}'가 중복됩니다.");
            }
            ValidateLocalizedName(
                role.name,
                "직군",
                role.NameLocalizationKey,
                role.FallbackName,
                issues);
            ValidateOptionalLocalizedDescription(
                role.name,
                "직군",
                role.DescriptionLocalizationKey,
                role.FallbackDescription,
                issues);

            HashSet<string> passiveIds =
                new(StringComparer.Ordinal);
            foreach (CharacterRolePassiveDefinition passive in
                     role.PassiveDefinitions)
            {
                if (passive == null || !passive.IsConfigured)
                    continue;
                if (string.IsNullOrWhiteSpace(passive.PassiveId))
                {
                    issues.Add(
                        $"{role.name}: 설정된 직군 패시브 ID가 비어 있습니다.");
                }
                else if (!passiveIds.Add(passive.PassiveId))
                {
                    issues.Add(
                        $"{role.name}: 패시브 ID " +
                        $"'{passive.PassiveId}'가 중복됩니다.");
                }
                ValidateLocalizedName(
                    $"{role.name}/{passive.PassiveId}",
                    "직군 패시브",
                    passive.NameLocalizationKey,
                    passive.FallbackName,
                    issues);
                ValidateOptionalLocalizedDescription(
                    $"{role.name}/{passive.PassiveId}",
                    "직군 패시브",
                    passive.DescriptionLocalizationKey,
                    passive.FallbackDescription,
                    issues);
            }
        }

        HashSet<string> archetypeIds =
            new(StringComparer.Ordinal);
        foreach (CharacterArchetypeSO archetype in Archetypes)
        {
            if (archetype == null)
            {
                issues.Add("세부 직군 목록에 비어 있는 참조가 있습니다.");
                continue;
            }

            if (string.IsNullOrWhiteSpace(archetype.ArchetypeId))
            {
                issues.Add($"{archetype.name}: 세부 직군 ID가 비어 있습니다.");
            }
            else if (!archetypeIds.Add(archetype.ArchetypeId))
            {
                issues.Add(
                    $"{archetype.name}: 세부 직군 ID " +
                    $"'{archetype.ArchetypeId}'가 중복됩니다.");
            }
            ValidateLocalizedName(
                archetype.name,
                "세부 직군",
                archetype.NameLocalizationKey,
                archetype.FallbackName,
                issues);
            ValidateOptionalLocalizedDescription(
                archetype.name,
                "세부 직군",
                archetype.DescriptionLocalizationKey,
                archetype.FallbackDescription,
                issues);

            HashSet<string> passiveIds = new(StringComparer.Ordinal);
            foreach (CharacterRolePassiveDefinition passive in
                     archetype.PassiveDefinitions)
            {
                if (passive == null || !passive.IsConfigured)
                    continue;
                if (string.IsNullOrWhiteSpace(passive.PassiveId))
                {
                    issues.Add(
                        $"{archetype.name}: 설정된 세부 직군 패시브 " +
                        "ID가 비어 있습니다.");
                }
                else if (!passiveIds.Add(passive.PassiveId))
                {
                    issues.Add(
                        $"{archetype.name}: 세부 직군 패시브 ID " +
                        $"'{passive.PassiveId}'가 중복됩니다.");
                }
                ValidateLocalizedName(
                    $"{archetype.name}/{passive.PassiveId}",
                    "세부 직군 패시브",
                    passive.NameLocalizationKey,
                    passive.FallbackName,
                    issues);
                ValidateOptionalLocalizedDescription(
                    $"{archetype.name}/{passive.PassiveId}",
                    "세부 직군 패시브",
                    passive.DescriptionLocalizationKey,
                    passive.FallbackDescription,
                    issues);
            }

            if (archetype.ParentRole == null)
            {
                issues.Add(
                    $"{archetype.name}: 상위 직군이 지정되지 않았습니다.");
            }
            else if (!registeredRoles.Contains(archetype.ParentRole))
            {
                issues.Add(
                    $"{archetype.name}: 상위 직군이 카탈로그에 없습니다.");
            }
        }

        return issues;
    }

    private static void ValidateLocalizedName(
        string owner,
        string typeLabel,
        string localizationKey,
        string fallback,
        ICollection<string> issues)
    {
        if (string.IsNullOrWhiteSpace(localizationKey))
        {
            issues.Add(
                $"{owner}: {typeLabel} 이름 Localization 키가 " +
                "비어 있습니다.");
        }
        else if (!LocalizationService.TryGet(
                     localizationKey,
                     out _))
        {
            issues.Add(
                $"{owner}: Localization 키 '{localizationKey}'를 " +
                "찾을 수 없습니다.");
        }

        if (string.IsNullOrWhiteSpace(fallback))
        {
            issues.Add(
                $"{owner}: {typeLabel} 이름 fallback이 비어 있습니다.");
        }
    }

    private static void ValidateOptionalLocalizedDescription(
        string owner,
        string typeLabel,
        string localizationKey,
        string fallback,
        ICollection<string> issues)
    {
        bool hasKey = !string.IsNullOrWhiteSpace(localizationKey);
        bool hasFallback = !string.IsNullOrWhiteSpace(fallback);
        if (!hasKey && !hasFallback)
            return;

        if (!hasKey)
        {
            issues.Add(
                $"{owner}: {typeLabel} 설명 Localization 키가 " +
                "비어 있습니다.");
        }
        else if (!LocalizationService.TryGet(
                     localizationKey,
                     out _))
        {
            issues.Add(
                $"{owner}: 설명 Localization 키 " +
                $"'{localizationKey}'를 찾을 수 없습니다.");
        }

        if (!hasFallback)
        {
            issues.Add(
                $"{owner}: {typeLabel} 설명 fallback이 비어 있습니다.");
        }
    }

    private void OnValidate()
    {
        roles ??= new List<CharacterRoleSO>();
        archetypes ??= new List<CharacterArchetypeSO>();
        CharacterRolePresentation.Invalidate();
    }
}
