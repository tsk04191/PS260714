using System;
using System.Collections.Generic;
using PS260714.Localization;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class CharacterRolePassiveDefinition :
    IBattleAbilityDefinition
{
    [SerializeField] private string passiveId;
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [FormerlySerializedAs("koreanName")]
    [SerializeField] private string fallbackName = "ROLE PASSIVE";
    [FormerlySerializedAs("koreanDescription")]
    [SerializeField, TextArea(2, 6)] private string fallbackDescription;
    [SerializeField] private Sprite iconSprite;
    [SerializeField] private CharacterPassiveDefinition ability = new();

    public string PassiveId => passiveId ?? string.Empty;
    public string NameLocalizationKey =>
        nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public string FallbackDescription =>
        fallbackDescription ?? string.Empty;
    public Sprite IconSprite => iconSprite;
    public CharacterPassiveDefinition Ability => ability;
    public bool IsConfigured =>
        ability != null && !ability.IsEmptyPlaceholder;
    public string AbilityId => PassiveId;
    public AbilityExecutionDomain ExecutionDomain =>
        AbilityExecutionDomain.Battle;
    public int AbilitySchemaVersion =>
        ability?.AbilitySchemaVersion ?? 0;
    public BattleEffectOriginKind OriginKind =>
        BattleEffectOriginKind.CharacterPassive;
    public BattleAbilityTargeting Targeting =>
        ability?.Targeting ?? default;
    public IEnumerable<IBattleEffectDefinition> BattleEffects =>
        ability?.BattleEffects ?? Array.Empty<IBattleEffectDefinition>();
    public bool UsesLegacyEffectStorage =>
        ability?.UsesLegacyEffectStorage ?? true;
    public bool HasExecutableContent =>
        ability?.HasExecutableContent ?? false;

    public string GetDisplayName()
    {
        return ResolveLocalizedText(
            nameLocalizationKey,
            fallbackName,
            "ROLE PASSIVE");
    }

    public string GetDescription()
    {
        return ResolveLocalizedText(
            descriptionLocalizationKey,
            fallbackDescription,
            string.Empty,
            BattleAbilityLocalizationArguments.Build(this));
    }

    public void Validate()
    {
        passiveId = (passiveId ?? string.Empty).Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        fallbackDescription =
            (fallbackDescription ?? string.Empty).Trim();
        ability ??= new CharacterPassiveDefinition();
        ability.Validate();
    }

    internal static string ResolveLocalizedText(
        string localizationKey,
        string fallback,
        string defaultFallback,
        params LocalizationArgument[] arguments)
    {
        if (!string.IsNullOrWhiteSpace(localizationKey) &&
            LocalizationService.TryGet(
                localizationKey,
                out string localized,
                arguments ?? Array.Empty<LocalizationArgument>()))
        {
            return localized;
        }

        if (!string.IsNullOrWhiteSpace(fallback))
            return fallback.Trim();
        return defaultFallback ?? string.Empty;
    }
}

[CreateAssetMenu(
    fileName = "CharacterRole",
    menuName = "PS260714/Characters/Role")]
public sealed class CharacterRoleSO : ScriptableObject,
    IBattleAbilityProvider
{
    [SerializeField] private string roleId =
        Guid.NewGuid().ToString("N");
    [SerializeField] private string nameLocalizationKey;
    [SerializeField] private string descriptionLocalizationKey;
    [FormerlySerializedAs("koreanName")]
    [SerializeField] private string fallbackName = "ROLE";
    [FormerlySerializedAs("koreanDescription")]
    [SerializeField, TextArea(2, 6)] private string fallbackDescription;
    [SerializeField] private Sprite iconSprite;
    [SerializeField]
    private List<CharacterRolePassiveDefinition> passiveDefinitions = new();

    public string RoleId => roleId ?? string.Empty;
    public string NameLocalizationKey =>
        nameLocalizationKey ?? string.Empty;
    public string DescriptionLocalizationKey =>
        descriptionLocalizationKey ?? string.Empty;
    public string FallbackName => fallbackName ?? string.Empty;
    public string FallbackDescription =>
        fallbackDescription ?? string.Empty;
    public Sprite IconSprite => iconSprite;
    public IReadOnlyList<CharacterRolePassiveDefinition>
        PassiveDefinitions => passiveDefinitions != null
            ? passiveDefinitions
            : Array.Empty<CharacterRolePassiveDefinition>();

    public IEnumerable<IBattleAbilityDefinition> EnumerateBattleAbilities()
    {
        foreach (CharacterRolePassiveDefinition passive in
                 PassiveDefinitions)
        {
            if (passive?.Ability?.HasExplicitEffects == true &&
                passive.Ability.HasSection(
                    CharacterPassiveSectionType.Ability))
                yield return passive;
        }
    }

    public string GetDisplayName()
    {
        return CharacterRolePassiveDefinition.ResolveLocalizedText(
            nameLocalizationKey,
            fallbackName,
            "UNASSIGNED ROLE");
    }

    public string GetDescription()
    {
        return CharacterRolePassiveDefinition.ResolveLocalizedText(
            descriptionLocalizationKey,
            fallbackDescription,
            string.Empty);
    }

    public void RegenerateRoleId()
    {
        roleId = Guid.NewGuid().ToString("N");
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(roleId))
            roleId = Guid.NewGuid().ToString("N");
        roleId = (roleId ?? string.Empty).Trim();
        nameLocalizationKey =
            (nameLocalizationKey ?? string.Empty).Trim();
        descriptionLocalizationKey =
            (descriptionLocalizationKey ?? string.Empty).Trim();
        fallbackName = (fallbackName ?? string.Empty).Trim();
        fallbackDescription =
            (fallbackDescription ?? string.Empty).Trim();
        passiveDefinitions ??=
            new List<CharacterRolePassiveDefinition>();
        foreach (CharacterRolePassiveDefinition passive in
                 passiveDefinitions)
        {
            passive?.Validate();
        }

        CharacterRolePresentation.Invalidate();
    }
}
